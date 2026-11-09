using System.Collections;
using System;
using Unity.Netcode;
using UnityEngine;

namespace GonzoDemo
{
    [CreateAssetMenu(menuName = "Jutsus/Behaviours/Bubble Prison")]
    public class BubbleJutsuBehaviour : JutsuBehaviour
    {
        public GameObject BubblePrefab;
        [Min(0f)] public float TargetRange = 15f;
        [Min(0f)] public float MissSpawnDistance = 4f;

        public override IEnumerator OnServerCast(JutsuContext ctx)
        {
            if (BubblePrefab == null)
            {
                Debug.LogError("Bubble Prison has no prefab assigned.", this);
                yield break;
            }

            NetworkObject caster = ctx.Controller.GetComponentInParent<NetworkObject>();
            if (caster == null || !caster.IsSpawned)
            {
                Debug.LogError("Bubble Prison caster needs a spawned NetworkObject.", ctx.Controller);
                yield break;
            }

            BubbleContainer bubbleTemplate = BubblePrefab.GetComponent<BubbleContainer>();
            NetworkObject bubbleNetworkPrefab = BubblePrefab.GetComponent<NetworkObject>();
            if (bubbleTemplate == null || bubbleNetworkPrefab == null)
            {
                Debug.LogError("Bubble Prison prefab needs BubbleContainer and NetworkObject components.", BubblePrefab);
                yield break;
            }

            Vector3 aimDirection = Quaternion.Euler(ctx.GetCastAngle()) * Vector3.forward;
            if (aimDirection.sqrMagnitude < 0.0001f)
            {
                aimDirection = ctx.GetFlatAimDirection();
            }
            aimDirection.Normalize();

            GameObject target = FindTarget(ctx.CastOrigin, aimDirection, caster);
            Vector3 spawnPosition = target != null
                ? GetTargetCenter(target)
                : ctx.CastOrigin + aimDirection * Mathf.Max(0f, MissSpawnDistance);

            GameObject bubbleObject = Instantiate(BubblePrefab, spawnPosition, BubblePrefab.transform.rotation);
            BubbleContainer bubble = bubbleObject.GetComponent<BubbleContainer>();
            NetworkObject networkObject = bubbleObject.GetComponent<NetworkObject>();
            PlayerStats casterStats = ctx.Controller.GetComponentInParent<PlayerStats>();
            bubble.Initialize(
                caster,
                casterStats,
                ctx.GetStat("duration"),
                ctx.GetStat("draining"),
                target);
            networkObject.Spawn();
            yield break;
        }

        private GameObject FindTarget(Vector3 origin, Vector3 direction, NetworkObject caster)
        {
            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                direction,
                Mathf.Max(0f, TargetRange),
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null)
                {
                    continue;
                }

                GameObject target = BubbleContainer.FindCaptureTarget(hit.collider, caster);
                if (target != null)
                {
                    return target;
                }

                if (!hit.collider.isTrigger)
                {
                    break;
                }
            }

            return null;
        }

        private static Vector3 GetTargetCenter(GameObject target)
        {
            Collider[] colliders = target.GetComponentsInChildren<Collider>();
            if (colliders.Length > 0)
            {
                Bounds bounds = colliders[0].bounds;
                for (int i = 1; i < colliders.Length; i++)
                {
                    bounds.Encapsulate(colliders[i].bounds);
                }

                return bounds.center;
            }

            return target.transform.position + Vector3.up;
        }
    }
}
