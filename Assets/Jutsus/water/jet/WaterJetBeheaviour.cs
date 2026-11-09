using System.Collections;
using GonzoDemo;
using Unity.Netcode;
using UnityEngine;

namespace GonzoDemo
{
    [CreateAssetMenu(menuName = "Jutsus/Behaviours/Water Jet")]
    public class WaterJetBehaviour : JutsuBehaviour
    {
        public GameObject WaterJetPrefab;

        public override float GetHoldDuration(JutsuContext ctx)
        {
            return Mathf.Max(0f, ctx.GetStat("duration"));
        }

        public override bool PreCast(JutsuContext ctx)
        {
            return WaterJetPrefab != null &&
                   WaterJetPrefab.GetComponent<NetworkObject>() != null &&
                   WaterJetPrefab.GetComponent<WaterJetBreath>() != null;
        }

        public override IEnumerator OnServerHoldCast(JutsuContext ctx, JutsuHoldSession session)
        {
            NetworkObject casterNetworkObject = ctx.Controller.GetComponent<NetworkObject>();
            PlayerController caster = ctx.Controller.GetComponent<PlayerController>();
            if (!ctx.Controller.IsServer || casterNetworkObject == null || !casterNetworkObject.IsSpawned ||
                caster == null || WaterJetPrefab == null)
                yield break;

            Quaternion aimRotation = Quaternion.Euler(ctx.GetCastAngle());
            GameObject breathObject = Instantiate(WaterJetPrefab, ctx.CastOrigin, aimRotation);
            WaterJetBreath breath = breathObject.GetComponent<WaterJetBreath>();
            NetworkObject breathNetworkObject = breathObject.GetComponent<NetworkObject>();
            if (breath == null || breathNetworkObject == null)
            {
                Debug.LogError("Water Jet prefab needs WaterJetBreath and NetworkObject on its root.", WaterJetPrefab);
                Destroy(breathObject);
                yield break;
            }

            breath.Initialize(
                caster,
                ctx.GetStat("damage"),
                ctx.GetStat("drain"),
                ctx.GetStat("distance"),
                ctx.GetStat("duration"));

            breathNetworkObject.SpawnWithOwnership(casterNetworkObject.OwnerClientId);
            breathNetworkObject.TrySetParent(casterNetworkObject, true);
            if (breathObject.transform.parent != caster.transform)
            {
                Debug.LogWarning("Water Jet could not parent to its caster; it will keep the initial origin.", breath);
            }

            // while (!session.IsComplete && breathNetworkObject.IsSpawned)
            //     yield return null;

            // if (breathNetworkObject.IsSpawned)
            //     breathNetworkObject.Despawn(true);
        }
    }
}
