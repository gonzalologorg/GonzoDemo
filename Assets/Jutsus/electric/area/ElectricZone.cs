using System.Collections;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class ElectricZone : NetworkBehaviour
{
    private readonly NetworkVariable<float> scaleProgress = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    [Min(0f)] public float DamagePerSecond;
    [Min(0f)] public float SpeedReduction;
    [Min(0f)] public float Duration = 5f;

    private Vector3 fullScale;
    private NetworkObject casterNetworkObject;
    private Coroutine lifetimeRoutine;

    private void Awake()
    {
        fullScale = transform.localScale;

        // The prefab collider is only for mesh editing; the zone is visual for now.
        foreach (Collider zoneCollider in GetComponentsInChildren<Collider>(true))
            zoneCollider.enabled = false;
    }

    public void Initialize(NetworkObject caster, float damagePerSecond, float speedReduction, float duration)
    {
        casterNetworkObject = caster;
        DamagePerSecond = Mathf.Max(0f, damagePerSecond);
        SpeedReduction = Mathf.Max(0f, speedReduction);
        Duration = Mathf.Max(0f, duration);
    }

    public override void OnNetworkSpawn()
    {
        ApplyScale();

        if (IsServer)
        {
            if (casterNetworkObject != null && casterNetworkObject.IsSpawned)
                NetworkObject.TrySetParent(casterNetworkObject, true);

            lifetimeRoutine = StartCoroutine(RunLifetime());
        }
    }

    public override void OnNetworkDespawn()
    {
        if (lifetimeRoutine != null)
        {
            StopCoroutine(lifetimeRoutine);
            lifetimeRoutine = null;
        }
    }

    private void Update()
    {
        ApplyScale();
    }

    private void FixedUpdate()
    {
        transform.Rotate(Vector3.up, 90f * Time.fixedDeltaTime);
    }

    private IEnumerator RunLifetime()
    {
        yield return AnimateScale(0f, 1f, 1f);

        float elapsed = 0f;
        while (elapsed < Duration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        yield return AnimateScale(1f, 0f, 1f);

        if (NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn(true);
    }

    private IEnumerator AnimateScale(float start, float end, float duration)
    {
        float elapsed = 0f;
        scaleProgress.Value = start;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime * 2;
            scaleProgress.Value = Mathf.Lerp(start, end, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        scaleProgress.Value = end;
    }

    private void ApplyScale()
    {
        Vector3 scale = fullScale * Mathf.Clamp01(scaleProgress.Value);
        transform.localScale = scale;
    }
}
