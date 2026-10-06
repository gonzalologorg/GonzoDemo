using UnityEngine;
using System;
[Serializable]
public class PlayerData
{
	[Serializable]
	public class PlayerInfo
	{
		public int id;
		public string username;
		public string email;
		public string creation_date;
		public int premium_currency;
	}

	[Serializable]
	public class ItemContainer
	{
		public int id;
		public int amount;
		public bool isUnique;
	}

	[Serializable]
	public class CharacterInfo
	{
		public int id;
		public int player_id;
		public string name;
		// The API serializes both values as JSON arrays.
		public ItemContainer[] inventory;
		public ItemContainer[] equipment;
		public int side;
		public int portrait;
	}

	// JsonUtility only reads fields; it ignores C# properties such as
	// `player { get; set; }` and `characters { get; set; }`.
	public PlayerInfo player;
	public CharacterInfo[] characters;

}
