using Blocks.Gameplay.Core;
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
	private UIDocument m_UIDocument;

	#region UI Elements
	private Label uiNickLabel;
	private Label uiLevelLabel;
	private Label uiHealthLabel;
	private Label uiChakraLabel;
	private Image uiHealthBar;
	private Image uiChakraBar;
	public VisualElement[] uiJutsuControls = new VisualElement[5];
	public Canvas hudHead;
	Camera mainCamera;

	#endregion

	public override void OnNetworkSpawn()
	{
		if (!IsOwner)
		{
			DisableForNonOwner();
			return;
		}

		RegisterEventListeners();
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
		UpdateHUDBar(uiHealthBar, 45, 100);
	}

	private void RegisterEventListeners()
	{
		if (onStatChanged != null) onStatChanged.RegisterListener(HandleStatChanged);
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

	public void UpdateName(string newName)
	{
		if (uiNickLabel != null)
		{
			uiNickLabel.text = newName;
		}

		GameObject nickLabel = hudHead.transform.Find("Name").gameObject;
		if (nickLabel.GetComponent<TextMeshProUGUI>() != null)
		{
			nickLabel.GetComponent<TextMeshProUGUI>().text = newName;
		}
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
