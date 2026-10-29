using Blocks.Gameplay.Core;
using System.Collections;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class HUDController : NetworkBehaviour
{
	[Header("Component Dependencies")]
	[SerializeField] private CoreStatsHandler coreStats;
	[Header("Listening to Events")]
	[SerializeField] private StatChangeEvent onStatChanged;
	[SerializeField] private RespawnStatusEvent onRespawnStatusChanged;
	private UIDocument m_UIDocument;

	#region UI Elements
	private Label uiNickLabel;
	private Label uiLevelLabel;
	private Label uiHealthLabel;
	private Label uiChakraLabel;
	private Image uiHealthBar;
	private Image uiChakraBar;
	public VisualElement[] uiJutsuControls = new VisualElement[5];
	#endregion

	public override void OnNetworkSpawn()
	{
		if (!IsOwner)
		{
			DisableForNonOwner();
			return;
		}

		RegisterEventListeners();
		StartCoroutine(InitialHUDUpdate());
	}

	void Awake()
	{
		if (!ValidateComponents()) return;

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
		enabled = false;
	}

	private bool ValidateComponents()
	{
		if (coreStats == null)
		{
			Debug.LogError("CoreHUD could not find CoreStatsHandler! The HUD will not function.", this);
			enabled = false;
			return false;
		}
		return true;
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
		UpdateHUDBar(uiHealthBar, 45, 100);
	}

	private void RegisterEventListeners()
	{
		if (onStatChanged != null) onStatChanged.RegisterListener(HandleStatChanged);
	}

	/// <summary>
	/// Coroutine that performs initial HUD updates after spawn.
	/// </summary>
	/// <returns>IEnumerator for coroutine execution.</returns>
	private IEnumerator InitialHUDUpdate()
	{
		yield return null;

		if (coreStats != null)
		{
			foreach (var stat in coreStats.GetAllStats())
			{
				HandleStatChanged(new StatChangePayload
				{
					statName = stat.name,
					currentValue = stat.current,
					maxValue = stat.max
				});
			}
		}
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
			UpdateHUDBar(uiHealthBar, (int)payload.currentValue, (int)payload.maxValue);
		else if (payload.statID == StatKeys.Stamina)
			UpdateHUDBar(uiChakraBar, (int)payload.currentValue, (int)payload.maxValue);
	}

	private void UpdateHUDBar(Image control, int value, int maxValue)
	{
		float progress = (float)value / maxValue;
		if (control != null)
		{
			control.style.width = Length.Percent(progress * 100);
			control.style.unitySliceRight = (int)((1 - progress) * 1008);
			Label label = control.ElementAt(0) as Label;
			label.text = $"{value}/{maxValue}";
		}
	}

	#endregion

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
		var iconImage = jutsuControl.Q<Image>("JutsuIcon");
		if (iconImage != null && instance.Definition != null)
		{
			iconImage.style.backgroundImage = new StyleBackground(instance.Definition.JutsuIcon.texture);
			iconImage.style.unityBackgroundImageTintColor = new StyleColor(Color.white);
		}
	}
}
