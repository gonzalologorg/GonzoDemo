using System.Collections;
using Unity.Netcode;
using UnityEngine;
namespace GonzoDemo
{
	[CreateAssetMenu(menuName = "Jutsus/Behaviours/MultiFireball Jutsu")]

	public class MultiFireballJutsuBehaviour : JutsuBehaviour
	{
		public GameObject ProjectilePrefab;
		public float Speed = 1500f;

		public override IEnumerator OnServerCast(JutsuContext ctx)
		{
			int fireballCount = Mathf.Max(0, Mathf.RoundToInt(ctx.GetStat("count")));
			Vector3 castOrigin = ctx.GetCastOrigin();

			for (int i = 0; i < fireballCount; i++)
			{
				// Read the latest server-synchronized camera rotation for each shot.
				Quaternion castRotation = Quaternion.Euler(ctx.GetCastAngle());
				GameObject projectile = Instantiate(
					ProjectilePrefab,
					castOrigin,
					castRotation
				);

				ProjectileBase projectileBase = projectile.GetComponent<ProjectileBase>();
				projectileBase.Speed = Speed;
				//DamageResolver.BindProjectileImpact(projectileBase, ctx.Controller.gameObject);
				projectile.GetComponent<NetworkObject>()
					.Spawn();

				projectile.GetComponent<ProjectileBase>().Initialize(ctx.Controller.GetComponentInParent<NetworkObject>());

				//Wait timer 
				yield return new WaitForSeconds(0.5f);
			}
			yield break;
		}


	}
}
