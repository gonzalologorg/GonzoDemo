using NUnit.Framework;
using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerInventory : NetworkBehaviour
{
    private NetworkList<NetworkItem> networkInventory = new();
    private NetworkList<NetworkItem> networkEquipment = new();

    [SerializeField]
    public Dictionary<int, ItemInstance> inventory = new();
    [SerializeField]
    public Dictionary<int, ItemInstance> equipment = new();


    private bool inventoryDirty;

    private void Awake()
    {
        networkInventory = new NetworkList<NetworkItem>(
            null,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server
        );

        networkEquipment = new NetworkList<NetworkItem>(
            null,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );
    }

    public override void OnNetworkSpawn()
    {
        networkInventory.OnListChanged += OnInventoryChanged;
        networkEquipment.OnListChanged += OnEquipmentChanged;

        RebuildDictionary(networkInventory, inventory);
        RebuildDictionary(networkEquipment, equipment);
    }

    public override void OnNetworkDespawn()
    {
        networkInventory.OnListChanged -= OnInventoryChanged;
        networkEquipment.OnListChanged -= OnEquipmentChanged;
    }

    private void OnInventoryChanged(NetworkListEvent<NetworkItem> change)
    {
        RebuildDictionary(networkInventory, inventory);
    }

    private void OnEquipmentChanged(NetworkListEvent<NetworkItem> change)
    {
        RebuildDictionary(networkEquipment, equipment);
    }

    private void SetNetworkItem(NetworkList<NetworkItem> list, int slot, ItemInstance item)
    {
        if (!IsServer)
            return;

        var networkItem = new NetworkItem(
            slot,
            item.itemId,
            item.amount
        );

        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].Slot == slot)
            {
                list[i] = networkItem;
                return;
            }
        }

        list.Add(networkItem);
    }

    public void SetItem(ItemInstance item, bool equipped)
    {
        if (!IsServer)
            return;

        var netContainer = equipped ? networkEquipment : networkInventory;
        var container = equipped ? equipment : inventory;

        container[item.slot] = item;
        SetNetworkItem(netContainer, item.slot, item);
    }

    public void DownloadItems(int charId)
    {
        
    }

    public void AddItem(string itemId, int amount)
    {
        if (!IsServer)
            return;

        // Find empty spot
        int slot = 0;
        for (int i = 0; i < 20; i++)
        {
            if (!inventory.ContainsKey(i))
            {
                slot = i;
                break;
            }
        }

        SetItem(new ItemInstance(slot, itemId, amount), false);
    }

    int GetSlotIndex(NetworkList<NetworkItem> list, int slot)
    {
        int result = -1;
        for (int i = 0; list.Count > 0; i++)
        {
            if (list[i].Slot == slot)
            {
                result = i;
                break;
            }
        }

        return result;
    }

    public bool EquipItem(int from, int to, bool isEquip)
    {
        if (!IsServer) return false;
        // Get inventory and equipment saved
        var fromCon = isEquip ? inventory : equipment;
        var toCon = isEquip ? equipment : inventory;

        // Disallow if we are equiping an inexisting item or if we are equipping into a busy slot
        if (!fromCon.ContainsKey(from))
            return false;

        if (toCon.ContainsKey(from))
            return false;

        ItemInstance fromItem = fromCon[from];
        fromItem.slot = to;
        toCon[to] = fromItem;
        fromCon.Remove(from);

        // Remove from the start container and updates the target container
        var netContainer = isEquip ? networkInventory : networkEquipment;
        netContainer.RemoveAt(GetSlotIndex(netContainer, from));
        SetNetworkItem(isEquip ? networkEquipment : networkInventory, to, fromItem);

        return true;
    }

    public bool MoveItem(int from, int to)
    {
        if (!IsServer) return false;

        // We don't have a valid start item
        if (!inventory.ContainsKey(from))
            return false;

        ItemInstance fromItem = inventory[from];
        var clientRPC = GetOwnerRPC();

        // If we have a target item, then we copy from target into our principal slot (Switch
        if (inventory.ContainsKey(to))
        {
            inventory[from] = new(from, inventory[to].itemId, inventory[to].amount);
            SetNetworkItem(networkInventory, from, inventory[from]);
        }
        else
        {
            // If we had no item we remove the start slot and then we propagate the networking change
            inventory.Remove(from);
            networkInventory.RemoveAt(GetSlotIndex(networkInventory, from));
        }

        // Setup target slot with the fromItem and then we propagate the net message
        inventory[to] = fromItem;
        SetNetworkItem(networkInventory, to, inventory[to]);

        return true;
    }

    ClientRpcParams GetOwnerRPC()
    {
        // Base owner ClientRPC params for the owner
        return new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } } };
    }

    public bool RemoveItem(int slot, bool isEquipment)
    {
        if (!IsServer) return false;

        var netContainer = isEquipment ? networkEquipment : networkInventory;
        var container = isEquipment ? equipment : inventory;

        if (!container.ContainsKey(slot))
            return false;

        // Updates from local and server copy on list
        container.Remove(slot);
        netContainer.RemoveAt(GetSlotIndex(netContainer, slot));

        return true;
    }

    private void RebuildDictionary(NetworkList<NetworkItem> source, Dictionary<int, ItemInstance> target)
    {
        target.Clear();

        foreach (var entry in source)
        {
            target[entry.Slot] = new ItemInstance(entry.Slot, entry.ItemId.ToString(), entry.Amount);
        }
    }
}
