using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using GonzoDemo.Combat;
using Blocks.Gameplay.Core;

public class RockWallController : HitProcessor
{
    public int Duration;
    [Header("Durability")]
    public NetworkVariable<int> wallHealth = new NetworkVariable<int>(
        100,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);


    [Header("Spawn push")]
    [SerializeField, Min(0f)] private float spawnPushForce = 16f;
    [SerializeField, Min(0f)] private float spawnPushUpwardForce = 5f;

    private static readonly int HealthParameter = Animator.StringToHash("Health");
    private static readonly int KillParameter = Animator.StringToHash("Kill");
    private static readonly int DeathState = Animator.StringToHash("Base Layer.death");

    private Animator m_AnimationController;
    private BoxCollider m_BoxCollider;
    private bool m_IsDying;

    private void Awake()
    {
        m_AnimationController = GetComponent<Animator>();
        m_BoxCollider = GetComponent<BoxCollider>();
    }

    public override void OnNetworkSpawn()
    {
        wallHealth.OnValueChanged += OnWallHealthChanged;
        UpdateHealthAnimation(wallHealth.Value);

        if (wallHealth.Value <= 0)
        {
            BeginDeath();
        }

        if (IsServer)
        {
            PushOverlappingPlayersOnSpawnRpc();
        }
    }

    public override void OnNetworkDespawn()
    {
        wallHealth.OnValueChanged -= OnWallHealthChanged;
        base.OnNetworkDespawn();
    }

    protected override void HandleHit(HitInfo info)
    {
        Debug.Log($"RockWall hit with amount: {info.amount}, current wall health: {wallHealth.Value}");
        if (!IsServer || m_IsDying || info.amount <= 0f)
        {
            return;
        }

        int damage = Mathf.Max(1, Mathf.CeilToInt(info.amount));
        wallHealth.Value = Mathf.Max(0, wallHealth.Value - damage);
    }

    private void OnWallHealthChanged(int oldValue, int newValue)
    {
        UpdateHealthAnimation(newValue);

        if (newValue <= 0)
        {
            BeginDeath();
        }
    }

    private void UpdateHealthAnimation(int health)
    {
        if (m_AnimationController != null)
        {
            m_AnimationController.SetInteger(HealthParameter, health);
        }
    }

    private void BeginDeath()
    {
        if (m_IsDying)
        {
            return;
        }

        m_IsDying = true;

        if (m_BoxCollider != null)
        {
            m_BoxCollider.enabled = false;
        }

        if (m_AnimationController != null)
        {
            m_AnimationController.SetTrigger(KillParameter);
        }

        if (IsServer)
        {
            StartCoroutine(DespawnAfterDeathAnimation());
        }
    }

    private IEnumerator DespawnAfterDeathAnimation()
    {
        float timeout = GetDeathAnimationDuration() + 2f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            if (m_AnimationController != null)
            {
                AnimatorStateInfo state = m_AnimationController.GetCurrentAnimatorStateInfo(0);
                if (state.fullPathHash == DeathState && state.normalizedTime >= 1f)
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

    private float GetDeathAnimationDuration()
    {
        if (m_AnimationController != null && m_AnimationController.runtimeAnimatorController != null)
        {
            foreach (AnimationClip clip in m_AnimationController.runtimeAnimatorController.animationClips)
            {
                if (clip.name.ToLowerInvariant().Contains("death"))
                {
                    float speed = Mathf.Abs(m_AnimationController.speed);
                    return speed > 0f ? clip.length / speed : clip.length;
                }
            }
        }

        return 2f;
    }

    [Rpc(SendTo.Everyone)]
    private void PushOverlappingPlayersOnSpawnRpc()
    {
        if (m_BoxCollider == null || spawnPushForce <= 0f)
        {
            return;
        }

        Vector3 scale = m_BoxCollider.transform.lossyScale;
        Vector3 halfExtents = Vector3.Scale(m_BoxCollider.size, new Vector3(
            Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z))) * 0.5f;
        Vector3 center = m_BoxCollider.transform.TransformPoint(m_BoxCollider.center);
        Collider[] overlaps = Physics.OverlapBox(
            center,
            halfExtents,
            m_BoxCollider.transform.rotation,
            Physics.AllLayers,
            QueryTriggerInteraction.Collide);

        HashSet<CoreMovement> pushedPlayers = new HashSet<CoreMovement>();
        foreach (Collider overlap in overlaps)
        {
            CoreMovement movement = overlap.GetComponentInParent<CoreMovement>();
            if (movement == null || !movement.IsOwner || !pushedPlayers.Add(movement))
            {
                continue;
            }

            Vector3 direction = movement.transform.position - center;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f)
            {
                direction = movement.transform.position - transform.position;
                direction.y = 0f;
            }
            if (direction.sqrMagnitude < 0.001f)
            {
                direction = transform.forward;
                direction.y = 0f;
            }

            direction.Normalize();
            Vector3 impulse = direction * spawnPushForce + Vector3.up * spawnPushUpwardForce;
            movement.ApplyExternalForce(impulse, ForceMode.Impulse);
        }
    }
}
