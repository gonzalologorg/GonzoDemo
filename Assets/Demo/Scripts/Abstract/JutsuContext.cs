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

		public float GetCooldown() => Definition.Cooldown[Instance.Level];

		public IEnumerator Wait(float seconds)
		{
			yield return new WaitForSeconds(seconds);
		}
	}

}