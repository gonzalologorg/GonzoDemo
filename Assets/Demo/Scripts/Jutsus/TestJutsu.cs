using System.Collections;
using UnityEngine;

[CreateAssetMenu(menuName = "Jutsus/Behaviours/Test Jutsu")]

public class TestJutsu : JutsuBehaviour
{
    public GameObject ProjectilePrefab;
    public float Speed = 15f;

	public override IEnumerator OnServerCast(JutsuContext ctx)
	{
		GameObject projectile = Instantiate(
		   ProjectilePrefab,
		   ctx.CastOrigin.position,
		   ctx.CastOrigin.rotation
	   );

		projectile.GetComponent<Rigidbody>().linearVelocity =
			ctx.CastOrigin.forward * Speed;

		projectile.GetComponent<Unity.Netcode.NetworkObject>()
			.Spawn();

		yield break;
	}


}
