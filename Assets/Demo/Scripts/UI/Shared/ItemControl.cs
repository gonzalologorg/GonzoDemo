using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ItemControl : MonoBehaviour,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler,
    IDropHandler
{
    public Sprite graphicHover;
    public Sprite graphicNormal;
    public Sprite graphicEmpty;

    public ItemDefinition Item;
    public Image IconObject;
    public TMP_Text LabelObject;
	public int Amount;
    public int Slot;
    public bool IsEquipment = true;

	public GameObject gradient;
    public InventoryController inventoryController;

    GameObject dragObject;
    RectTransform dragRect;
    public Canvas rootCanvas;

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

    public void OnBeginDrag(PointerEventData eventData)
	{
        dragObject = new GameObject(
            "DraggedItem",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(CanvasGroup)
        );

        dragObject.transform.SetParent(
            rootCanvas.transform, false
        );

        Image image = dragObject.GetComponent<Image>();
        image.sprite = IconObject.sprite;
        image.raycastTarget = false;

        CanvasGroup group = dragObject.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;

        dragRect = dragObject.GetComponent<RectTransform>();
        dragRect.sizeDelta = ((RectTransform)transform).rect.size;

        IconObject.color = new Color(1, 1, 1, 0.4f);

        OnDrag(eventData);
    }

	public void OnDrag(PointerEventData eventData)
	{
        if (dragRect == null)
            return;

        RectTransform canvasRect =
            (RectTransform)rootCanvas.transform;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            eventData.position,
            rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : rootCanvas.worldCamera,
            out Vector2 localPoint))
        {
            dragRect.localPosition = localPoint;
        }
    }

	public void OnEndDrag(PointerEventData eventData)
	{
        if (dragObject != null)
            Destroy(dragObject);

        IconObject.color = Color.white;        
    }

    public void OnDrop(PointerEventData eventData)
    {
        ItemControl source =
        eventData.pointerDrag?.GetComponent<ItemControl>();

        if (source == null || source == this)
            return;

        inventoryController.playerInventory.RequestMoveItemRpc(
            source.Slot,
            Slot,
            source.IsEquipment,
            IsEquipment
        );
    }

        // Update is called once per frame
    void Update()
    {
        
    }
}
