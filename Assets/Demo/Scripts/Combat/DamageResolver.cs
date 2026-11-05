using UnityEngine;

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

            float healthBefore = victim.GetStat(PlayerStats.StatType.Health);
            float resolvedDamage = damageInfo.Type switch
            {
                DamageType.Physical => victim.CalculateDamage(damageInfo.Damage, false),
                DamageType.Magical => victim.CalculateDamage(damageInfo.Damage, true),
                DamageType.Unmitigated => damageInfo.Damage,
                _ => 0f
            };

            if (float.IsNaN(resolvedDamage) || float.IsInfinity(resolvedDamage) || resolvedDamage <= 0f)
                return 0f;

            victim.SetStat(PlayerStats.StatType.Health, healthBefore - resolvedDamage);
            return healthBefore - victim.GetStat(PlayerStats.StatType.Health);
        }

        /// <summary>Routes projectile impacts through DamageInfo and this resolver.</summary>
        public static void BindProjectileImpact(
            ProjectileBase projectile,
            GameObject attacker,
            DamageType damageType = DamageType.Magical)
        {
            if (projectile == null)
                return;

            projectile.OnImpact += hitCollider =>
            {
                if (hitCollider == null)
                    return;

                PlayerStats victim = hitCollider.GetComponentInParent<PlayerStats>();
                if (victim == null)
                    return;

                Vector3 hitPosition = hitCollider.ClosestPoint(projectile.transform.position);
                var damageInfo = new DamageInfo(
                    projectile.Damage,
                    attacker,
                    projectile.gameObject,
                    damageType,
                    projectile.transform.forward * projectile.Damage,
                    hitPosition,
                    hitCollider);

                Apply(victim, damageInfo);
            };
        }
    }
}
