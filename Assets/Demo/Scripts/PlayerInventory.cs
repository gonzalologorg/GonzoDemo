using NUnit.Framework;
using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
namespace GonzoDemo
{
    public class PlayerInventory : NetworkBehaviour
    {
        private NetworkList<NetworkItem> networkInventory = new();
        private NetworkList<NetworkItem> networkEquipment = new();

        [SerializeField]
        public Dictionary<int, ItemInstance> inventory = new();
        [SerializeField]
        public Dictionary<int, ItemInstance> equipment = new();

        public event Action OnInventoryUpdated;
        public event Action OnEquipmentUpdated;
        public ItemDatabase itemDatabase;


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
            if (IsServer || IsOwner)
            {
                networkInventory.OnListChanged += OnInventoryChanged;
                networkEquipment.OnListChanged += OnEquipmentChanged;
            }

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

            if (IsClient && IsOwner)
                OnInventoryUpdated?.Invoke();
        }

        private void OnEquipmentChanged(NetworkListEvent<NetworkItem> change)
        {
            RebuildDictionary(networkEquipment, equipment);

            if (IsClient && IsOwner)
                OnEquipmentUpdated?.Invoke();
        }

        void Start()
        {
            itemDatabase = GlobalManager.Instance.itemDatabase;
            if (IsClient && IsOwner)
            {
                GameObject InventoryPanel = (GameObject)Instantiate(Resources.Load("UI/Panels/Inventory"), GameObject.Find("WorldCanvas").transform);
                InventoryPanel.GetComponent<InventoryController>().playerInventory = this;
                InventoryPanel.GetComponent<InventoryController>().StartPanel();
            }
        }

        private void SetNetworkItem(NetworkList<NetworkItem> list, int slot, ItemInstance item)
        {
            if (!IsServer)
                return;

            if (item == null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].Slot == slot)
                    {
                        list.RemoveAt(i);
                        return;
                    }
                }
                return;
            }

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
            int slot = -1;
            for (int i = 0; i < 20; i++)
            {
                if (inventory.ContainsKey(i) && inventory[i].itemId == itemId)
                {
                    inventory[i].amount += amount;
                    SetNetworkItem(networkInventory, i, inventory[i]);
                    return;
                }
            }
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

            var fromCon = isEquip ? inventory : equipment;
            var toCon = isEquip ? equipment : inventory;
            var fromNetCon = isEquip ? networkInventory : networkEquipment;
            var toNetCon = isEquip ? networkEquipment : networkInventory;

            // Cancels interaction onto a busy slot or if the source slot is empty
            if (!fromCon.ContainsKey(from) || toCon.ContainsKey(to))
                return false;

            ItemInstance fromItem = new ItemInstance(to, fromCon[from].itemId, fromCon[from].amount);

            if (fromItem.amount == 1)
            {
                // If we only have one item, we just move it to the target slot and remove it from the source slot
                toCon[to] = fromItem;
                fromCon.Remove(from);

                fromNetCon.RemoveAt(GetSlotIndex(fromNetCon, from));
                SetNetworkItem(toNetCon, to, fromItem);
            }
            else
            {
                // Decrease count item by 1 and then we update the networking item
                toCon[to] = new ItemInstance(to, fromItem.itemId, 1);
                fromItem.amount -= 1;
                fromCon[from] = fromItem;

                for (int i = 0; i < fromNetCon.Count; i++)
                {
                    if (fromNetCon[i].Slot == from)
                    {
                        NetworkItem entry = fromNetCon[i];
                        entry.Amount = fromItem.amount;
                        fromNetCon[i] = entry;
                        break;
                    }
                }

                SetNetworkItem(toNetCon, to, toCon[to]);
            }

            ItemDefinition itemDefinition = itemDatabase.Get(fromItem.itemId);
            if (itemDefinition != null)
            {
                PlayerStats stats = GetComponent<PlayerStats>();
                if (stats != null)
                {
                    foreach (var stat in itemDefinition.Stats)
                    {
                        // Updates player stats based on the item being equipped or unequipped
                        if (Enum.TryParse(stat.Key, out PlayerStats.StatType statType))
                        {
                            float currentStatValue = stats.GetStat(statType);
                            // Add stats if we are equipping, substract if are not
                            float newStatValue = isEquip
                                ? currentStatValue + stat.Value
                                : currentStatValue - stat.Value;

                            stats.SetStat(statType, newStatValue);
                        }
                    }
                }
            }

            return true;
        }

        public bool MoveItem(int from, int to)
        {
            if (!IsServer) return false;

            // We don't have a valid start item
            if (!inventory.ContainsKey(from))
                return false;

            ItemInstance fromItem = inventory[from];

            // If we have a target item, then we copy from target into our principal slot (Switch
            if (inventory.ContainsKey(to))
            {
                ItemInstance targetSlot = inventory[to];
                if (targetSlot.itemId == fromItem.itemId)
                {
                    inventory[to].amount += fromItem.amount;
                    inventory[from].amount -= fromItem.amount;
                    inventory.Remove(from);

                    SetNetworkItem(networkInventory, to, inventory[to]);
                    SetNetworkItem(networkInventory, from, null);

                    return true;
                }
                else
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


        private Dictionary<ItemDefinition.ItemType, int> itemPairs = new(){
        { ItemDefinition.ItemType.Head, 21 },
        { ItemDefinition.ItemType.Body, 22 },
        { ItemDefinition.ItemType.Weapon, 23 },
        { ItemDefinition.ItemType.Misc, 24 },
        { ItemDefinition.ItemType.Consumable, 25 },
    };

        [Rpc(SendTo.Server)]
        public void RequestMoveItemRpc(int from, int to, bool IsEquipment, bool IsTargetEquipment, RpcParams rpcParams = default)
        {
            Debug.Log($"RequestMoveItemRpc called with from: {from}, to: {to}, IsEquipment: {IsEquipment}, IsTargetEquipment: {IsTargetEquipment}" +
                $"IsOwner: {IsOwner}, IsServer: {IsServer}, OwnerClientId: {OwnerClientId}, IsClient: {IsClient}");

            ulong senderClientId = rpcParams.Receive.SenderClientId;

            if (senderClientId != OwnerClientId)
                return;

            if (!IsEquipment && !IsTargetEquipment)
            {
                MoveItem(from, to);
                return;
            }

            if (!IsTargetEquipment)
            {
                // If we are moving from equipment to inventory, we check if the target slot is empty
                ItemInstance targetItemSlot = inventory.ContainsKey(to) ? inventory[to] : null;
                if (targetItemSlot != null) return;

                EquipItem(from, to, false);
            }
            else
            {
                // Check if the target slot is empty and if the item type matches the equipment slot or if item definition is invalid
                ItemInstance targetItemSlot = equipment.ContainsKey(to) ? equipment[to] : null;
                if (targetItemSlot != null) return;
                ItemDefinition itemDefinition = itemDatabase.Get(inventory[from].itemId);
                if (itemDefinition == null) return;
                // Reject wrong slot x item definition
                if (itemPairs.ContainsKey(itemDefinition.Type) && itemPairs[itemDefinition.Type] != to) return;

                EquipItem(from, to, true);
            }
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
                // Rebuilds the target dictionary from the source network list
                target[entry.Slot] = new ItemInstance(entry.Slot, entry.ItemId.ToString(), entry.Amount);
            }
        }
    }
}