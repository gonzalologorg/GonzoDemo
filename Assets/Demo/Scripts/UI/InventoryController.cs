using Blocks.Gameplay.Core;
using System.Collections;
using Unity.Netcode;
using Unity.Services.Matchmaker.Models;
using UnityEngine;
using UnityEngine.InputSystem;
namespace GonzoDemo
{

    public class InventoryController : MonoBehaviour
    {
        public RectTransform InventorySlots;
        public GameObject InventorySlotPrefab;
        public ItemDatabase ItemDatabase;
        public PlayerInventory playerInventory;
        public GameObject EquipmentPanel;

        bool inventoryOpen = false;
        CanvasGroup canvasGroup;

        public void StartPanel()
        {
            ItemDatabase = Resources.Load<ItemDatabase>("Data/ItemDatabase");
            for (int i = 0; i < 20; i++)
            {
                GameObject slot = Instantiate(InventorySlotPrefab, InventorySlots);
                slot.name = "InventorySlot_" + i;
                ItemControl item = slot.GetComponent<ItemControl>();
                item.rootCanvas = GetComponentInParent<Canvas>();
                item.inventoryController = this;
                item.IsEquipment = false;
                item.Slot = i;

                // Setup for first time , if we have a player inventory, we will set the item in the slot
                if (playerInventory != null && playerInventory.inventory.ContainsKey(i))
                {
                    ItemInstance itemInstance = playerInventory.inventory[i];
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

            playerInventory.OnInventoryUpdated += OnInventoryUpdated;
            playerInventory.OnEquipmentUpdated += OnEquipmentUpdated;
        }

        private void OnInventoryUpdated()
        {
            int max = 20;

            for (int i = 0; i <= max; i++)
            {
                // Check if we have a slot for this index
                GameObject control = InventorySlots.Find("InventorySlot_" + i)?.gameObject;
                if (control == null) { continue; }

                // Check if the control has an ItemControl component
                ItemControl ItemSlot = control.GetComponent<ItemControl>();
                if (ItemSlot == null) { continue; }

                ItemInstance invItem = playerInventory.inventory.ContainsKey(i) ? playerInventory.inventory[i] : null;
                // If the player has an item, we set the control, otherwise we clear it
                if (invItem == null)
                {
                    ItemSlot.Item = null;
                    ItemSlot.Amount = 0;
                }
                else
                {
                    ItemSlot.Item = ItemDatabase.Get(invItem.itemId);
                    ItemSlot.Amount = invItem.amount;
                }

                ItemSlot.SetupControl();
            }
        }

        public void OnEquipmentUpdated()
        {
            Debug.Log("Equipment updated!");
            for (int i = 21; i <= 25; i++)
            {
                ItemControl itemSlot = EquipmentPanel.transform.Find("Equipment_" + i)?.GetComponent<ItemControl>();
                ItemInstance ourItem = playerInventory.equipment.ContainsKey(i) ? playerInventory.equipment[i] : null;

                itemSlot.Item = ourItem != null ? playerInventory.itemDatabase.Get(ourItem.itemId) : null;
                itemSlot.Amount = ourItem != null ? ourItem.amount : 0;
                itemSlot.SetupControl();
            }
        }

        public void ToggleInventory(bool state)
        {
            StartCoroutine(ToggleFade(state ? 1 : 0, .5f));

            // Set input status on or off, along setting the alpha target value
            canvasGroup.interactable = state;
            canvasGroup.blocksRaycasts = state;

            Cursor.visible = state;
            Cursor.lockState = state ? CursorLockMode.None : CursorLockMode.Locked;
            playerInventory.gameObject.GetComponent<CoreCameraController>().enabled = !state;
        }
        void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        IEnumerator ToggleFade(float targetAlpha, float fadeDuration)
        {
            float startAlpha = canvasGroup.alpha;
            float elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;

                float t = Mathf.Clamp01(elapsed / fadeDuration);

                canvasGroup.alpha =
                    Mathf.Lerp(startAlpha, targetAlpha, t);

                yield return null;
            }

            canvasGroup.alpha = targetAlpha;
        }


        // Update is called once per frame
        void Update()
        {
            // Check for key input to toggle inventory
            if (Keyboard.current != null && Keyboard.current.iKey.wasPressedThisFrame)
            {
                inventoryOpen = !inventoryOpen;
                ToggleInventory(inventoryOpen);
            }
        }
    }
}