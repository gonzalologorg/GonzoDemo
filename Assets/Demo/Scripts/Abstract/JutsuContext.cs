using UnityEngine;
using System.Collections;
namespace GonzoDemo
{
	public class JutsuContext
	{
		public JutsuDefinition Definition { get; }
		public JutsuInstance Instance { get; }
		public JutsuController Controller { get; }
		public Vector3 CastOrigin { get; }
		public Vector3 CastAngle { get; }

		public JutsuContext(
			JutsuDefinition definition,
			JutsuInstance instance,
			JutsuController controller,
			Vector3 castOrigin,
			Vector3 castAngle)
		{
			Definition = definition;
			Instance = instance;
			Controller = controller;
			CastOrigin = castOrigin;
			CastAngle = castAngle;
		}

		public float GetStat(string stat)
		{
			return Definition.Stats[Instance.Level].TryGetValue(stat, out var value) ? value : 0;
		}

		public int GetLevel() => Instance.Level;

		public float GetCooldown()
		{
			var cooldowns = Definition.Cooldown;
			if (cooldowns == null || cooldowns.Count == 0)
				return 0f;

			// Levels start at 1; balance lists start at index 0.
			int index = Mathf.Clamp(Instance.Level - 1, 0, cooldowns.Count - 1);
			return Mathf.Max(0f, cooldowns[index]);
		}

		public float GetChakraCost()
		{
			var costs = Definition.Cost;
			if (costs == null || costs.Count == 0) return 0f;
			return Mathf.Max(0f, costs[Mathf.Clamp(Instance.Level - 1, 0, costs.Count - 1)]);
		}

		public IEnumerator Wait(float seconds)
		{
			yield return new WaitForSeconds(seconds);
		}
	}

}
