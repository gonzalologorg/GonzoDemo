using System;
using System.Collections;
using GonzoDemo;
using GonzoDemo.Combat;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace GonzoDemo.AI
{
    public enum MinionStat { Health, MaxHealth, Chakra, MaxChakra, Speed, AttackSpeed, Armor, MagicResistance }

    /// <summary>Server-controlled unit. Target selection and combat belong to optional components.</summary>
    [RequireComponent(typeof(NetworkObject), typeof(NetworkTransform), typeof(NavMeshAgent))]
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    [RequireComponent(typeof(MinionSensor))]
    [DisallowMultipleComponent]
    public class Minion : NetworkBehaviour, IHittable
    {
        [SerializeField, Min(1)] private float initialHealth = 100;
        [SerializeField, Min(0)] private float initialChakra = 100;
        [SerializeField, Min(0)] private float initialSpeed = 3.5f;
        [SerializeField, Min(0)] private float initialAttackSpeed = 1;
        [SerializeField, Min(0)] private float initialArmor;
        [SerializeField, Min(0)] private float initialMagicResistance;
        [SerializeField, Min(0)] private float deathDelay = 1;
        [SerializeField, Min(0)] private float positioningRadius = 2;
        [SerializeField] private UnityEvent onDeath = new UnityEvent();
        private NetworkList<float> stats = new NetworkList<float>();
        private static NetworkObjectReference NoTarget => new NetworkObjectReference((NetworkObject)null);
        private readonly NetworkVariable<NetworkObjectReference> enemy = new NetworkVariable<NetworkObjectReference>(NoTarget);
        private readonly NetworkVariable<NetworkObjectReference> owner = new NetworkVariable<NetworkObjectReference>(NoTarget);
        private readonly NetworkVariable<bool> dead = new NetworkVariable<bool>();
        public NavMeshAgent Agent { get; private set; }
        public GameObject Enemy => Resolve(enemy.Value);
        public GameObject Owner => Resolve(owner.Value);
        public bool IsDead => dead.Value;
        public bool CanAct => IsSpawned && IsServer && !IsDead;
        public event Action<MinionStat, float> StatChanged;
        public event Action<Minion> Died;
        public event Action<GameObject> UnitEntered;
        public event Action<GameObject> UnitExited;

        protected virtual void Awake()
        {
            Agent = GetComponent<NavMeshAgent>();
            var body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            Agent.enabled = false;
        }

        public override void OnNetworkSpawn()
        {
            stats.OnListChanged += StatsChanged;
            dead.OnValueChanged += DeathChanged;
            Agent.enabled = IsServer;
            if (IsServer)
            {
                dead.Value = false;
                enemy.Value = NoTarget;
                owner.Value = NoTarget;
                stats.Clear();
                foreach (float value in new[] { initialHealth, initialHealth, initialChakra, initialChakra,
                    initialSpeed, initialAttackSpeed, initialArmor, initialMagicResistance })
                    stats.Add(Mathf.Max(0, value));
                Agent.speed = GetStat(MinionStat.Speed);
            }
            if (IsDead) onDeath.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            stats.OnListChanged -= StatsChanged;
            dead.OnValueChanged -= DeathChanged;
            StopAllCoroutines();
            Agent.enabled = false;
        }

        private GameObject Resolve(NetworkObjectReference reference) =>
            IsSpawned && reference.TryGet(out NetworkObject value, NetworkManager) ? value.gameObject : null;
        public float GetStat(MinionStat stat) => (int)stat >= 0 && (int)stat < stats.Count ? stats[(int)stat] : 0;

        /// <summary>Call on the server; values are replicated to every observer.</summary>
        public void SetStat(MinionStat stat, float value)
        {
            if (!CanAct || (int)stat < 0 || (int)stat >= stats.Count || !Finite(value)) return;
            value = Mathf.Max(0, value);
            if (stat == MinionStat.Health) value = Mathf.Min(value, GetStat(MinionStat.MaxHealth));
            if (stat == MinionStat.Chakra) value = Mathf.Min(value, GetStat(MinionStat.MaxChakra));
            stats[(int)stat] = value;
            if (stat == MinionStat.MaxHealth) SetHealth(GetStat(MinionStat.Health));
            if (stat == MinionStat.MaxChakra) SetChakra(GetStat(MinionStat.Chakra));
            if (stat == MinionStat.Health && value <= 0) Kill();
        }
        public void SetHealth(float value) => SetStat(MinionStat.Health, value);
        public void SetMaxHealth(float value) => SetStat(MinionStat.MaxHealth, value);
        public void SetChakra(float value) => SetStat(MinionStat.Chakra, value);
        public void SetMaxChakra(float value) => SetStat(MinionStat.MaxChakra, value);
        public void SetSpeed(float value) => SetStat(MinionStat.Speed, value);
        public void SetAttackSpeed(float value) => SetStat(MinionStat.AttackSpeed, value);
        public void SetArmor(float value) => SetStat(MinionStat.Armor, value);
        public void SetMagicResistance(float value) => SetStat(MinionStat.MagicResistance, value);
        public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private void StatsChanged(NetworkListEvent<float> change)
        {
            if (IsServer && Agent.enabled) Agent.speed = GetStat(MinionStat.Speed);
            if (change.Index >= 0 && change.Index < stats.Count)
                StatChanged?.Invoke((MinionStat)change.Index, stats[change.Index]);
        }
        public bool MoveTo(Vector3 point)
        {
            if (!CanAct || !Finite(point.x) || !Finite(point.y) || !Finite(point.z) ||
                !Agent.enabled || !Agent.isOnNavMesh) return false;
            if (!NavMesh.SamplePosition(point, out NavMeshHit hit, 2f, Agent.areaMask)) return false;
            Agent.isStopped = false;
            return Agent.SetDestination(hit.position);
        }
        public void TargetTo(Vector3 point) => TargetTo(point, positioningRadius);
        public void TargetTo(Vector3 point, float radius)
        {
            Vector3 direction = transform.position - point;
            direction.y = 0;
            if (direction.sqrMagnitude < 0.01f) direction = transform.forward;
            MoveTo(point + direction.normalized * Mathf.Max(0, radius));
        }
        public void StopMoving()
        {
            if (IsServer && Agent.enabled && Agent.isOnNavMesh) { Agent.ResetPath(); Agent.isStopped = true; }
        }
        public void SetEnemy(GameObject target)
        {
            if (!CanAct || (target != null && (!MinionCombat.IsAlive(target) || target == gameObject || target == Owner))) return;
            if (TryReference(target, out var reference)) { enemy.Value = reference; StopMoving(); }
        }
        public void SetOwner(GameObject target)
        {
            if (!CanAct || target == gameObject) return;
            if (TryReference(target, out var reference))
            {
                owner.Value = reference;
                if (Enemy == Owner) enemy.Value = NoTarget;
                StopMoving();
            }
        }
        private static bool TryReference(GameObject target, out NetworkObjectReference reference)
        {
            reference = NoTarget;
            if (target == null) return true;
            var network = target.GetComponentInParent<NetworkObject>();
            if (network == null || !network.IsSpawned) return false;
            reference = new NetworkObjectReference(network);
            return true;
        }
        internal void NotifyUnit(GameObject unit, bool entered)
        {
            if (!CanAct) return;
            if (entered) UnitEntered?.Invoke(unit); else UnitExited?.Invoke(unit);
        }
        public float ReceiveDamage(in DamageInfo info)
        {
            if (!CanAct || !Finite(info.Damage) || info.Damage <= 0) return 0;
            float defense = info.Type == DamageType.Physical ? GetStat(MinionStat.Armor) :
                info.Type == DamageType.Magical ? GetStat(MinionStat.MagicResistance) : 0;
            float before = GetStat(MinionStat.Health);
            float removed = Mathf.Min(before, info.Damage * (100f / (100f + defense)));
            // Notify before Kill so pets can observe the attacker even on a lethal hit.
            MinionCombat.ReportDamage(gameObject, info.Attacker, removed);
            SetHealth(before - removed);
            return removed;
        }
        public void OnHit(HitInfo info)
        {
            if (!CanAct) return;
            GameObject attacker = null;
            if (NetworkManager.ConnectedClients.TryGetValue(info.attackerId, out var client) && client.PlayerObject != null)
                attacker = client.PlayerObject.gameObject;
            ReceiveDamage(new DamageInfo(info.amount, attacker, attacker, DamageType.Physical,
                info.impactForce, info.hitPoint, null));
        }
        public void Kill()
        {
            if (!CanAct) return;
            stats[(int)MinionStat.Health] = 0;
            dead.Value = true;
            StopMoving();
            enemy.Value = NoTarget;
            Died?.Invoke(this);
            StartCoroutine(DespawnAfterDeath());
        }
        private void DeathChanged(bool previous, bool current) { if (current) onDeath.Invoke(); }
        private IEnumerator DespawnAfterDeath()
        {
            yield return new WaitForSeconds(deathDelay);
            if (IsSpawned && IsServer) NetworkObject.Despawn(true);
        }
    }
}
