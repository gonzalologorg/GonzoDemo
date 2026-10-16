using Unity.Collections;
using Unity.Netcode;
using Unity.Services.Matchmaker.Models;
using UnityEngine;

public class ItemSack : NetworkBehaviour
{
    public int Amount;
    public string ItemId;
    GameObject icon;

    void Start()
    {
        SpriteRenderer spriteRenderer = transform.Find("Square").GetComponent<SpriteRenderer>();
        icon = transform.Find("Square").gameObject;
        GlobalManager gmInstance = GlobalManager.Instance;
        ItemDefinition itemDefinition = gmInstance.itemDatabase.Get(ItemId.ToString());
        if (itemDefinition == null) return;
        spriteRenderer.sprite = itemDefinition.Icon;
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log(
            $"Trigger: {other.name} | " +
            $"IsServer: {IsServer} | " +
            $"IsSpawned: {IsSpawned}"
        );
        if (!IsServer) return;

        if (!other.TryGetComponent<PlayerInventory>(
                out var playerInventory))
            return;

        playerInventory.AddItem(ItemId, Amount);
        NetworkObject.Despawn();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        icon.transform.eulerAngles = Camera.main.transform.eulerAngles;
    }
}
