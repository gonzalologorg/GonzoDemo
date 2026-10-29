using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
namespace GonzoDemo
{
    public class PlayerController : NetworkBehaviour
    {
        public Transform LeftHandTransform;
        public Transform RightHandTransform;
        public Transform HeadTransform;

        public GameObject WeaponEquipped;

        PlayerControls controls;
        JutsuController controller;
        public int selectedJutsuIndex = 0;
        public bool IsCastingJutsu = false;
        public bool IsRecharging = false;

		void Awake()
        {
            controls = new PlayerControls();
			controller = GetComponent<JutsuController>();

		}

		public override void OnNetworkSpawn()
		{
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
            if (controls == null)
                return;

            controls.Player.Disable();
        }

		private void OnDestroy()
		{
			controls?.Dispose();
		}

		public void OnAttack(InputAction.CallbackContext context)
        {
            Debug.Log("Do Attack");
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
	}

}