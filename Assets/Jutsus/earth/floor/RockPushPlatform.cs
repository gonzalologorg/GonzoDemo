using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using GonzoDemo;
using Blocks.Gameplay.Core;

[RequireComponent(typeof(NetworkObject))]
public class RockPushPlatform : NetworkBehaviour
{
    private static readonly int BirthState = Animator.StringToHash("Base Layer.birth");

    public int Damage;
    public float PushForce = 10f;

    private readonly NetworkVariable<ulong> casterClientId = new NetworkVariable<ulong>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    private readonly HashSet<ulong> affectedPlayers = new HashSet<ulong>();
    private Animator animator;

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            StartCoroutine(DespawnAfterAnimation());
        }
    }

    public void SetCaster(ulong clientId)
    {
        casterClientId.Value = clientId;
    }

    private IEnumerator DespawnAfterAnimation()
    {
        float timeout = GetAnimationDuration() + 2f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            if (animator != null)
            {
                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
                if (state.fullPathHash == BirthState && state.normalizedTime >= 1f)
                {
                    break;
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private float GetAnimationDuration()
    {
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip.name == "birth")
                {
                    float speed = Mathf.Abs(animator.speed);
                    return speed > 0f ? clip.length / speed : clip.length;
                }
            }
        }

        return 2f;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsSpawned || other == null)
        {
            return;
        }

        PlayerStats victim = other.GetComponentInParent<PlayerStats>();
        if (victim == null || victim.OwnerClientId == casterClientId.Value ||
            !affectedPlayers.Add(victim.OwnerClientId))
        {
            return;
        }

        Vector3 pushDirection = victim.transform.position - transform.position;
        pushDirection.y = 0f;
        if (pushDirection.sqrMagnitude < 0.001f)
        {
            pushDirection = transform.forward;
        }
        pushDirection.Normalize();

        if (IsServer && Damage > 0)
        {
            Vector3 hitPosition = other.ClosestPoint(transform.position);
            DamageResolver.Apply(victim, new DamageInfo(
                Damage,
                NetworkManager.SpawnManager.GetPlayerNetworkObject(casterClientId.Value)?.gameObject,
                gameObject,
                DamageType.Magical,
                pushDirection * PushForce,
                hitPosition,
                other));
        }

        CoreMovement movement = victim.GetComponentInParent<CoreMovement>();
        if (movement == null || !movement.IsOwner || PushForce <= 0f)
        {
            return;
        }

        movement.ApplyExternalForce(pushDirection * PushForce, ForceMode.Impulse);
    }
}
