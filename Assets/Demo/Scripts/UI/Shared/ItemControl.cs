using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class ItemControl : MonoBehaviour
{
    public Sprite graphicHover;
    public Sprite graphicNormal;
    public Sprite graphicEmpty;

    public ItemDefinition Item;
    public Image IconObject;
    public TMP_Text LabelObject;
	public int Amount;

	public GameObject gradient;

	Dictionary<ItemDefinition.ItemRarity, string> RarityColors = new Dictionary<ItemDefinition.ItemRarity, string>
	{
		{ ItemDefinition.ItemRarity.Common, "5E98D9" },
		{ ItemDefinition.ItemRarity.Uncommon, "4B69FF" },
		{ ItemDefinition.ItemRarity.Rare, "8847FF" },
		{ ItemDefinition.ItemRarity.Legendary, "D32CE6" },
		{ ItemDefinition.ItemRarity.Epic, "FFD700" }
	};

	void Start()
    {
		SetupControl();
	}

	public void SetupControl()
    {
		Image sp = GetComponent<Image>();
		if (Item != null)
		{
			sp.sprite = graphicNormal;

			if (Amount > 1)
				LabelObject.text = "x" + Amount.ToString();
			else
				LabelObject.text = "";

			IconObject.gameObject.SetActive(true);
			IconObject.sprite = Item.Icon;
			LabelObject.gameObject.SetActive(Amount > 1);
			gradient.SetActive(true);
			gradient.GetComponent<Image>().color = ColorUtility.TryParseHtmlString("#" + RarityColors[Item.Rarity], out Color color) ? color : Color.white;
		}
		else
		{
			sp.sprite = graphicEmpty;
			LabelObject.text = "";
			gradient.SetActive(false);
			IconObject.gameObject.SetActive(false);
			LabelObject.gameObject.SetActive(false);
		}
	}

    // Update is called once per frame
    void Update()
    {
        
    }
}
