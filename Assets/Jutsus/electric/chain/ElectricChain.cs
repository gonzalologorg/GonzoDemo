using System.Collections;
using System.Collections.Generic;
using GonzoDemo;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public sealed class ElectricChain : NetworkBehaviour
{
    private readonly NetworkVariable<Vector3> segmentStart = new NetworkVariable<Vector3>(
        Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<Vector3> segmentEnd = new NetworkVariable<Vector3>(
        Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<bool> segmentVisible = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [SerializeField, Min(0.1f)] private float maxJumpDistance = 15f;
    [SerializeField] private Material chainMaterial;
    [SerializeField, Min(0f)] private float lineWidth = 0.12f;

    private const float JumpInterval = 0.5f;
    private const float RetryInterval = 0.1f;
    private readonly HashSet<ulong> damagedEntities = new HashSet<ulong>();
    private readonly List<SlowedTarget> slowedTargets = new List<SlowedTarget>();

    private struct SlowedTarget
    {
        public PlayerStats Stats;
        public float AppliedReduction;
    }

    private NetworkObject caster;
    private PlayerController casterController;
    private PlayerStats casterStats;
    private PlayerStats segmentSource;
    private PlayerStats currentTarget;
    private ulong previousEntityId;
    private float damagePerJump;
    private int jumpsRemaining;
    private float speedReduction;
    private float duration;
    private float startedAt;
    private Coroutine chainRoutine;
    private LineRenderer line;
    private Vector3 initialStart;

    private void Awake()
    {
        line = GetComponent<LineRenderer>();
        if (line == null)
            line = gameObject.AddComponent<LineRenderer>();

        line.useWorldSpace = true;
        line.positionCount = 2;
        line.startWidth = lineWidth;
        line.endWidth = lineWidth * 0.55f;
        line.numCapVertices = 4;
        line.numCornerVertices = 4;
        line.sharedMaterial = chainMaterial;
        line.enabled = false;
    }

    public void Initialize(NetworkObject casterObject, float damage, int jumps, float slow, float lifeTime)
    {
        caster = casterObject;
        casterController = caster != null ? caster.GetComponent<PlayerController>() : null;
        casterStats = caster != null ? caster.GetComponent<PlayerStats>() : null;
        damagePerJump = Mathf.Max(0f, damage);
        jumpsRemaining = Mathf.Max(0, jumps);
        speedReduction = Mathf.Max(0f, slow);
        duration = Mathf.Max(0f, lifeTime);
        initialStart = GetAimPoint(casterStats, casterController);
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            segmentStart.Value = initialStart;
            segmentEnd.Value = initialStart;
            startedAt = Time.time;
            previousEntityId = caster != null ? caster.NetworkObjectId : 0;
            chainRoutine = StartCoroutine(RunChain());
        }

        ApplySegment();
        segmentStart.OnValueChanged += HandleSegmentChanged;
        segmentEnd.OnValueChanged += HandleSegmentChanged;
        segmentVisible.OnValueChanged += HandleVisibilityChanged;
    }

    public override void OnNetworkDespawn()
    {
        segmentStart.OnValueChanged -= HandleSegmentChanged;
        segmentEnd.OnValueChanged -= HandleSegmentChanged;
        segmentVisible.OnValueChanged -= HandleVisibilityChanged;

        if (chainRoutine != null)
        {
            StopCoroutine(chainRoutine);
            chainRoutine = null;
        }

        RestoreSlows();
    }

    private void Update()
    {
        if (IsServer && segmentVisible.Value)
        {
            if (IsValidTarget(segmentSource) && IsValidTarget(currentTarget))
            {
                Vector3 from = GetAimPoint(segmentSource, segmentSource.GetComponent<PlayerController>());
                Vector3 to = GetAimPoint(currentTarget, currentTarget.GetComponent<PlayerController>());
                if ((segmentStart.Value - from).sqrMagnitude > 0.0001f)
                    segmentStart.Value = from;
                if ((segmentEnd.Value - to).sqrMagnitude > 0.0001f)
                    segmentEnd.Value = to;
            }
            else
            {
                segmentVisible.Value = false;
            }
        }

        if (IsServer && casterStats != null && IsValidTarget(casterStats))
        {
            Vector3 origin = GetAimPoint(casterStats, casterController);
            if (currentTarget == null && !segmentVisible.Value)
                segmentStart.Value = origin;
        }

        ApplySegment();
    }

    private IEnumerator RunChain()
    {
        while (Time.time - startedAt < duration && jumpsRemaining > 0)
        {
            PlayerStats nextTarget = FindNearestTarget();
            if (nextTarget == null)
            {
                segmentVisible.Value = false;
                yield return new WaitForSeconds(RetryInterval);
                continue;
            }

            NetworkObject targetObject = nextTarget.GetComponentInParent<NetworkObject>();
            if (targetObject == null)
            {
                yield return new WaitForSeconds(RetryInterval);
                continue;
            }

            Vector3 from = currentTarget != null
                ? GetAimPoint(currentTarget, currentTarget.GetComponent<PlayerController>())
                : GetAimPoint(casterStats, casterController);
            Vector3 to = GetAimPoint(nextTarget, nextTarget.GetComponent<PlayerController>());
            segmentStart.Value = from;
            segmentEnd.Value = to;
            segmentVisible.Value = true;

            segmentSource = currentTarget != null ? currentTarget : casterStats;
            previousEntityId = currentTarget != null
                ? currentTarget.NetworkObjectId
                : (caster != null ? caster.NetworkObjectId : 0);
            currentTarget = nextTarget;
            jumpsRemaining--;

            if (nextTarget != casterStats && damagedEntities.Add(targetObject.NetworkObjectId))
                HitTarget(nextTarget, to);

            yield return new WaitForSeconds(JumpInterval);
        }

        while (Time.time - startedAt < duration)
            yield return null;

        if (NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn(true);
    }

    private PlayerStats FindNearestTarget()
    {
        if (currentTarget != null && !IsValidTarget(currentTarget))
            currentTarget = null;

        Vector3 origin = currentTarget != null
            ? GetAimPoint(currentTarget, currentTarget.GetComponent<PlayerController>())
            : GetAimPoint(casterStats, casterController);

        PlayerStats nearest = null;
        float nearestDistance = maxJumpDistance * maxJumpDistance;
        PlayerStats[] candidates = FindObjectsByType<PlayerStats>(FindObjectsSortMode.None);

        foreach (PlayerStats candidate in candidates)
        {
            if (!IsValidTarget(candidate))
                continue;

            NetworkObject candidateObject = candidate.GetComponentInParent<NetworkObject>();
            if (candidateObject == null || candidateObject.NetworkObjectId == previousEntityId ||
                (currentTarget != null && candidateObject.NetworkObjectId == currentTarget.NetworkObjectId))
                continue;

            Vector3 candidatePosition = GetAimPoint(candidate, candidate.GetComponent<PlayerController>());
            float distance = (candidatePosition - origin).sqrMagnitude;
            if (distance > nearestDistance)
                continue;

            nearest = candidate;
            nearestDistance = distance;
        }

        return nearest;
    }

    private bool IsValidTarget(PlayerStats target)
    {
        if (target == null || !target.IsSpawned || !target.IsServer)
            return false;

        NetworkObject targetObject = target.GetComponentInParent<NetworkObject>();
        return targetObject != null && targetObject.IsSpawned;
    }

    private void HitTarget(PlayerStats target, Vector3 hitPoint)
    {
        if (target == null || target == casterStats)
            return;

        var damageInfo = new DamageInfo(
            damagePerJump,
            caster != null ? caster.gameObject : null,
            gameObject,
            DamageType.Magical,
            Vector3.zero,
            hitPoint,
            null);
        DamageResolver.Apply(target, damageInfo);

        if (speedReduction <= 0f)
            return;

        float currentSpeed = target.GetStat(PlayerStats.StatType.Speed);
        float reducedSpeed = Mathf.Max(0f, currentSpeed - speedReduction);
        float appliedReduction = currentSpeed - reducedSpeed;
        if (appliedReduction <= 0f)
            return;

        target.SetStat(PlayerStats.StatType.Speed, reducedSpeed);
        slowedTargets.Add(new SlowedTarget { Stats = target, AppliedReduction = appliedReduction });
    }

    private void RestoreSlows()
    {
        if (!IsServer)
            return;

        foreach (SlowedTarget slowedTarget in slowedTargets)
        {
            if (slowedTarget.Stats == null || !slowedTarget.Stats.IsSpawned || !slowedTarget.Stats.IsServer)
                continue;

            float speed = slowedTarget.Stats.GetStat(PlayerStats.StatType.Speed);
            slowedTarget.Stats.SetStat(PlayerStats.StatType.Speed, speed + slowedTarget.AppliedReduction);
        }

        slowedTargets.Clear();
    }

    private static Vector3 GetAimPoint(PlayerStats stats, PlayerController controller)
    {
        if (controller != null && controller.HeadTransform != null)
            return controller.HeadTransform.position;
        return stats != null ? stats.transform.position + Vector3.up : Vector3.zero;
    }

    private void HandleSegmentChanged(Vector3 previous, Vector3 current) => ApplySegment();
    private void HandleVisibilityChanged(bool previous, bool current) => ApplySegment();

    private void ApplySegment()
    {
        if (line == null)
            return;

        line.enabled = segmentVisible.Value;
        if (!segmentVisible.Value)
            return;

        line.startWidth = lineWidth;
        line.endWidth = lineWidth * 0.55f;
        line.SetPosition(0, segmentStart.Value);
        line.SetPosition(1, segmentEnd.Value);
    }
}
