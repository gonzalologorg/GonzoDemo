using System.Collections;
using Unity.Netcode;
using UnityEngine;
namespace GonzoDemo
{
	[CreateAssetMenu(menuName = "Jutsus/Behaviours/Test Jutsu")]

	public class TestJutsu : JutsuBehaviour
	{
		public GameObject ProjectilePrefab;
		public float Speed = 1500f;

		public override IEnumerator OnServerCast(JutsuContext ctx)
		{
			GameObject projectile = Instantiate(
			   ProjectilePrefab,
			   ctx.CastOrigin,
			   Quaternion.Euler(ctx.CastAngle)
		   );

			projectile.GetComponent<ProjectileBase>().Speed = Speed;
			projectile.GetComponent<NetworkObject>()
				.Spawn();

			projectile.GetComponent<ProjectileBase>().Initialize(ctx.Controller.GetComponentInParent<NetworkObject>());

			yield break;
		}


	}
}