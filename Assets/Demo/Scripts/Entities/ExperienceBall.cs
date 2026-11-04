using UnityEngine;
using Unity.Netcode;
[RequireComponent(typeof(NetworkObject))]
public class ExperienceBall : NetworkBehaviour
{
    [Min(1)] public int Amount = 10;
    private bool consumed;

    public override void OnNetworkSpawn()
    {
        consumed = false;
    }

    public override void OnNetworkDespawn()
    {
        gameObject.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || !IsSpawned || consumed || Amount <= 0) return;

        // The player's collider may be on a child object.
        var playerStats = other.GetComponentInParent<GonzoDemo.PlayerStats>();
        if (playerStats == null || !playerStats.IsSpawned) return;

        consumed = true;
        playerStats.AddExperience(Amount);
        NetworkObject.Despawn(false);
        gameObject.SetActive(false);
    }
}
