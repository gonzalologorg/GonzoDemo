using UnityEngine;
using Unity.Netcode;
[RequireComponent(typeof(NetworkObject))]
public class ExperienceBall : NetworkBehaviour
{
    [Min(1)] public int Amount = 10;
    [SerializeField, Min(0f)] private float attractionRadius = 3f;
    [SerializeField, Min(0.1f)] private float travelSpeed = 8f;
    [SerializeField, Min(0.01f)] private float absorbDistance = 0.15f;
    private readonly NetworkVariable<Vector3> flightPosition = new NetworkVariable<Vector3>();
    private GonzoDemo.PlayerStats target;
    private bool consumed;

    private void Awake()
    {
        var body = GetComponent<Rigidbody>();
        if (body == null) body = gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        // The original XPOrb prefab has no renderer; provide a visible orb by default.
        if (GetComponentInChildren<Renderer>(true) == null)
        {
            var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "XP Orb Visual";
            visual.transform.SetParent(transform, false);
            visual.transform.localScale = Vector3.one * 0.25f;
            var visualCollider = visual.GetComponent<Collider>();
            visualCollider.enabled = false;
            Destroy(visualCollider);
        }
    }

    public override void OnNetworkSpawn()
    {
        consumed = false;
        target = null;
        if (IsServer) flightPosition.Value = transform.position;
    }

    public override void OnNetworkDespawn()
    {
        target = null;
        gameObject.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || !IsSpawned || consumed || Amount <= 0) return;

        TargetTo(other.GetComponentInParent<GonzoDemo.PlayerStats>());
    }

    public void TargetTo(GameObject player) => TargetTo(player != null ? player.GetComponentInParent<GonzoDemo.PlayerStats>() : null);

    /// <summary>Server-only. The first valid player claims the orb until they die or disconnect.</summary>
    public void TargetTo(GonzoDemo.PlayerStats player)
    {
        if (!IsServer || !IsSpawned || consumed || Amount <= 0 || IsValidTarget(target) || !IsValidTarget(player)) return;
        target = player;
    }

    private static bool IsValidTarget(GonzoDemo.PlayerStats player) =>
        player != null && player.IsSpawned && player.gameObject.activeInHierarchy && player.GetHealth() > 0;

    private static Vector3 AbsorptionPoint(GonzoDemo.PlayerStats player)
    {
        var controller = player.GetComponent<CharacterController>();
        return controller != null ? controller.bounds.center : player.transform.position + Vector3.up;
    }

    private void Update()
    {
        if (!IsSpawned || consumed) return;
        if (!IsServer)
        {
            transform.position = Vector3.MoveTowards(transform.position, flightPosition.Value, travelSpeed * Time.deltaTime);
            return;
        }

        if (!IsValidTarget(target))
        {
            target = null;
            float nearestDistance = attractionRadius * attractionRadius;
            foreach (var client in NetworkManager.ConnectedClientsList)
            {
                var player = client.PlayerObject != null ? client.PlayerObject.GetComponent<GonzoDemo.PlayerStats>() : null;
                if (!IsValidTarget(player)) continue;
                float distance = (AbsorptionPoint(player) - transform.position).sqrMagnitude;
                if (distance <= nearestDistance)
                {
                    nearestDistance = distance;
                    target = player;
                }
            }
        }
        if (target == null || Amount <= 0) return;

        Vector3 destination = AbsorptionPoint(target);
        transform.position = Vector3.MoveTowards(transform.position, destination, travelSpeed * Time.deltaTime);
        flightPosition.Value = transform.position;
        if ((destination - transform.position).sqrMagnitude > absorbDistance * absorbDistance) return;

        consumed = true;
        target.AddExperience(Amount);
        NetworkObject.Despawn(true);
    }
}
