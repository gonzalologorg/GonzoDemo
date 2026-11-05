using UnityEngine;
using Unity.Netcode;
[RequireComponent(typeof(NetworkObject))]
public class AbilityBall : NetworkBehaviour
{
    [Min(1)] public int Level = 1;
    public GonzoDemo.JutsuDefinition Jutsu;
    private bool consumed;
    private SpriteRenderer spriteRenderer;

    private void OnValidate()
    {
        RefreshJutsuIcon();
    }

    public override void OnNetworkSpawn()
    {
        RefreshJutsuIcon();
        consumed = false;
        GonzoDemo.JutsuController.RegisterAvailableJutsu(Jutsu);
    }

    public override void OnNetworkDespawn()
    {
        gameObject.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || !IsSpawned || consumed || Level <= 0) return;

        // The player's collider may be on a child object.
        var jutsuController = other.GetComponentInParent<GonzoDemo.JutsuController>();
        if (jutsuController == null || !jutsuController.IsSpawned || Jutsu == null) return;
        if (!jutsuController.TryAcquireJutsu(Jutsu, Level, out _)) return;

        consumed = true;
        NetworkObject.Despawn(false);
        gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);

        Camera mainCamera = Camera.main;
        if (spriteRenderer == null || mainCamera == null)
            return;

        // Keep the icon centered on the orb and facing the active main camera.
        spriteRenderer.transform.rotation = mainCamera.transform.rotation;
    }

    private void RefreshJutsuIcon()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);

        if (spriteRenderer == null)
            return;

        spriteRenderer.transform.localPosition = Vector3.zero;
        spriteRenderer.sprite = Jutsu != null ? Jutsu.JutsuIcon : null;
    }
}
