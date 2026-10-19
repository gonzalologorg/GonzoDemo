using UnityEngine;
using Unity.Netcode.Components;
using Blocks.Gameplay.Core;

namespace Gonzo
{
	/// <summary>
	/// Controls the player's Animator component based on the state of the <see cref="CoreMovement"/> controller.
	/// This component is responsible for setting locomotion parameters (speed, grounded, jump, etc.)
	/// and handling Animation Events to trigger sound effects like footsteps and landing sounds.
	/// It inherits from <see cref="NetworkAnimator"/> to automatically synchronize animation states across the network.
	/// </summary>
	[RequireComponent(typeof(AudioSource))]
	public class CoreAnimator : NetworkAnimator
	{
		#region Fields & Properties

		[Header("Component Dependencies")]
		[Tooltip("Reference to the CoreMovement component to get movement state information.")]
		[SerializeField] private CoreMovement coreMovement;

		private readonly int m_AnimIDSpeed = Animator.StringToHash("Speed");
		private readonly int m_AnimIDGrounded = Animator.StringToHash("Grounded");
		private readonly int m_AnimIDJump = Animator.StringToHash("Jump");
		private readonly int m_AnimIDFreeFall = Animator.StringToHash("FreeFall");
		private readonly int m_AnimIDMotionSpeed = Animator.StringToHash("MotionSpeed");

		#endregion

		#region Unity & Network Lifecycle

		protected override void Awake()
		{
			base.Awake();
			if (coreMovement == null)
			{
				Debug.LogError("[Core Animator] needs a CoreMovement component.");
			}
		}

		private void Update()
		{
			// We only want the owner to send animation state updates.
			// NetworkAnimator will handle propagating these changes to other clients.
			if (!IsOwner || coreMovement == null) return;

			UpdateLocomotionParameters();
		}

		#endregion

		/// <summary>
		/// This method is called by an AnimationEvent defined in the landing animation clip.
		/// It plays the landing sound effect.
		/// </summary>
		/// <param name="animationEvent">Data from the animation event.</param>
		public void OnLand(AnimationEvent animationEvent)
		{
		}

		#region Private Methods

		/// <summary>
		/// Reads the current state from the CoreMovement component and updates the Animator parameters accordingly.
		/// </summary>
		private void UpdateLocomotionParameters()
		{
			bool isGrounded = coreMovement.IsGrounded;
			float verticalVelocity = coreMovement.VerticalVelocity;

			// Set booleans for grounded, jumping, and falling states.
			Animator.SetBool(m_AnimIDGrounded, isGrounded);
			Animator.SetBool(m_AnimIDJump, !isGrounded && verticalVelocity > 0.1f);
			Animator.SetBool(m_AnimIDFreeFall, !isGrounded && verticalVelocity <= 0.1f);

			// Set floats for speed and input magnitude to drive blend trees.
			Animator.SetFloat(m_AnimIDSpeed, coreMovement.CurrentSpeed);
			Animator.SetFloat(m_AnimIDMotionSpeed, coreMovement.InputMagnitude);
		}

		public void TurnInPlaceStart()
		{
			Animator.applyRootMotion = true;
		}

		public void TurnInPlaceEnd()
		{
			Animator.applyRootMotion = false;
		}

		#endregion
	}
}
