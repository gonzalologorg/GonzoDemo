using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JutsuController : MonoBehaviour
{
	public Dictionary<int, JutsuInstance> Jutsus;
	void Start()
	{
		Jutsus = new Dictionary<int, JutsuInstance>();
	}
	// Start is called once before the first execution of Update after the MonoBehaviour is created
	private IEnumerator ExecuteJutsu(JutsuContext ctx)
	{
		foreach (var behaviour in ctx.Definition.Behaviours)
		{
			if (behaviour == null)
				continue;

			if (!behaviour.PreCast(ctx))
				yield break;
		}

		foreach (var behaviour in ctx.Definition.Behaviours)
		{
			if (behaviour == null)
				continue;

			yield return behaviour.OnServerCast(ctx);
		}
	}
}
