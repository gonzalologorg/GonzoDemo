using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
namespace GonzoDemo
{
    public enum EPlayerState : byte
    {
        Alive,
        Dead,
        InShop
    }
    public class PlayerController : NetworkBehaviour
    {
        #region Networked Variables
        private readonly NetworkVariable<EPlayerState> n_PlayerState = new NetworkVariable<EPlayerState>(EPlayerState.Alive, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<FixedString64Bytes> n_Name = new NetworkVariable<FixedString64Bytes>(null, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> n_Health = new NetworkVariable<int>(100, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> n_MaxHealth = new NetworkVariable<int>(100, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> n_Energy = new NetworkVariable<int>(100, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> n_MaxEnergy = new NetworkVariable<int>(100, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public string PlayerName => n_Name.Value.ToString();
        public EPlayerState PlayerState => n_PlayerState.Value;

        public Action<string> OnPlayerChangedName;
        #endregion

        #region Fields
        public Transform LeftHandTransform;
        public Transform RightHandTransform;
        public Transform HeadTransform;

        public GameObject WeaponEquipped;

        PlayerControls controls;
        JutsuController controller;
        PlayerCombat playerCombat;
        HUDController hudController;
        PlayerStats playerStats;
        Blocks.Gameplay.Core.CoreMovement coreMovement;
        bool castingMovementWasEnabled;
        float castingElapsed;
        float castingMaxDuration;
        Action<float> castingCompleted;
        #endregion

        #region Player State Properties
        public int selectedJutsuIndex = 0;
        public bool IsCastingJutsu = false;
        public bool IsRecharging = false;
        #endregion

        void Awake()
        {
            controls = new PlayerControls();
			controller = GetComponent<JutsuController>();
			playerCombat = GetComponent<PlayerCombat>();
			hudController = GetComponent<HUDController>();
			playerStats = GetComponent<PlayerStats>();
			coreMovement = GetComponent<Blocks.Gameplay.Core.CoreMovement>();
		}

        public void SetHealth(int amount) => playerStats?.SetHealth(amount);
        public int GetHealth() => playerStats != null ? playerStats.GetHealth() : 0;
        public void AddHealth(int amount) => playerStats?.AddHealth(amount);

        public void SetChakra(int amount) => playerStats?.SetChakra(amount);
        public int GetChakra() => playerStats != null ? playerStats.GetChakra() : 0;
        public void AddChakra(int amount) => playerStats?.AddChakra(amount);
        public bool TryUseChakra(float amount) => playerStats != null && playerStats.TryUseChakra(amount);

		public override void OnNetworkSpawn()
		{
			n_Name.OnValueChanged += HandlePlayerNameChanged;
			ApplyPlayerName(n_Name.Value);

            if (!IsOwner)
                return;

            controls.Player.Attack.performed += OnAttack;
            controls.Player.CastJutsu.started += OnCastJutsu;
            controls.Player.CastJutsu.canceled += OnStopJutsu;
            controls.Player.Reload.started += ctx => IsRecharging = true;
            controls.Player.Reload.canceled += ctx => IsRecharging = false;
            controls.Player.Jutsu1.performed += ctx => SelectJutsu(0);
            controls.Player.Jutsu2.performed += ctx => SelectJutsu(1);
            controls.Player.Jutsu3.performed += ctx => SelectJutsu(2);
            controls.Player.Jutsu4.performed += ctx => SelectJutsu(3);
            controls.Player.Jutsu5.performed += ctx => SelectJutsu(4);

			SelectJutsu(selectedJutsuIndex);
			controls.Player.Enable();
		}

        public override void OnNetworkDespawn()
        {
			n_Name.OnValueChanged -= HandlePlayerNameChanged;
            CancelCasting();

            if (controls == null)
                return;

            controls.Player.Disable();
        }

        public void SetPlayerName(string newName)
        {
            if (IsServer)
            {
                n_Name.Value = newName;
            } else
            {
                SetPlayerNameServerRpc(newName);
            }
        }

		private void HandlePlayerNameChanged(FixedString64Bytes oldValue, FixedString64Bytes newValue)
		{
			ApplyPlayerName(newValue);
		}

		private void ApplyPlayerName(FixedString64Bytes value)
		{
			string playerName = value.ToString();
			OnPlayerChangedName?.Invoke(playerName);
			hudController?.UpdateName(playerName);
		}

        private void OnDestroy()
		{
			CancelCasting();
			controls?.Dispose();
		}

		private void Update()
		{
			if (!IsOwner || !IsCastingJutsu || castingMaxDuration <= 0f)
				return;

			Camera mainCamera = Camera.main;
			if (mainCamera != null)
			{
				float yaw = mainCamera.transform.eulerAngles.y;
				transform.rotation = Quaternion.Euler(0f, yaw, 0f);
				coreMovement?.SetTargetRotation(yaw);
			}

			castingElapsed += Time.deltaTime;
			if (castingElapsed >= castingMaxDuration)
				CompleteCasting();
		}

		/// <summary>Locks movement while a held cast runs and invokes its completion callback with 0..1 power.</summary>
		public void StartCasting(float maxDuration, Action<float> onComplete)
		{
			if (!IsOwner || maxDuration <= 0f)
				return;

			if (IsCastingJutsu && castingMaxDuration > 0f)
				CancelCasting();

			IsCastingJutsu = true;
			castingElapsed = 0f;
			castingMaxDuration = maxDuration;
			castingCompleted = onComplete;
			if (coreMovement != null)
			{
				castingMovementWasEnabled = coreMovement.IsMovementEnabled;
				coreMovement.IsMovementEnabled = false;
			}
		}

		public void StopCasting()
		{
			if (!IsCastingJutsu || castingMaxDuration <= 0f)
				return;

			CompleteCasting();
		}

		public void CancelCasting()
		{
			if (coreMovement != null && castingMaxDuration > 0f)
				coreMovement.IsMovementEnabled = castingMovementWasEnabled;

			IsCastingJutsu = false;
			castingElapsed = 0f;
			castingMaxDuration = 0f;
			castingCompleted = null;
		}

		private void CompleteCasting()
		{
			float power = castingMaxDuration > 0f
				? Mathf.Clamp01(castingElapsed / castingMaxDuration)
				: 0f;
			Action<float> callback = castingCompleted;
			if (coreMovement != null && castingMaxDuration > 0f)
				coreMovement.IsMovementEnabled = castingMovementWasEnabled;

			IsCastingJutsu = false;
			castingElapsed = 0f;
			castingMaxDuration = 0f;
			castingCompleted = null;
			callback?.Invoke(power);
		}

        public void SelectJutsu(int slot)
        {
            if (!IsOwner || slot < 0 || slot > 4)
                return;

            selectedJutsuIndex = slot;
            hudController?.SelectJutsu(slot);
        }

		public void OnAttack(InputAction.CallbackContext context)
        {
            if (context.performed && !IsCastingJutsu)
                playerCombat?.Attack();
        }

		public void OnCastJutsu(InputAction.CallbackContext context)
		{
            IsCastingJutsu = true;

            if (selectedJutsuIndex < 0 || selectedJutsuIndex > 4)
                return;

            controller.RequestCast(selectedJutsuIndex);
		}

        public void OnStopJutsu(InputAction.CallbackContext context)
		{
			if (controller != null)
				controller.StopLocalHoldCast();
			if (castingMaxDuration <= 0f)
				IsCastingJutsu = false;
		}


        #region RPC Messages
        [ServerRpc]
        private void SetPlayerNameServerRpc(string newName)
        {
            n_Name.Value = newName;
        }
        #endregion
    }

}
