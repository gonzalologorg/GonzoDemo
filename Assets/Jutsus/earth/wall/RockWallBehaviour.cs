using System.Collections;
using Unity.Netcode;
using UnityEngine;
namespace GonzoDemo
{
    [CreateAssetMenu(menuName = "Jutsus/Behaviours/Rock Wall")]

    public class RockWallBehaviour : JutsuBehaviour
    {
        public GameObject WallPrefab;

        public override IEnumerator OnServerCast(JutsuContext ctx)
		{
			if (WallPrefab == null)
			{
				Debug.LogError("Rock Wall has no prefab assigned.", this);
				yield break;
			}

			if (!ctx.CastEmptyGround(5f, out Vector3 targetPosition))
			{
				Debug.LogWarning("Rock Wall could not find a ground position to spawn on.", ctx.Controller);
				yield break;
			}

			Vector3 flatAimDirection = ctx.GetFlatAimDirection();
			Quaternion wallRotation = Quaternion.LookRotation(flatAimDirection, Vector3.up);
			GameObject wall = Instantiate(WallPrefab, targetPosition, wallRotation);

			int duration = Mathf.Max(0, Mathf.RoundToInt(ctx.GetStat("duration")));
			int health = Mathf.Max(0, Mathf.RoundToInt(ctx.GetStat("health")));
			RockWallController rockWall = wall.GetComponent<RockWallController>();
			NetworkObject networkObject = wall.GetComponent<NetworkObject>();
			if (rockWall == null)
			{
				Debug.LogError("Rock Wall prefab needs a RockWallController component.", WallPrefab);
				Destroy(wall);
				yield break;
			}

			if (networkObject == null)
			{
				Debug.LogError("Rock Wall prefab needs a NetworkObject component.", WallPrefab);
				Destroy(wall);
				yield break;
			}

			if (wall.TryGetComponent<Collider>(out Collider wallCollider))
			{
				float colliderBottomOffset = wallCollider.bounds.min.y - wall.transform.position.y;
				wall.transform.position += Vector3.down * colliderBottomOffset;
			}

			rockWall.Duration = duration;
			rockWall.wallHealth.Value = health;

			networkObject.Spawn();
			yield break;
		}
    }
}
