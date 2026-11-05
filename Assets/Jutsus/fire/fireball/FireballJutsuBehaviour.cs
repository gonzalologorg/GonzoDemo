using System.Collections;
using Unity.Netcode;
using UnityEngine;
namespace GonzoDemo
{
	[CreateAssetMenu(menuName = "Jutsus/Behaviours/Fireball Jutsu")]

	public class FireballJutsuBehaviour : JutsuBehaviour
	{
		public GameObject ProjectilePrefab;
		public float Speed = 1500f;

		public override IEnumerator OnServerCast(JutsuContext ctx)
		{
			GameObject projectile = Instantiate(
			   ProjectilePrefab,
			   ctx.GetCastOrigin(),
			   Quaternion.Euler(ctx.GetCastAngle())
		   );

			ProjectileBase projectileBase = projectile.GetComponent<ProjectileBase>();
			projectileBase.Speed = Speed;
			//DamageResolver.BindProjectileImpact(projectileBase, ctx.Controller.gameObject);

			projectile.GetComponent<NetworkObject>()
				.Spawn();

			projectile.GetComponent<ProjectileBase>().Initialize(ctx.Controller.GetComponentInParent<NetworkObject>());

			yield break;
		}


	}
}
