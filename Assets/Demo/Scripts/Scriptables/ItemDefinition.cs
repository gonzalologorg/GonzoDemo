using System.ComponentModel;
using UnityEngine;
using System.Collections.Generic;
namespace GonzoDemo
{
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

		public enum ItemRarity
		{
			Common,
			Uncommon,
			Rare,
			Epic,
			Legendary,
		}

		[SerializeField] private string id;
		[SerializeField] private ItemRarity rarity;
		[SerializeField] private string itemName;
		[SerializeField] private string description;
		[SerializeField] private Sprite icon;
		[SerializeField] private ItemType type;
		[SerializeField] private Dictionary<string, float> stats;

		public string Id => id;
		public ItemRarity Rarity => rarity;
		public string Name => itemName;
		public string Description => description;
		public Sprite Icon => icon;
		public ItemType Type => type;
		public Dictionary<string, float> Stats => stats;
	}
}