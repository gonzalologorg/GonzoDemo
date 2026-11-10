using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace GonzoDemo.AI
{
    /// <summary>Dedicated trigger child avoids confusing the unit's body contacts with perception.</summary>
    public class MinionSensor : MonoBehaviour
    {
        [Min(0.1f)] public float Radius = 10;
        public LayerMask Layers = ~0;
        private Minion unit;
        private SphereCollider trigger;
        private readonly Dictionary<Collider, GameObject> contacts = new Dictionary<Collider, GameObject>();
        private readonly HashSet<GameObject> nearby = new HashSet<GameObject>();
        private readonly List<Collider> stale = new List<Collider>();
        public IEnumerable<GameObject> Nearby => nearby;
        private void Awake()
        {
            unit = GetComponent<Minion>();
            var child = new GameObject("Perception");
            child.layer = 2; // Ignore Raycast: perception must not become an oversized hurtbox.
            child.transform.SetParent(transform, false);
            trigger = child.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = Radius;
            child.AddComponent<MinionSensorRelay>().Sensor = this;
        }
        private void Update()
        {
            trigger.enabled = unit.CanAct && enabled;
            trigger.radius = Mathf.Max(0.1f, Radius);
            stale.Clear();
            foreach (var pair in contacts)
                if (pair.Key == null || !pair.Key.enabled || !pair.Key.gameObject.activeInHierarchy || !MinionCombat.IsAlive(pair.Value))
                    stale.Add(pair.Key);
            foreach (var collider in stale) Exit(collider);
        }
        internal void Enter(Collider other)
        {
            if (!enabled || !unit.CanAct || other.GetComponent<MinionSensorRelay>() != null ||
                (Layers.value & (1 << other.gameObject.layer)) == 0 || contacts.ContainsKey(other)) return;
            var network = other.GetComponentInParent<NetworkObject>();
            if (network == null || network.gameObject == gameObject || !MinionCombat.IsAlive(network.gameObject)) return;
            contacts.Add(other, network.gameObject);
            if (nearby.Add(network.gameObject)) unit.NotifyUnit(network.gameObject, true);
        }
        internal void Exit(Collider other)
        {
            if (!contacts.TryGetValue(other, out var target)) return;
            contacts.Remove(other);
            if (contacts.ContainsValue(target)) return;
            nearby.Remove(target);
            unit.NotifyUnit(target, false);
        }
        private void OnDisable()
        {
            if (trigger != null) trigger.enabled = false;
            foreach (var target in nearby) unit.NotifyUnit(target, false);
            contacts.Clear();
            nearby.Clear();
        }
    }
}
