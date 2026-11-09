using System.Collections.Generic;
using GonzoDemo;
using GonzoDemo.Combat;
using Unity.Netcode;
using UnityEngine;

public class WaterJetBreath : BreathBase
{
    [Min(0f)] public float DamagePerSecond;
    [Min(0f)] public float DrainAmount;

    private PlayerController caster;
    private PlayerStats casterStats;
    private readonly Dictionary<int, int> damagedFrameByTarget = new Dictionary<int, int>();
    private readonly Dictionary<int, float> damageRemainderByTarget = new Dictionary<int, float>();
    private float healingRemainder;

    public void Initialize(
        PlayerController castingPlayer,
        float damagePerSecond,
        float drain,
        float distance,
        float duration)
    {
        caster = castingPlayer;
        casterStats = castingPlayer != null ? castingPlayer.GetComponent<PlayerStats>() : null;
        DamagePerSecond = Mathf.Max(0f, damagePerSecond);
        DrainAmount = Mathf.Max(0f, drain);
        maxBreathDistance = Mathf.Max(0f, distance);
        lifeTime = Mathf.Max(0f, duration);
        SetCaster(castingPlayer);
    }

    public override void OnBreathImpact(Collider impactPoint)
    {
        Debug.Log("Breath impact detected on: " + impactPoint.name);
        if (!IsServer || impactPoint == null || DamagePerSecond <= 0f)
            return;

        NetworkObject victimNetworkObject = impactPoint.GetComponentInParent<NetworkObject>();
        int targetKey = victimNetworkObject != null
            ? victimNetworkObject.NetworkObjectId.GetHashCode()
            : impactPoint.transform.root.gameObject.GetEntityId().GetHashCode();

        if (damagedFrameByTarget.TryGetValue(targetKey, out int lastFrame) && lastFrame == Time.frameCount)
            return;
        damagedFrameByTarget[targetKey] = Time.frameCount;

        damageRemainderByTarget.TryGetValue(targetKey, out float remainder);
        remainder += DamagePerSecond * Time.deltaTime;
        int damage = Mathf.FloorToInt(remainder);
        damageRemainderByTarget[targetKey] = remainder - damage;
        if (damage <= 0)
            return;

        GameObject attacker = caster != null ? caster.gameObject : null;
        PlayerStats victimStats = impactPoint.GetComponentInParent<PlayerStats>();
        if (victimStats != null)
        {
            if (victimStats == casterStats)
                return;

            Vector3 hitPosition = impactPoint.ClosestPoint(transform.position);
            DamageInfo damageInfo = new DamageInfo(
                damage,
                attacker,
                gameObject,
                DamageType.Magical,
                transform.forward * damage,
                hitPosition,
                impactPoint);
            HealCaster(DamageResolver.Apply(victimStats, damageInfo));
            return;
        }

        IHittable hittable = impactPoint.GetComponentInParent<IHittable>();
        if (hittable != null)
        {
            NetworkObject attackerNetworkObject = caster != null
                ? caster.GetComponent<NetworkObject>()
                : null;
            hittable.OnHit(new HitInfo
            {
                amount = damage,
                hitPoint = impactPoint.ClosestPoint(transform.position),
                hitNormal = -transform.forward,
                attackerId = attackerNetworkObject != null ? attackerNetworkObject.OwnerClientId : 0,
                impactForce = transform.forward * damage
            });

            HealCaster(damage);
        }
    }

    private void HealCaster(float damageDone)
    {
        if (casterStats == null || !casterStats.IsServer || damageDone <= 0f)
            return;

        float drainRatio = DrainAmount > 1f ? DrainAmount / 100f : DrainAmount;
        healingRemainder += damageDone * Mathf.Clamp01(drainRatio);
        int healing = Mathf.FloorToInt(healingRemainder);
        if (healing <= 0)
            return;

        healingRemainder -= healing;
        caster?.AddHealth(healing);
    }
}
