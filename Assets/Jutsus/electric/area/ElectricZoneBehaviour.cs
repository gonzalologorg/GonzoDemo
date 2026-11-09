using System.Collections;
using GonzoDemo;
using Unity.Netcode;
using UnityEngine;

namespace GonzoDemo
{
    [CreateAssetMenu(menuName = "Jutsus/Behaviours/Electric Zone")]
    public class ElectricZoneBehaviour : JutsuBehaviour
    {
        public GameObject ZonePrefab;

        public override bool PreCast(JutsuContext ctx)
        {
            return ZonePrefab != null &&
                   ZonePrefab.GetComponent<NetworkObject>() != null &&
                   ZonePrefab.GetComponent<ElectricZone>() != null;
        }

        public override IEnumerator OnServerCast(JutsuContext ctx)
        {
            NetworkObject caster = ctx.Controller.GetComponent<NetworkObject>();
            if (!ctx.Controller.IsServer || caster == null || !caster.IsSpawned || ZonePrefab == null)
                yield break;

            GameObject zoneObject = Instantiate(
                ZonePrefab,
                ctx.Controller.transform.position,
                ctx.Controller.transform.rotation);

            ElectricZone zone = zoneObject.GetComponent<ElectricZone>();
            NetworkObject zoneNetworkObject = zoneObject.GetComponent<NetworkObject>();
            if (zone == null || zoneNetworkObject == null)
            {
                Debug.LogError("Electric Zone prefab needs ElectricZone and NetworkObject on its root.", ZonePrefab);
                Destroy(zoneObject);
                yield break;
            }

            zone.Initialize(
                caster,
                ctx.GetStat("damage"),
                ctx.GetStat("speed"),
                ctx.GetStat("duration"));
            zoneNetworkObject.Spawn();
        }
    }
}
