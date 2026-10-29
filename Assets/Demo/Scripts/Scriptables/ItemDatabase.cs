using System.Collections.Generic;
using UnityEngine;
namespace GonzoDemo
{
    [CreateAssetMenu(fileName = "ItemDatabase", menuName = "ScriptableObjects/ItemDatabase", order = 1)]
    public class ItemDatabase : ScriptableObject
    {
        [SerializeField]
        private ItemDefinition[] items;
        private Dictionary<string, ItemDefinition> lookup;

        public void Initialize()
        {
            lookup = new Dictionary<string, ItemDefinition>();
            foreach (ItemDefinition newItem in items)
            {
                if (!lookup.TryAdd(newItem.Id, newItem))
                {
                    Debug.LogWarning($"Duplicate item ID {newItem.Id} found in ItemDatabase. Item '{newItem.Name}' will be ignored.");
                }
            }
        }

        public ItemDefinition Get(string id)
        {
            if (lookup == null)
                Initialize();

            return lookup.GetValueOrDefault(id);
        }
    }
}