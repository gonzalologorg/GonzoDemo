using GonzoDemo.AI;
using UnityEngine;
using Unity.Netcode;
using GonzoDemo.Combat;

namespace GonzoDemo
{
    /// <summary>Applies server-authoritative DamageInfo values to player stats.</summary>
    public static class DamageResolver
    {
        /// <returns>The amount of health actually removed, or zero if the hit was rejected.</returns>
        public static float Apply(PlayerStats victim, in DamageInfo damageInfo)
        {
            if (victim == null || !victim.IsSpawned || !victim.IsServer ||
                float.IsNaN(damageInfo.Damage) || float.IsInfinity(damageInfo.Damage) ||
                damageInfo.Damage <= 0f)
                return 0f;

            PlayerController victimController = victim.GetComponentInParent<PlayerController>();
            if (victimController == null)
                return 0f;

            int healthBefore = victimController.GetHealth();
            float resolvedDamage = damageInfo.Type switch
            {
                DamageType.Physical => victim.CalculateDamage(damageInfo.Damage, false),
                DamageType.Magical => victim.CalculateDamage(damageInfo.Damage, true),
                DamageType.Unmitigated => damageInfo.Damage,
                _ => 0f
            };

            if (float.IsNaN(resolvedDamage) || float.IsInfinity(resolvedDamage) || resolvedDamage <= 0f)
                return 0f;

            int damage = Mathf.Max(1, Mathf.CeilToInt(resolvedDamage));
            victimController.AddHealth(-damage);
            float removed = healthBefore - victimController.GetHealth();
            MinionCombat.ReportDamage(victim.gameObject, damageInfo.Attacker, removed);
            return removed;
        }

        /// <summary>Routes projectile hits to destructible targets or player stats.</summary>
        public static void BindProjectileImpact(
            ProjectileBase projectile,
            GameObject attacker,
            DamageType damageType = DamageType.Magical)
        {
            if (projectile == null)
                return;

            projectile.OnHit += hitCollider =>
            {
                if (hitCollider == null)
                    return;

                Vector3 hitPosition = hitCollider.ClosestPoint(projectile.transform.position);
                Vector3 impactForce = projectile.transform.forward * projectile.Damage;
                NetworkObject attackerNetworkObject = attacker != null
                    ? attacker.GetComponentInParent<NetworkObject>() ?? attacker.GetComponentInChildren<NetworkObject>()
                    : null;

                Minion minion = hitCollider.GetComponentInParent<Minion>();
                if (minion != null)
                {
                    minion.ReceiveDamage(new DamageInfo(projectile.Damage, attacker, projectile.gameObject,
                        damageType, impactForce, hitPosition, hitCollider));
                    return;
                }

                IHittable hittable = hitCollider.GetComponentInParent<IHittable>();
                if (hittable != null)
                {
                    hittable.OnHit(new HitInfo
                    {
                        amount = projectile.Damage,
                        hitPoint = hitPosition,
                        hitNormal = -projectile.transform.forward,
                        attackerId = attackerNetworkObject != null ? attackerNetworkObject.OwnerClientId : 0,
                        impactForce = impactForce
                    });
                    return;
                }

                PlayerStats victim = hitCollider.GetComponentInParent<PlayerStats>();
                if (victim == null)
                    return;

                var damageInfo = new DamageInfo(
                    projectile.Damage,
                    attacker,
                    projectile.gameObject,
                    damageType,
                    impactForce,
                    hitPosition,
                    hitCollider);

                Apply(victim, damageInfo);
            };
        }
    }
}
