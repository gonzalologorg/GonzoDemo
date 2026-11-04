using Blocks.Gameplay.Core;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class HUDController : NetworkBehaviour
{
	[Header("Listening to Events")]
	[SerializeField] private StatChangeEvent onStatChanged;
	[SerializeField] private RespawnStatusEvent onRespawnStatusChanged;
	[SerializeField, Range(0f, 1f)] private float unselectedJutsuOpacity = 0.5f;
	private UIDocument m_UIDocument;
	private GonzoDemo.PlayerStats playerStats;
	private string displayedName = string.Empty;
	private int displayedLevel = 1;
	private readonly Dictionary<int, GonzoDemo.JutsuInstance> installedJutsus = new Dictionary<int, GonzoDemo.JutsuInstance>();

	#region UI Elements
	private Label uiNickLabel;
	private Label uiLevelLabel;
	private Label uiHealthLabel;
	private Label uiChakraLabel;
	private Image uiHealthBar;
	private Image uiChakraBar;
	private VisualElement uiXPBar;
	public VisualElement[] uiJutsuControls = new VisualElement[5];
	public Canvas hudHead;
	Camera mainCamera;

	#endregion

	public override void OnNetworkSpawn()
	{
		playerStats = GetComponent<GonzoDemo.PlayerStats>();
		if (playerStats != null)
		{
			playerStats.OnLevelChanged += UpdateLevel;
			if (IsOwner)
			{
				playerStats.OnExperienceChanged += UpdateExperience;
				playerStats.OnStatChanged += HandlePlayerStatChanged;
				UpdateResourceBars();
			}
			UpdateLevel(playerStats.Level);
			if (IsOwner) UpdateExperience(playerStats.Experience);
		}
		var player = GetComponent<GonzoDemo.PlayerController>();
		if (player != null) UpdateName(player.PlayerName);
		if (!IsOwner)
		{
			DisableForNonOwner();
			return;
		}

		RegisterEventListeners();
	}

	public override void OnNetworkDespawn()
	{
		if (playerStats != null)
		{
			playerStats.OnLevelChanged -= UpdateLevel;
			playerStats.OnExperienceChanged -= UpdateExperience;
			playerStats.OnStatChanged -= HandlePlayerStatChanged;
		}
		if (onStatChanged != null) onStatChanged.UnregisterListener(HandleStatChanged);
	}

	void Awake()
	{
		mainCamera = Camera.main;
		CreateHUD();
	}

	#region HUD Initialization
	/// <summary>
	/// Disables the HUD for non-owner clients.
	/// </summary>
	private void DisableForNonOwner()
	{
		if (TryGetComponent<UIDocument>(out var uiDoc))
		{
			uiDoc.enabled = false;
		}
		//enabled = false;
	}

	private void CreateHUD()
	{
		m_UIDocument = GetComponent<UIDocument>();
		if (m_UIDocument == null)
		{
			Debug.LogError("CoreHUD requires a UIDocument component.", this);
			return;
		}

		var root = m_UIDocument.rootVisualElement;
		CacheUIElements(root);
		ClearJutsus();
		UpdateHUDBar(uiHealthBar, uiHealthLabel, 0f, 0f);
		UpdateHUDBar(uiChakraBar, uiChakraLabel, 0f, 0f);
	}

	private void RegisterEventListeners()
	{
		if (playerStats == null && onStatChanged != null) onStatChanged.RegisterListener(HandleStatChanged);
	}


	/// <summary>
	/// Handles stat change events, updating progress bars and handling elimination notifications.
	/// </summary>
	/// <param name="payload">The stat change payload.</param>
	private void HandleStatChanged(StatChangePayload payload)
	{
		// Process local player stat changes
		if (payload.targetPlayerId == OwnerClientId)
		{
			UpdateLocalPlayerStats(payload);
			//HandleStatChangedLocal(payload);
		}
	}

	private void CacheUIElements(VisualElement root)
	{
		uiNickLabel = root.Q<Label>("Nick");
		uiLevelLabel = root.Q<Label>("Level");
		uiHealthLabel = root.Q<Label>("HealthLabel");
		uiChakraLabel = root.Q<Label>("ChakraLabel");
		uiHealthBar = root.Q<Image>("HealthFill");
		uiChakraBar = root.Q<Image>("ChakraFill");
		uiXPBar = root.Q<VisualElement>("XPBar");
		if (uiXPBar != null)
		{
			uiXPBar.style.flexGrow = 0f;
			uiXPBar.style.flexShrink = 0f;
			uiXPBar.style.width = Length.Percent(0f);
		}

		for (int i = 0; i < uiJutsuControls.Length; i++)
		{
			uiJutsuControls[i] = root.Q<VisualElement>("JutsuContainer").Q<VisualElement>($"Jutsu{i + 1}");
		}
	}

	#endregion

	#region UI Changes
	private void UpdateLocalPlayerStats(StatChangePayload payload)
	{
		if (payload.statID == StatKeys.Health)
			UpdateHUDBar(uiHealthBar, uiHealthLabel, payload.currentValue, payload.maxValue);
		else if (payload.statID == StatKeys.Stamina)
			UpdateHUDBar(uiChakraBar, uiChakraLabel, payload.currentValue, payload.maxValue);
	}

	private void HandlePlayerStatChanged(GonzoDemo.PlayerStats.StatType type, float value)
	{
		if (type == GonzoDemo.PlayerStats.StatType.Health || type == GonzoDemo.PlayerStats.StatType.MaxHealth ||
			type == GonzoDemo.PlayerStats.StatType.Chakra || type == GonzoDemo.PlayerStats.StatType.MaxChakra)
			UpdateResourceBars();
	}

	private void UpdateResourceBars()
	{
		if (!IsOwner || playerStats == null) return;
		UpdateHUDBar(uiHealthBar, uiHealthLabel, playerStats.GetStat(GonzoDemo.PlayerStats.StatType.Health),
			playerStats.GetStat(GonzoDemo.PlayerStats.StatType.MaxHealth));
		UpdateHUDBar(uiChakraBar, uiChakraLabel, playerStats.GetStat(GonzoDemo.PlayerStats.StatType.Chakra),
			playerStats.GetStat(GonzoDemo.PlayerStats.StatType.MaxChakra));
	}

	private void UpdateHUDBar(Image control, Label label, float value, float maxValue)
	{
		maxValue = Mathf.Max(0f, maxValue);
		value = Mathf.Clamp(value, 0f, maxValue);
		float progress = maxValue > 0f ? value / maxValue : 0f;
		if (control != null)
		{
			control.style.width = Length.Percent(progress * 100);
			control.style.unitySliceRight = (int)((1 - progress) * 1008);
		}
		if (label != null) label.text = $"{value:0.##}/{maxValue:0.##}";
	}

	#endregion

	public void SelectJutsu(int slot)
	{
		if (slot < 0 || slot >= uiJutsuControls.Length)
			return;

		for (int i = 0; i < uiJutsuControls.Length; i++)
		{
			if (uiJutsuControls[i] != null)
				uiJutsuControls[i].style.opacity = i == slot ? 1f : unselectedJutsuOpacity;
		}
	}

	public void InstallJutsu(int slot, GonzoDemo.JutsuInstance instance)
	{
		if (slot < 0 || slot >= uiJutsuControls.Length)
		{
			Debug.LogWarning($"[{nameof(HUDController)}] Slot {slot} is out of bounds for Jutsu controls.", this);
			return;
		}
		var jutsuControl = uiJutsuControls[slot];
		if (jutsuControl == null)
		{
			Debug.LogWarning($"[{nameof(HUDController)}] Jutsu control for slot {slot} is not assigned.", this);
			return;
		}
		if (instance == null || instance.Definition == null) return;
		installedJutsus[slot] = instance;
		var iconImage = jutsuControl.Q<Image>("JutsuIcon");
		var cooldownIcon = jutsuControl.Q<Image>("CDJutsuIcon");
		var sprite = instance.Definition.JutsuIcon;
		if (iconImage != null)
		{
			iconImage.style.backgroundImage = new StyleBackground(sprite);
			iconImage.style.unityBackgroundImageTintColor = new StyleColor(Color.white);
		}
		if (cooldownIcon != null)
		{
			cooldownIcon.style.backgroundImage = new StyleBackground(sprite);
			cooldownIcon.style.unityBackgroundImageTintColor = Color.white;
		}
		UpdateJutsuCooldown(slot, 0f);
	}

	public void ClearJutsus()
	{
		installedJutsus.Clear();
		for (int slot = 0; slot < uiJutsuControls.Length; slot++)
		{
			var icon = uiJutsuControls[slot]?.Q<Image>("JutsuIcon");
			if (icon != null)
				icon.style.unityBackgroundImageTintColor = new Color(0f, 0f, 0f, 0.5f);
			UpdateJutsuCooldown(slot, 0f);
		}
	}

	public void UpdateJutsuCooldown(int slot, float remaining)
	{
		if (slot < 0 || slot >= uiJutsuControls.Length) return;
		var control = uiJutsuControls[slot];
		if (control == null) return;
		bool coolingDown = remaining > 0f;
		var icon = control.Q<Image>("JutsuIcon");
		var cooldownIcon = control.Q<Image>("CDJutsuIcon");
		var timeLabel = control.Q<Label>("CDTime");
		if (icon != null) icon.style.display = coolingDown ? DisplayStyle.None : DisplayStyle.Flex;
		// CDJutsuIcon already defines its grayscale filter in the UXML.
		if (cooldownIcon != null) cooldownIcon.style.display = coolingDown ? DisplayStyle.Flex : DisplayStyle.None;
		if (timeLabel != null)
		{
			timeLabel.style.display = coolingDown ? DisplayStyle.Flex : DisplayStyle.None;
			timeLabel.text = coolingDown ? Mathf.CeilToInt(remaining).ToString() : string.Empty;
		}
	}

	private void Update()
	{
		if (!IsSpawned || !IsOwner) return;
		double now = NetworkManager.ServerTime.Time;
		foreach (var entry in installedJutsus)
			UpdateJutsuCooldown(entry.Key, (float)System.Math.Max(0d, entry.Value.CooldownEndTime - now));
	}

	public void UpdateLevel(int level)
	{
		displayedLevel = level;
		if (IsOwner && uiLevelLabel != null) uiLevelLabel.text = $"Level: {level}";
		UpdateHeadLabel();
		if (IsOwner && playerStats != null) UpdateExperience(playerStats.Experience);
	}

	private void UpdateExperience(int experience)
	{
		if (!IsOwner || uiXPBar == null || playerStats == null) return;
		int required = playerStats.ExperienceToNextLevel;
		float progress = required > 0 ? Mathf.Clamp01((float)experience / required) : 0f;
		uiXPBar.style.width = Length.Percent(progress * 100f);
		uiXPBar.tooltip = $"{experience}/{required} XP";
	}

	public void UpdateName(string newName)
	{
		displayedName = newName;
		if (IsOwner && uiNickLabel != null)
		{
			uiNickLabel.text = newName;
		}

		UpdateHeadLabel();
	}

	private void UpdateHeadLabel()
	{
		if (hudHead == null) return;
		var nameTransform = hudHead.transform.Find("Name");
		if (nameTransform != null && nameTransform.TryGetComponent<TextMeshProUGUI>(out var label))
			label.text = $"{displayedName} · Nv. {displayedLevel}";
	}

	private void FixedUpdate()
	{
		UpdateNameDisplayRotation();
	}

	/// <summary>
	/// Rotates the nameplate canvas to always face the camera (billboard effect).
	/// The 180-degree rotation corrects the canvas facing direction.
	/// </summary>
	private void UpdateNameDisplayRotation()
	{
		if (hudHead == null || !hudHead.gameObject.activeSelf)
		{
			return;
		}

		if (mainCamera == null)
		{
			mainCamera = Camera.main;
		}

		if (mainCamera == null)
		{
			return;
		}

		Transform nameplate = hudHead.transform;
		Vector3 directionToCamera = mainCamera.transform.position - nameplate.position;

		if (directionToCamera.sqrMagnitude > 0.0001f)
		{
			nameplate.rotation = Quaternion.LookRotation(directionToCamera, mainCamera.transform.up)
				* Quaternion.Euler(0f, 180f, 0f);
		}
	}
}
