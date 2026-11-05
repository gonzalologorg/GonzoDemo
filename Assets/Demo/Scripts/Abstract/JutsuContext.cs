using UnityEngine;
using System.Collections;
namespace GonzoDemo
{
	public class JutsuContext
	{
		public JutsuDefinition Definition { get; }
		public JutsuInstance Instance { get; }
		public JutsuController Controller { get; }
		public Vector3 CastOrigin => GetCastOrigin();
		public Vector3 CastAngle => GetCastAngle();

		public JutsuContext(
			JutsuDefinition definition,
			JutsuInstance instance,
			JutsuController controller)
		{
			Definition = definition;
			Instance = instance;
			Controller = controller;
		}

		public float GetStat(string stat)
		{
			if (Definition?.Stats == null || Instance == null ||
				!Definition.Stats.TryGetValue(Instance.Level, out var levelStats) || levelStats == null)
				return 0f;

			return levelStats.TryGetValue(stat, out var value) ? value : 0f;
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

		public Vector3 GetCastOrigin()
		{
			PlayerController playerController = Controller != null
				? Controller.GetComponent<PlayerController>()
				: null;
			return playerController != null && playerController.HeadTransform != null
				? playerController.HeadTransform.position
				: Controller != null ? Controller.transform.position : Vector3.zero;
		}

		public Vector3 GetCastAngle()
		{
			return Controller != null ? Controller.GetAimAngle() : Vector3.zero;
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
