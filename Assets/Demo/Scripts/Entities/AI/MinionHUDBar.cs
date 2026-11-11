using TMPro;
using UnityEngine;
using GonzoDemo.AI;

public class MinionHUDBar : MonoBehaviour
{
    [SerializeField, Min(0f)] private float fullHealthWidth = 168f;

    private Minion minion;
    private RectTransform fillRect;
    private TMP_Text levelText;
    private float lastHealth = float.NaN;
    private float lastMaxHealth = float.NaN;
    private int lastLevel = int.MinValue;

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
    }

    private void LateUpdate()
    {
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
}
