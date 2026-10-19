using System.Collections;
using UnityEngine;

public class JutsuBehaviour : ScriptableObject
{
    public virtual bool PreCast(JutsuContext ctx)
    {
        return true;
    }

    public virtual IEnumerator OnServerCast(JutsuContext ctx)
	{
		yield break;
	}

    public virtual void OnClientCast(JutsuContext ctx) { }
    public virtual void OnSelect(JutsuContext ctx) { }
    public virtual void OnUnequip(JutsuContext ctx) { }
    public virtual void OnEquip(JutsuContext ctx) { }
    public virtual void OnRender(JutsuContext ctx) { }
    public virtual void DrawHUD(JutsuContext ctx) { }
}
