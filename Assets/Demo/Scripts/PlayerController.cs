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
        private readonly NetworkVariable<int> n_Level = new NetworkVariable<int>(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
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
        HUDController hudController;
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
			hudController = GetComponent<HUDController>();
		}

		public override void OnNetworkSpawn()
		{
            n_Name.OnValueChanged += (oldValue, newValue) =>
            {
                OnPlayerChangedName?.Invoke(newValue.ToString());
                hudController.UpdateName(newValue.ToString());
			};

            if (!IsOwner)
                return;

            controls.Player.Attack.performed += OnAttack;
            controls.Player.CastJutsu.started += OnCastJutsu;
            controls.Player.CastJutsu.canceled += OnStopJutsu;
            controls.Player.Reload.started += ctx => IsRecharging = true;
            controls.Player.Reload.canceled += ctx => IsRecharging = false;
            controls.Player.Jutsu1.performed += ctx => selectedJutsuIndex = 0;
            controls.Player.Jutsu2.performed += ctx => selectedJutsuIndex = 1;
            controls.Player.Jutsu3.performed += ctx => selectedJutsuIndex = 2;
            controls.Player.Jutsu4.performed += ctx => selectedJutsuIndex = 3;
            controls.Player.Jutsu5.performed += ctx => selectedJutsuIndex = 4;

			controls.Player.Enable();
		}

        public override void OnNetworkDespawn()
        {
            n_Name.OnValueChanged -= (oldValue, newValue) =>
            {
                OnPlayerChangedName?.Invoke(newValue.ToString());
            };

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

        private void OnDestroy()
		{
			controls?.Dispose();
		}

		public void OnAttack(InputAction.CallbackContext context)
        {
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