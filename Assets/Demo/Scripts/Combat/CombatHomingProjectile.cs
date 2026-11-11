using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace GonzoDemo
{
    [RequireComponent(typeof(NetworkObject), typeof(NetworkTransform))]
    public class CombatHomingProjectile : NetworkBehaviour
    {
        private NetworkObject attacker;
        private NetworkObject target;
        private float damage;
        private float radius;
        private float speed;
        private LayerMask hitLayers;
        private bool initialized;

        public void Initialize(
            NetworkObject attackerObject,
            NetworkObject targetObject,
            float damageAmount,
            float areaRadius,
            float travelSpeed,
            LayerMask layers)
        {
            attacker = attackerObject;
            target = targetObject;
            damage = Mathf.Max(0f, damageAmount);
            radius = Mathf.Max(0f, areaRadius);
            speed = Mathf.Max(0.1f, travelSpeed);
            hitLayers = layers;
            initialized = true;
        }

        private void Update()
        {
            if (!IsServer || !initialized)
                return;

            if (attacker == null || !attacker.IsSpawned || target == null || !target.IsSpawned)
            {
                NetworkObject.Despawn(true);
                return;
            }

            Vector3 destination = GetTargetPoint(target);
            Vector3 toTarget = destination - transform.position;
            float step = speed * Time.deltaTime;
            if (toTarget.sqrMagnitude <= step * step || toTarget.sqrMagnitude < 0.0025f)
            {
                transform.position = destination;
                ApplyImpact(destination, toTarget.sqrMagnitude > 0f ? toTarget.normalized : transform.forward);
                NetworkObject.Despawn(true);
                return;
            }

            Vector3 direction = toTarget.normalized;
            transform.SetPositionAndRotation(
                transform.position + direction * step,
                Quaternion.LookRotation(direction, Vector3.up));
        }

        private void ApplyImpact(Vector3 point, Vector3 direction)
        {
            if (radius <= 0f)
            {
                Collider targetCollider = FindTargetCollider(target);
                PlayerCombat.ApplyDamageToTarget(target, targetCollider, attacker.gameObject,
                    gameObject, damage, direction, point);
                return;
            }

            HashSet<ulong> hitIds = new HashSet<ulong>();
            Collider[] overlaps = Physics.OverlapSphere(point, radius, hitLayers, QueryTriggerInteraction.Ignore);
            foreach (Collider overlap in overlaps)
            {
                NetworkObject victim = overlap.GetComponentInParent<NetworkObject>();
                if (victim == null || !victim.IsSpawned || victim == attacker || !hitIds.Add(victim.NetworkObjectId))
                    continue;

                PlayerCombat.ApplyDamageToTarget(victim, overlap, attacker.gameObject,
                    gameObject, damage, direction, overlap.ClosestPoint(point));
            }
        }

        private static Vector3 GetTargetPoint(NetworkObject targetObject)
        {
            Collider collider = FindTargetCollider(targetObject);
            return collider != null ? collider.bounds.center : targetObject.transform.position;
        }

        private static Collider FindTargetCollider(NetworkObject targetObject)
        {
            if (targetObject == null)
                return null;
            Collider collider = targetObject.GetComponent<Collider>();
            return collider != null ? collider : targetObject.GetComponentInChildren<Collider>();
        }
    }
}
