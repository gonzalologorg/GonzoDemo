using System.Collections;
using GonzoDemo;
using Unity.Netcode;
using UnityEngine;

[CreateAssetMenu(menuName = "Jutsus/Behaviours/Electric Chain")]
public sealed class ElectricChainBehaviour : JutsuBehaviour
{
    [SerializeField] private GameObject chainPrefab;

    public override bool PreCast(JutsuContext ctx)
    {
        return chainPrefab != null &&
               chainPrefab.GetComponent<NetworkObject>() != null &&
               chainPrefab.GetComponent<ElectricChain>() != null;
    }

    public override IEnumerator OnServerCast(JutsuContext ctx)
    {
        if (ctx?.Controller == null || !ctx.Controller.IsServer || chainPrefab == null)
            yield break;

        NetworkObject caster = ctx.Controller.GetComponent<NetworkObject>();
        if (caster == null || !caster.IsSpawned)
            yield break;

        GameObject chainObject = Instantiate(chainPrefab, ctx.CastOrigin, Quaternion.identity);
        ElectricChain chain = chainObject.GetComponent<ElectricChain>();
        NetworkObject chainNetworkObject = chainObject.GetComponent<NetworkObject>();
        if (chain == null || chainNetworkObject == null)
        {
            Debug.LogError("Electric Chain prefab needs ElectricChain and NetworkObject on its root.", chainPrefab);
            Destroy(chainObject);
            yield break;
        }

        chain.Initialize(
            caster,
            ctx.GetStat("damage"),
            Mathf.RoundToInt(ctx.GetStat("jumps")),
            ctx.GetStat("slow"),
            ctx.GetStat("duration"));
        chainNetworkObject.Spawn();
    }
}
