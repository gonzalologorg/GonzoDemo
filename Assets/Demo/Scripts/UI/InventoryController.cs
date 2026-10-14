using UnityEngine;

public class InventoryController : MonoBehaviour
{
    public RectTransform InventorySlots;
    public GameObject InventorySlotPrefab;
	public ItemDatabase ItemDatabase;

	public PlayerEquipment inventoryBox;
    public PlayerEquipment equipmentBox;

	void Start()
    {
		ItemDatabase = Resources.Load<ItemDatabase>("Data/ItemDatabase");
		for (int i = 0; i < 20; i++)
		{
			GameObject slot = Instantiate(InventorySlotPrefab, InventorySlots);
			slot.name = "InventorySlot_" + i;
			ItemControl item = slot.GetComponent<ItemControl>();

            if (inventoryBox != null && inventoryBox.Backpack.ContainsKey(i))
			{
				ItemInstance itemInstance = inventoryBox.Backpack[i];
				ItemDefinition itemDefinition = ItemDatabase.Get(itemInstance.itemId);
				item.Item = itemDefinition;
				item.Amount = itemInstance.amount;
				item.SetupControl();
			}
			else
			{
				item.Item = null;
				item.Amount = 0;
				item.SetupControl();
			}
		}
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
