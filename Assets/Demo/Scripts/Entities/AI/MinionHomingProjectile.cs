using GonzoDemo;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace GonzoDemo.AI
{
    [RequireComponent(typeof(NetworkObject), typeof(NetworkTransform))]
    public class MinionHomingProjectile : NetworkBehaviour
    {
        [Min(0.1f)] public float Lifetime = 5;
        [Min(0.05f)] public float HitRadius = 0.3f;
        public DamageType DamageType = DamageType.Magical;
        private Minion source;
        private GameObject target;
        private float damage, speed, expires;
        public void Initialize(Minion attacker, GameObject victim, float amount, float movementSpeed)
        {
            source = attacker;
            target = victim;
            damage = amount;
            speed = Mathf.Max(0.1f, movementSpeed);
        }
        public override void OnNetworkSpawn() => expires = Time.time + Lifetime;
        private void Update()
        {
            if (!IsSpawned || !IsServer) return;
            if (!MinionCombat.IsAlive(target) || Time.time >= expires) { NetworkObject.Despawn(); return; }
            Vector3 point = target.transform.position + Vector3.up * 0.5f;
            Vector3 direction = point - transform.position;
            if (direction.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(direction);
            transform.position = Vector3.MoveTowards(transform.position, point, speed * Time.deltaTime);
            if (Vector3.Distance(transform.position, point) > HitRadius) return;
            if (source == null || !MinionCombat.Friendly(source, target))
                MinionCombat.Apply(target, source != null ? source.gameObject : null, damage, DamageType);
            NetworkObject.Despawn();
        }
    }
}
