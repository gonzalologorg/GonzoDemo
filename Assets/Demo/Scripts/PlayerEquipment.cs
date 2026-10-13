using UnityEngine;
using System.Collections.Generic;
using System;

public class PlayerEquipment : MonoBehaviour
{
    [SerializeField]
    private ItemDatabase database;
    [NonSerialized]
    public Dictionary<int, ItemInstance> Backpack;

    void Awake()
	{
		Backpack = new Dictionary<int, ItemInstance>();
	}

    public void SetupInventory(Dictionary<int, ItemInstance> backpack)
	{
		Backpack = backpack;
	}
}
