using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GonzoDemo.AI;

[DefaultExecutionOrder(10000)]
public class MinionHUDBar : MonoBehaviour
{
    [SerializeField, Min(0f)] private float fullHealthWidth = 168f;

    private Minion minion;
    private RectTransform fillRect;
    private TMP_Text levelText;
    private float lastHealth = float.NaN;
    private float lastMaxHealth = float.NaN;
    private int lastLevel = int.MinValue;
    private RectTransform headerRect;
    private RectTransform overlayRect;
    private Canvas overlayCanvas;
    private CanvasGroup overlayVisibility;
    private Transform anchorParent;
    private Vector3 anchorLocalPosition;

    private void Awake()
    {
        minion = GetComponentInParent<Minion>();

        Transform headbar = transform.Find("headbar");
        if (headbar == null && transform.name == "headbar")
            headbar = transform;
        if (headbar == null)
            headbar = transform;

        fillRect = headbar.Find("fill") as RectTransform;
        levelText = headbar.Find("level")?.GetComponent<TMP_Text>();

        headerRect = headbar as RectTransform;
        if (minion == null || headerRect == null) return;

        // Keep the authored world anchor before moving the visual to screen space.
        anchorParent = headbar.parent;
        anchorLocalPosition = headbar.localPosition;
        var overlay = new GameObject("Minion HUD Overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        overlayRect = overlay.GetComponent<RectTransform>();
        overlayCanvas = overlay.GetComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 10;
        overlayVisibility = overlay.GetComponent<CanvasGroup>();
        overlayVisibility.interactable = false;
        overlayVisibility.blocksRaycasts = false;

        headbar.SetParent(overlayRect, false);
        headerRect.anchorMin = headerRect.anchorMax = Vector2.zero;
        headerRect.localScale = Vector3.one;
        headerRect.localRotation = Quaternion.identity;
        // The existing canvas becomes a nested canvas and inherits the overlay.
        var headerCanvas = headbar.GetComponent<Canvas>();
        if (headerCanvas != null) headerCanvas.overrideSorting = false;
        var scaler = headbar.GetComponent<CanvasScaler>();
        if (scaler != null) scaler.enabled = false;
        foreach (var graphic in headbar.GetComponentsInChildren<Graphic>(true))
            graphic.raycastTarget = false;
        overlayVisibility.alpha = 0f;
    }

    private void LateUpdate()
    {
        UpdateScreenPosition();
        if (minion == null || fillRect == null || levelText == null)
            return;

        float health = minion.GetStat(MinionStat.Health);
        float maxHealth = minion.GetStat(MinionStat.MaxHealth);
        if (!Mathf.Approximately(health, lastHealth) || !Mathf.Approximately(maxHealth, lastMaxHealth))
        {
            float healthRatio = maxHealth > 0f ? Mathf.Clamp01(health / maxHealth) : 0f;
            Vector2 size = fillRect.sizeDelta;
            size.x = fullHealthWidth * healthRatio;
            fillRect.sizeDelta = size;
            lastHealth = health;
            lastMaxHealth = maxHealth;
        }

        int level = minion.Level;
        if (level != lastLevel)
        {
            levelText.text = level.ToString();
            lastLevel = level;
        }
    }

    private void UpdateScreenPosition()
    {
        if (overlayCanvas == null) return;
        var camera = Camera.main;
        if (camera == null || anchorParent == null || minion == null)
        {
            overlayVisibility.alpha = 0f;
            return;
        }

        Vector3 screenPoint = camera.WorldToScreenPoint(anchorParent.TransformPoint(anchorLocalPosition));
        bool visible = screenPoint.z > camera.nearClipPlane && camera.pixelRect.Contains(screenPoint);
        overlayVisibility.alpha = visible ? 1f : 0f;
        if (!visible) return;

        overlayCanvas.targetDisplay = camera.targetDisplay;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayRect, screenPoint, null, out var position))
        {
            // Convert from the canvas pivot to the header's bottom-left anchor.
            headerRect.anchoredPosition = position + overlayRect.rect.size * overlayRect.pivot;
        }
    }

    private void OnDisable()
    {
        if (overlayVisibility != null) overlayVisibility.alpha = 0f;
    }

    private void OnDestroy()
    {
        if (overlayCanvas != null) Destroy(overlayCanvas.gameObject);
    }
}
