using System.ComponentModel;
using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(
	fileName = "Item",
	menuName = "Content/Items/Item",
	order = 1
)]
public abstract class ItemDefinition : ScriptableObject
{

	public enum ItemType
	{
		Head,
		Body,
		Weapon,
		Consumable,
		Misc,
	}

	[SerializeField] private int id;
	[SerializeField] private string itemName;
	[SerializeField] private string description;
	[SerializeField] private Sprite icon;
	[SerializeField] private ItemType type;
	[SerializeField] private Dictionary<string, float> stats;

	public int Id => id;
	public string Name => itemName;
	public string Description => description;
	public Sprite Icon => icon;
	public ItemType Type => type;
	public Dictionary<string, float> Stats => stats;
}
