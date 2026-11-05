using System.Collections;
using System.Collections.Generic;
using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;
namespace GonzoDemo
{
	public class JutsuController : NetworkBehaviour
	{
		[Serializable]
		public class JutsuSlotConfig
		{
			[Min(0)] public int slot;
			public JutsuDefinition definition;
			[Min(1)] public int level = 1;
		}

		[SerializeField]
		private List<JutsuSlotConfig> startingJutsus = new List<JutsuSlotConfig>();

		[NonSerialized]
		public Dictionary<int, JutsuInstance> Jutsus = new Dictionary<int, JutsuInstance>();
		private readonly NetworkVariable<Quaternion> n_AimRotation = new NetworkVariable<Quaternion>(
			Quaternion.identity,
			NetworkVariableReadPermission.Everyone,
			NetworkVariableWritePermission.Owner);
		private const int JutsuBarSize = 5;
		private static readonly Dictionary<string, JutsuDefinition> loadedJutsus = new Dictionary<string, JutsuDefinition>();
		PlayerController controller;
		HUDController hudController;
		PlayerStats playerStats;

		public override void OnNetworkSpawn()
		{
			//if (IsServer)

			controller = GetComponent<PlayerController>();
			playerStats = GetComponent<PlayerStats>();
			hudController = GetComponent<HUDController>();
			BuildRuntimeJutsus();
			UpdateAimRotation();
		}

		private void LateUpdate()
		{
			if (IsSpawned && IsOwner)
				UpdateAimRotation();
		}

		private void UpdateAimRotation()
		{
			Camera mainCamera = Camera.main;
			if (!IsOwner || mainCamera == null)
				return;

			Quaternion aimRotation = mainCamera.transform.rotation;
			if (Quaternion.Angle(n_AimRotation.Value, aimRotation) >= 0.05f)
				n_AimRotation.Value = aimRotation;
		}

		public Vector3 GetAimAngle()
		{
			return n_AimRotation.Value.eulerAngles;
		}

		private void BuildRuntimeJutsus()
		{
			Jutsus.Clear();
			if (IsOwner) hudController?.ClearJutsus();

			foreach (JutsuSlotConfig entry in startingJutsus)
			{
				if (entry == null || entry.definition == null)
				{
					Debug.LogWarning($"[{nameof(JutsuController)}] Slot sin definición en {name}.", this);
					continue;
				}

				if (Jutsus.ContainsKey(entry.slot))
				{
					Debug.LogWarning($"[{nameof(JutsuController)}] Slot duplicado {entry.slot} en {name}; se ignora esta entrada.", this);
					continue;
				}

				JutsuInstance instance = new JutsuInstance(entry.definition)
				{
					Level = Mathf.Clamp(entry.level, 1, entry.definition.MaxLevel)
				};

				Jutsus.Add(entry.slot, instance);
				if (IsOwner) hudController?.InstallJutsu(entry.slot, instance);
			}
		}

		/// <summary>Installs a picked-up jutsu into the first empty HUD slot.</summary>
		public bool TryAcquireJutsu(JutsuDefinition definition, int level, out int slot)
		{
			slot = -1;
			if (!IsServer || !IsSpawned || definition == null)
				return false;

			// Repeated pickups of a known jutsu upgrade its existing slot by one level.
			foreach (var entry in Jutsus)
			{
				if (entry.Value?.Definition != definition)
					continue;

				slot = entry.Key;
				if (entry.Value.Level >= definition.MaxLevel)
					return false;

				entry.Value.Level++;
				InstallAcquiredJutsuClientRpc(slot, GetDefinitionKey(definition), entry.Value.Level);
				return true;
			}

			for (int candidate = 0; candidate < JutsuBarSize; candidate++)
			{
				if (Jutsus.ContainsKey(candidate))
					continue;

				slot = candidate;
				var instance = new JutsuInstance(definition)
				{
					Level = Mathf.Clamp(level, 1, definition.MaxLevel)
				};
				Jutsus.Add(slot, instance);
				InstallAcquiredJutsuClientRpc(slot, GetDefinitionKey(definition), instance.Level);
				return true;
			}

			return false;
		}

		[ClientRpc]
		private void InstallAcquiredJutsuClientRpc(int slot, string jutsuName, int level, ClientRpcParams rpcParams = default)
		{
			if (!IsOwner || slot < 0 || slot >= JutsuBarSize)
				return;

			// ScriptableObject references are not valid RPC payloads. Resolve the shared asset
			// from assets already loaded by the game (including network prefab dependencies).
			JutsuDefinition definition = FindJutsuDefinition(jutsuName);
			if (definition == null)
			{
				Debug.LogError($"[{nameof(JutsuController)}] No se pudo resolver el jutsu '{jutsuName}' en el cliente.", this);
				return;
			}

			if (Jutsus.TryGetValue(slot, out var instance) && instance?.Definition == definition)
			{
				instance.Level = Mathf.Clamp(level, 1, definition.MaxLevel);
			}
			else
			{
				instance = new JutsuInstance(definition)
				{
					Level = Mathf.Clamp(level, 1, definition.MaxLevel)
				};
				Jutsus[slot] = instance;
			}

			hudController?.InstallJutsu(slot, instance);
		}

		private JutsuDefinition FindJutsuDefinition(string jutsuName)
		{
			if (loadedJutsus.TryGetValue(jutsuName, out var registeredDefinition) && registeredDefinition != null)
				return registeredDefinition;

			foreach (var instance in Jutsus.Values)
				if (instance?.Definition != null && GetDefinitionKey(instance.Definition) == jutsuName)
					return instance.Definition;

			var loadedDefinitions = Resources.FindObjectsOfTypeAll<JutsuDefinition>();
			foreach (var definition in loadedDefinitions)
				if (definition != null && GetDefinitionKey(definition) == jutsuName)
					return definition;

			return null;
		}

		public static void RegisterAvailableJutsu(JutsuDefinition definition)
		{
			if (definition == null)
				return;

			loadedJutsus[GetDefinitionKey(definition)] = definition;
		}

		private static string GetDefinitionKey(JutsuDefinition definition)
		{
			return string.IsNullOrWhiteSpace(definition.JutsuName) ? definition.name : definition.JutsuName;
		}

		public void RequestCast(int slot)
		{
			if (!IsOwner) return;
			if (!Jutsus.TryGetValue(slot, out var jutsu) ||
				jutsu.CooldownEndTime > NetworkManager.ServerTime.Time)
				return;
			RequestCastServerRpc(slot);
		}

		[ServerRpc]
		private void RequestCastServerRpc(int slot, ServerRpcParams rpcParams=default)
		{
			if (!Jutsus.TryGetValue(slot, out JutsuInstance jutsu))
				return;
			if (jutsu.CooldownEndTime > NetworkManager.ServerTime.Time)
				return;

			var context = new JutsuContext(jutsu.Definition, jutsu, this);
			if (jutsu.Definition.Behaviours == null) return;
			foreach (var behaviour in jutsu.Definition.Behaviours)
			{
				if (behaviour == null)
					continue;
				if (!behaviour.PreCast(context))
					return;
			}

			float chakraCost = context.GetChakraCost();
			if (chakraCost > 0f && (playerStats == null || !playerStats.TryUseChakra(chakraCost)))
				return;

			// Reserve the cooldown before starting behaviours so another request cannot cast twice.
			jutsu.CooldownEndTime = NetworkManager.ServerTime.Time + context.GetCooldown();
			ConfirmCooldownClientRpc(slot, jutsu.CooldownEndTime, new ClientRpcParams
			{
				Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
			});
			foreach (var behaviour in jutsu.Definition.Behaviours)
			{
				if (behaviour == null)
					continue;
				StartCoroutine(behaviour.OnServerCast(context));
			}
		}

		[ClientRpc]
		private void ConfirmCooldownClientRpc(int slot, double endTime, ClientRpcParams rpcParams = default)
		{
			if (!IsOwner || !Jutsus.TryGetValue(slot, out var jutsu)) return;
			jutsu.CooldownEndTime = endTime;
			hudController?.UpdateJutsuCooldown(slot, (float)Math.Max(0d, endTime - NetworkManager.ServerTime.Time));
		}
	}
}
