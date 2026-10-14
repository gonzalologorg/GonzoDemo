using UnityEngine;
using System.Collections.Generic;
using System;

public class PlayerEquipment : MonoBehaviour
{
	[SerializeField]
	private ItemDatabase database;
	public bool IsEquipment;

	[NonSerialized]
	public Dictionary<int, ItemInstance> Backpack;
	public event Action<PlayerEquipment> OnInventoryChanged;

	void Awake()
	{
		Backpack = new Dictionary<int, ItemInstance>();
	}

	public void SetupInventory(Dictionary<int, ItemInstance> backpack)
	{
		Backpack = backpack;
	}

	public bool AddItem(int slot, string itemId, int amount)
	{
		if (Backpack.ContainsKey(slot))
		{
			Debug.LogWarning($"Slot {slot} is already occupied.");
			return false;
		}

		ItemInstance newItem = new ItemInstance(itemId, amount);
		Backpack[slot] = newItem;

		OnInventoryChanged?.Invoke(this);
		return true;
	}

	public void MoveItem(int slotFrom, int slotTo)
	{
		ItemInstance oldItem = Backpack[slotFrom];
		ItemInstance newItem = Backpack[slotTo];

		Backpack[slotTo] = oldItem;
		Backpack[slotFrom] = newItem;

		OnInventoryChanged?.Invoke(this);
	}

	public bool RemoveItem(int slot)
	{
		if (!Backpack.ContainsKey(slot))
		{
			Debug.LogWarning($"Slot {slot} is already empty.");
			return false;
		}

		Backpack.Remove(slot);
		OnInventoryChanged?.Invoke(this);
		return true;
	}
}
