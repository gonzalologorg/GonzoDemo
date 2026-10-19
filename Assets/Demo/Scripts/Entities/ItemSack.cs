using Unity.Collections;
using Unity.Netcode;
using Unity.Services.Matchmaker.Models;
using UnityEngine;

public class ItemSack : NetworkBehaviour
{
    public int Amount;
    public string ItemId;
    public bool consumed = false;
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

    public override void OnNetworkDespawn()
    {
        gameObject.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || consumed) return;

        if (!other.TryGetComponent<PlayerInventory>(
                out var playerInventory))
            return;

        consumed = true;
        NetworkObject.Despawn(false);
        gameObject.SetActive(false);

        playerInventory.AddItem(ItemId, Amount);
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        icon.transform.eulerAngles = Camera.main.transform.eulerAngles;
    }
}
