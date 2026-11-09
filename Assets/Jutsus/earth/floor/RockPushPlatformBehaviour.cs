using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace GonzoDemo
{
    [CreateAssetMenu(menuName = "Jutsus/Behaviours/Rock Push Platform")]
    public class RockPushPlatformBehaviour : JutsuBehaviour
    {
        public GameObject PlatformPrefab;

        public override IEnumerator OnServerCast(JutsuContext ctx)
        {
            if (PlatformPrefab == null)
            {
                Debug.LogError("Rock Push has no platform prefab assigned.", this);
                yield break;
            }

            if (!ctx.CastEmptyGround(5f, out Vector3 targetPosition))
            {
                Debug.LogWarning("Rock Push could not find a ground position to spawn on.", ctx.Controller);
                yield break;
            }

            Vector3 flatAimDirection = ctx.GetFlatAimDirection();
            Quaternion platformRotation = Quaternion.LookRotation(flatAimDirection, Vector3.up);
            GameObject platform = Instantiate(PlatformPrefab, targetPosition, platformRotation);

            RockPushPlatform rockPush = platform.GetComponent<RockPushPlatform>();
            NetworkObject networkObject = platform.GetComponent<NetworkObject>();
            if (rockPush == null)
            {
                Debug.LogError("Rock Push prefab needs a RockPushPlatform component.", PlatformPrefab);
                Destroy(platform);
                yield break;
            }

            if (networkObject == null)
            {
                Debug.LogError("Rock Push prefab needs a NetworkObject component.", PlatformPrefab);
                Destroy(platform);
                yield break;
            }

            // if (platform.TryGetComponent<Collider>(out Collider platformCollider))
            // {
            //     float colliderBottomOffset = platformCollider.bounds.min.y - platform.transform.position.y;
            //     platform.transform.position += Vector3.down * colliderBottomOffset;
            // }

            rockPush.Damage = Mathf.Max(0, Mathf.RoundToInt(ctx.GetStat("damage")));
            rockPush.PushForce = Mathf.Max(0f, ctx.GetStat("pushforce"));
            rockPush.SetCaster(ctx.Controller.OwnerClientId);

            networkObject.Spawn();
            yield break;
        }
    }
}
