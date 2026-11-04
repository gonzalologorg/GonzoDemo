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
					Level = Mathf.Max(1, entry.level)
				};

				Jutsus.Add(entry.slot, instance);
				if (IsOwner) hudController?.InstallJutsu(entry.slot, instance);
			}
		}

		public void RequestCast(int slot)
		{
			if (!IsOwner) return;
			if (!Jutsus.TryGetValue(slot, out var jutsu) ||
				jutsu.CooldownEndTime > NetworkManager.ServerTime.Time)
				return;
			if (Camera.main == null) return;

			RequestCastServerRpc(slot, Camera.main.transform.eulerAngles);
		}

		[ServerRpc]
		private void RequestCastServerRpc(int slot, Vector3 angle, ServerRpcParams rpcParams=default)
		{
			if (!Jutsus.TryGetValue(slot, out JutsuInstance jutsu))
				return;
			if (jutsu.CooldownEndTime > NetworkManager.ServerTime.Time)
				return;

			var context = new JutsuContext(jutsu.Definition, jutsu, this, controller.HeadTransform.position, angle);
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
