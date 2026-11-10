using UnityEngine;

namespace GonzoDemo.AI
{
    public class MinionRangedAttack : MinionAttack
    {
        public MinionHomingProjectile ProjectilePrefab;
        public Transform Muzzle;
        [Min(0.1f)] public float ProjectileSpeed = 12;
        // Reserve until the projectile's maximum lifetime so chasing a moving target cannot
        // make this impact coincide with a subsequent minion's attack.
        protected override float TravelTime => ProjectilePrefab != null ? ProjectilePrefab.Lifetime : 0;
        protected override void PerformAttack(GameObject target)
        {
            if (ProjectilePrefab == null) return;
            var projectile = Instantiate(ProjectilePrefab, Muzzle != null ? Muzzle.position : transform.position + Vector3.up, Quaternion.identity);
            projectile.Initialize(Unit, target, Damage, ProjectileSpeed);
            projectile.NetworkObject.Spawn();
        }
    }
}
