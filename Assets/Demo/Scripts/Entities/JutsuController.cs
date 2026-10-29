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

		public override void OnNetworkSpawn()
		{
			//if (IsServer)

			controller = GetComponent<PlayerController>();
			hudController = GetComponent<HUDController>();
			BuildRuntimeJutsus();

		}

		private void BuildRuntimeJutsus()
		{
			Jutsus.Clear();

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
				hudController?.InstallJutsu(entry.slot, instance);
			}
		}

		public void RequestCast(int slot)
		{
			if (!IsOwner) return;

			RequestCastServerRpc(slot, Camera.main.transform.eulerAngles);
		}

		[ServerRpc]
		private void RequestCastServerRpc(int slot, Vector3 angle, ServerRpcParams rpcParams=default)
		{
			if (!Jutsus.TryGetValue(slot, out JutsuInstance jutsu))
				return;

			var context = new JutsuContext(jutsu.Definition, jutsu, this, controller.HeadTransform.position, angle);
			foreach (var behaviour in jutsu.Definition.Behaviours)
			{
				if (behaviour == null)
					continue;
				if (!behaviour.PreCast(context))
					return;
			}
			foreach (var behaviour in jutsu.Definition.Behaviours)
			{
				if (behaviour == null)
					continue;
				StartCoroutine(behaviour.OnServerCast(context));
			}
		}
	}
}
