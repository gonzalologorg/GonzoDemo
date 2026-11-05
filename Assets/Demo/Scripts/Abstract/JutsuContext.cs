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

		/// <summary>
		/// Finds a walkable ground position along the player's aim, falling back to the ground
		/// below a point at the requested horizontal distance when the aim ray points into the air.
		/// </summary>
		public bool CastEmptyGround(float distance, out Vector3 targetPosition)
		{
			Vector3 origin = GetCastOrigin();
			Vector3 aimDirection = Quaternion.Euler(GetCastAngle()) * Vector3.forward;
			float rayDistance = Mathf.Max(0f, distance);

			if (TryFindWalkableSurface(origin, aimDirection, rayDistance, out RaycastHit aimHit))
			{
				targetPosition = aimHit.point;
				return true;
			}

			Vector3 flatDirection = GetFlatAimDirection();
			Vector3 groundProbePoint = origin + flatDirection * rayDistance;
			float probeHeight = Mathf.Max(100f, Mathf.Abs(origin.y) + 10f);
			Vector3 probeOrigin = groundProbePoint + Vector3.up * probeHeight;

			if (TryFindWalkableSurface(probeOrigin, Vector3.down, probeHeight * 2f, out RaycastHit groundHit))
			{
				targetPosition = groundHit.point;
				return true;
			}

			targetPosition = groundProbePoint;
			return false;
		}

		/// <summary>Returns the player's aim direction projected onto the horizontal plane.</summary>
		public Vector3 GetFlatAimDirection()
		{
			Vector3 direction = Vector3.ProjectOnPlane(
				Quaternion.Euler(GetCastAngle()) * Vector3.forward,
				Vector3.up);

			if (direction.sqrMagnitude < 0.0001f)
			{
				direction = Controller != null
					? Vector3.ProjectOnPlane(Controller.transform.forward, Vector3.up)
					: Vector3.forward;
			}

			return direction.normalized;
		}

		private bool TryFindWalkableSurface(Vector3 origin, Vector3 direction, float distance, out RaycastHit nearestHit)
		{
			nearestHit = default;
			if (distance <= 0f || direction.sqrMagnitude < 0.0001f)
				return false;

			RaycastHit[] hits = Physics.RaycastAll(
				origin,
				direction.normalized,
				distance,
				Physics.DefaultRaycastLayers,
				QueryTriggerInteraction.Ignore);

			Transform playerRoot = Controller != null ? Controller.transform.root : null;
			float nearestDistance = float.MaxValue;
			bool foundSurface = false;

			foreach (RaycastHit hit in hits)
			{
				if (hit.collider == null || hit.normal.y < 0.5f ||
					(playerRoot != null && hit.collider.transform.root == playerRoot))
				{
					continue;
				}

				if (hit.distance >= nearestDistance)
					continue;

				nearestDistance = hit.distance;
				nearestHit = hit;
				foundSurface = true;
			}

			return foundSurface;
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
