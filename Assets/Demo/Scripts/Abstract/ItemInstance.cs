[System.Serializable]
public class ItemInstance
{
	public string itemId;
	public int amount;
	public int slot;

	public ItemInstance(int slot, string itemId, int amount)
	{
		this.slot = slot;
		this.itemId = itemId;
		this.amount = amount;
	}
}
