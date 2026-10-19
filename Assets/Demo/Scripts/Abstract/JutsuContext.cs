using UnityEngine;
using System.Collections;

public class JutsuContext
{
	public JutsuDefinition Definition { get; }
	public JutsuInstance Instance { get; }
	public JutsuController Controller { get; }
	public Transform CastOrigin { get; }

	public JutsuContext(
		JutsuDefinition definition,
		JutsuInstance instance,
		JutsuController controller,
		Transform castOrigin)
	{
		Definition = definition;
		Instance = instance;
		Controller = controller;
		CastOrigin = castOrigin;
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
