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
		private int localHoldSlot = -1;
		private int serverHoldSlot = -1;
		private JutsuHoldSession serverHoldSession;

		public override void OnNetworkSpawn()
		{
			//if (IsServer)

			controller = GetComponent<PlayerController>();
			hudController = GetComponent<HUDController>();
			BuildRuntimeJutsus();
			UpdateAimRotation();
		}

		private void LateUpdate()
		{
			if (IsSpawned && IsOwner)
				UpdateAimRotation();
		}

		private void Update()
		{
			if (IsServer && serverHoldSession != null && !serverHoldSession.IsComplete &&
				NetworkManager.ServerTime.Time - serverHoldStartedAt >= serverHoldSession.MaxDuration)
			{
				serverHoldSession.Complete(NetworkManager.ServerTime.Time, false);
			}
		}

		private double serverHoldStartedAt;

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

			JutsuContext context = new JutsuContext(jutsu.Definition, jutsu, this);
			JutsuBehaviour holdBehaviour = GetHoldBehaviour(context, out float holdDuration);
			if (holdBehaviour != null)
			{
				if (localHoldSlot >= 0)
					return;

				localHoldSlot = slot;
				controller?.StartCasting(holdDuration, _ => FinishLocalHoldCast(slot));
				RequestHoldCastServerRpc(slot);
				return;
			}

			RequestCastServerRpc(slot);
		}

		public void StopLocalHoldCast()
		{
			if (!IsOwner || localHoldSlot < 0)
				return;

			controller?.StopCasting();
		}

		private void FinishLocalHoldCast(int slot)
		{
			if (!IsOwner || localHoldSlot != slot)
				return;

			localHoldSlot = -1;
			StopHoldCastServerRpc(slot);
		}

		private JutsuBehaviour GetHoldBehaviour(JutsuContext context, out float duration)
		{
			duration = 0f;
			if (context?.Definition?.Behaviours == null)
				return null;

			foreach (JutsuBehaviour behaviour in context.Definition.Behaviours)
			{
				if (behaviour == null)
					continue;

				float candidateDuration = behaviour.GetHoldDuration(context);
				if (candidateDuration <= 0f)
					continue;

				duration = candidateDuration;
				return behaviour;
			}

			return null;
		}

		[ServerRpc]
		private void RequestHoldCastServerRpc(int slot, ServerRpcParams rpcParams = default)
		{
			bool accepted = false;
			float duration = 0f;
			JutsuBehaviour holdBehaviour = null;
			JutsuContext context = null;
			JutsuInstance jutsu = null;

			if (serverHoldSession == null && Jutsus.TryGetValue(slot, out jutsu) &&
				jutsu.CooldownEndTime <= NetworkManager.ServerTime.Time)
			{
				context = new JutsuContext(jutsu.Definition, jutsu, this);
				foreach (JutsuBehaviour behaviour in jutsu.Definition.Behaviours ?? new List<JutsuBehaviour>())
				{
					if (behaviour != null && !behaviour.PreCast(context))
					{
						context = null;
						break;
					}
				}

				if (context != null)
					holdBehaviour = GetHoldBehaviour(context, out duration);

				float chakraCost = context != null ? context.GetChakraCost() : 0f;
				if (holdBehaviour != null && duration > 0f &&
					(chakraCost <= 0f || (controller != null && controller.TryUseChakra(chakraCost))))
				{
					jutsu.CooldownEndTime = NetworkManager.ServerTime.Time + context.GetCooldown();
					ConfirmCooldownClientRpc(slot, jutsu.CooldownEndTime, new ClientRpcParams
					{
						Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
					});
					serverHoldSlot = slot;
					serverHoldStartedAt = NetworkManager.ServerTime.Time;
					serverHoldSession = new JutsuHoldSession(duration, serverHoldStartedAt);
					StartCoroutine(RunHoldCast(holdBehaviour, context, serverHoldSession));
					accepted = true;
				}
			}

			HoldCastResultClientRpc(slot, accepted, new ClientRpcParams
			{
				Send = new ClientRpcSendParams { TargetClientIds = new[] { rpcParams.Receive.SenderClientId } }
			});
		}

		[ServerRpc]
		private void StopHoldCastServerRpc(int slot, ServerRpcParams rpcParams = default)
		{
			if (serverHoldSession == null || serverHoldSlot != slot ||
				rpcParams.Receive.SenderClientId != OwnerClientId)
				return;

			serverHoldSession.Complete(NetworkManager.ServerTime.Time, true);
		}

		private IEnumerator RunHoldCast(JutsuBehaviour behaviour, JutsuContext context, JutsuHoldSession session)
		{
			yield return behaviour.OnServerHoldCast(context, session);
			behaviour.OnServerHoldComplete(context, session.Power, session.WasReleased);
			if (serverHoldSession == session)
			{
				serverHoldSession = null;
				serverHoldSlot = -1;
			}
		}

		[ClientRpc]
		private void HoldCastResultClientRpc(int slot, bool accepted, ClientRpcParams rpcParams = default)
		{
			if (!IsOwner || localHoldSlot != slot || accepted)
				return;

			localHoldSlot = -1;
			controller?.CancelCasting();
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
			if (chakraCost > 0f && (controller == null || !controller.TryUseChakra(chakraCost)))
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
