using System.Collections;
using Unity.Netcode;
using UnityEngine;
using Blocks.Gameplay.Core;
using GonzoDemo;

[RequireComponent(typeof(NetworkObject))]
public class BubbleContainer : NetworkBehaviour
{
    private static readonly int PopTrigger = Animator.StringToHash("Pop");
    private static readonly int PopState = Animator.StringToHash("Base Layer.BubblePop");

    private readonly NetworkVariable<NetworkObjectReference> capturedTargetReference =
        new NetworkVariable<NetworkObjectReference>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    private Animator animator;
    private Collider bubbleCollider;
    private NetworkObject casterNetworkObject;
    private PlayerStats casterStats;
    private PlayerController casterController;
    private float chakraRemainder;
    private Transform capturedTarget;
    private CoreMovement lockedCoreMovement;
    private CharacterController lockedCharacterController;
    private Rigidbody lockedRigidbody;
    private bool previousMovementEnabled;
    private bool previousCharacterControllerEnabled;
    private bool previousRigidbodyKinematic;
    private Vector3 previousRigidbodyVelocity;
    private Vector3 previousRigidbodyAngularVelocity;
    private bool movementLocked;
    private bool hasCapturedTarget;
    private float duration;
    private float draining;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        bubbleCollider = GetComponent<Collider>();
    }

    public void Initialize(
        NetworkObject caster,
        PlayerStats casterPlayerStats,
        float captureDuration,
        float chakraDrainRate,
        GameObject initialTarget)
    {
        casterNetworkObject = caster;
        casterStats = casterPlayerStats;
        casterController = caster != null ? caster.GetComponent<PlayerController>() : null;
        duration = Mathf.Max(0f, captureDuration);
        draining = Mathf.Max(0f, chakraDrainRate);

        if (initialTarget == null)
        {
            return;
        }

        capturedTarget = initialTarget.transform;
        hasCapturedTarget = true;
        NetworkObject targetNetworkObject = initialTarget.GetComponentInParent<NetworkObject>();
        if (targetNetworkObject != null && targetNetworkObject.IsSpawned)
        {
            capturedTargetReference.Value = new NetworkObjectReference(targetNetworkObject);
        }
    }

    public override void OnNetworkSpawn()
    {
        capturedTargetReference.OnValueChanged += OnCapturedTargetChanged;

        if (TryGetCapturedTarget(out GameObject target))
        {
            capturedTarget = target.transform;
            hasCapturedTarget = true;
        }

        if (hasCapturedTarget && capturedTarget != null)
        {
            LockMovement(capturedTarget.gameObject);
            if (bubbleCollider != null)
            {
                bubbleCollider.enabled = false;
            }
        }

        if (IsServer)
        {
            StartCoroutine(RunLifetime());
        }
    }

    public override void OnNetworkDespawn()
    {
        capturedTargetReference.OnValueChanged -= OnCapturedTargetChanged;
        UnlockMovement();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || !IsSpawned || hasCapturedTarget || other == null)
        {
            return;
        }

        GameObject target = FindCaptureTarget(other, casterNetworkObject);
        if (target != null)
        {
            CaptureTarget(target);
        }
    }

    public static GameObject FindCaptureTarget(Collider targetCollider, NetworkObject caster)
    {
        if (targetCollider == null)
        {
            return null;
        }

        NetworkObject targetNetworkObject = targetCollider.GetComponentInParent<NetworkObject>();
        if (targetNetworkObject != null && caster != null &&
            targetNetworkObject.NetworkObjectId == caster.NetworkObjectId)
        {
            return null;
        }

        if (caster != null && targetCollider.transform.root == caster.transform.root)
        {
            return null;
        }

        CoreMovement movement = targetCollider.GetComponentInParent<CoreMovement>();
        CharacterController characterController = targetCollider.GetComponentInParent<CharacterController>();
        Rigidbody rigidbody = targetCollider.GetComponentInParent<Rigidbody>();

        if (targetNetworkObject != null && targetNetworkObject.IsSpawned)
        {
            return targetNetworkObject.gameObject;
        }

        if (movement != null) return movement.gameObject;
        if (characterController != null) return characterController.gameObject;
        if (rigidbody != null) return rigidbody.gameObject;
        return null;
    }

    private void CaptureTarget(GameObject target)
    {
        if (hasCapturedTarget || target == null)
        {
            return;
        }

        capturedTarget = target.transform;
        hasCapturedTarget = true;
        NetworkObject targetNetworkObject = target.GetComponentInParent<NetworkObject>();
        if (targetNetworkObject != null && targetNetworkObject.IsSpawned)
        {
            capturedTargetReference.Value = new NetworkObjectReference(targetNetworkObject);
        }

        LockMovement(target);
        if (bubbleCollider != null)
        {
            bubbleCollider.enabled = false;
        }
    }

    private void OnCapturedTargetChanged(
        NetworkObjectReference previousTarget,
        NetworkObjectReference currentTarget)
    {
        UnlockMovement();

        if (currentTarget.TryGet(out NetworkObject targetNetworkObject))
        {
            capturedTarget = targetNetworkObject.transform;
            hasCapturedTarget = true;
            LockMovement(targetNetworkObject.gameObject);
            if (bubbleCollider != null)
            {
                bubbleCollider.enabled = false;
            }
        }
    }

    private bool TryGetCapturedTarget(out GameObject target)
    {
        target = null;
        if (!capturedTargetReference.Value.TryGet(out NetworkObject targetNetworkObject))
        {
            return false;
        }

        target = targetNetworkObject.gameObject;
        return true;
    }

    private void LockMovement(GameObject target)
    {
        if (movementLocked || target == null)
        {
            return;
        }

        lockedCoreMovement = target.GetComponentInChildren<CoreMovement>(true);
        lockedCharacterController = target.GetComponentInChildren<CharacterController>(true);
        lockedRigidbody = target.GetComponentInChildren<Rigidbody>(true);

        if (lockedCoreMovement != null)
        {
            previousMovementEnabled = lockedCoreMovement.IsMovementEnabled;
            lockedCoreMovement.IsMovementEnabled = false;
        }

        if (lockedCharacterController != null && lockedCoreMovement == null)
        {
            previousCharacterControllerEnabled = lockedCharacterController.enabled;
            lockedCharacterController.enabled = false;
        }

        if (lockedRigidbody != null && lockedCoreMovement == null && lockedCharacterController == null)
        {
            previousRigidbodyKinematic = lockedRigidbody.isKinematic;
            previousRigidbodyVelocity = lockedRigidbody.linearVelocity;
            previousRigidbodyAngularVelocity = lockedRigidbody.angularVelocity;
            lockedRigidbody.linearVelocity = Vector3.zero;
            lockedRigidbody.angularVelocity = Vector3.zero;
            lockedRigidbody.isKinematic = true;
        }

        movementLocked = true;
    }

    private void UnlockMovement()
    {
        if (!movementLocked)
        {
            return;
        }

        if (lockedCoreMovement != null)
        {
            lockedCoreMovement.IsMovementEnabled = previousMovementEnabled;
        }

        if (lockedCharacterController != null && lockedCoreMovement == null)
        {
            lockedCharacterController.enabled = previousCharacterControllerEnabled;
        }

        if (lockedRigidbody != null && lockedCoreMovement == null && lockedCharacterController == null)
        {
            lockedRigidbody.isKinematic = previousRigidbodyKinematic;
            if (!previousRigidbodyKinematic)
            {
                lockedRigidbody.linearVelocity = previousRigidbodyVelocity;
                lockedRigidbody.angularVelocity = previousRigidbodyAngularVelocity;
            }
        }

        lockedCoreMovement = null;
        lockedCharacterController = null;
        lockedRigidbody = null;
        movementLocked = false;
    }

    private IEnumerator RunLifetime()
    {
        float elapsed = 0f;
        float drainAccumulator = 0f;

        while (elapsed < duration)
        {
            float deltaTime = Mathf.Min(Time.deltaTime, duration - elapsed);
            if (hasCapturedTarget && draining > 0f && casterStats != null && casterStats.IsSpawned)
            {
                drainAccumulator += deltaTime;
                if (drainAccumulator >= 1f)
                {
                    GrantChakra(draining * drainAccumulator);
                    drainAccumulator = 0f;
                }
            }

            elapsed += deltaTime;
            yield return null;
        }

        if (hasCapturedTarget && drainAccumulator > 0f)
        {
            GrantChakra(draining * drainAccumulator);
        }

        PlayPopRpc();
        yield return WaitForPopAnimation();

        UnlockMovement();
        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
    }

    private void GrantChakra(float amount)
    {
        if (!IsServer || casterStats == null || !casterStats.IsSpawned || casterController == null || amount <= 0f)
        {
            return;
        }

        chakraRemainder += amount;
        int wholeChakra = Mathf.FloorToInt(chakraRemainder);
        if (wholeChakra <= 0)
            return;

        chakraRemainder -= wholeChakra;
        casterController.AddChakra(wholeChakra);
    }

    [Rpc(SendTo.Everyone)]
    private void PlayPopRpc()
    {
        if (animator != null)
        {
            animator.SetTrigger(PopTrigger);
        }
    }

    private IEnumerator WaitForPopAnimation()
    {
        float timeout = GetClipDuration("BubblePop", 0.25f) + 1f;
        float elapsed = 0f;
        bool popStarted = false;

        while (elapsed < timeout)
        {
            if (animator != null)
            {
                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
                if (state.fullPathHash == PopState)
                {
                    popStarted = true;
                    if (state.normalizedTime >= 1f)
                    {
                        yield break;
                    }
                }
                else if (popStarted)
                {
                    yield break;
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private float GetClipDuration(string clipName, float fallback)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return fallback;
        }

        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip.name == clipName)
            {
                float speed = Mathf.Abs(animator.speed);
                return speed > 0f ? clip.length / speed : clip.length;
            }
        }

        return fallback;
    }
}
