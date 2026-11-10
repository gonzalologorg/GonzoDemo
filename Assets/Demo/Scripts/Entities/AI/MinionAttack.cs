using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

namespace GonzoDemo.AI
{
    /// <summary>Attach one attack implementation to the network prefab.</summary>
    [RequireComponent(typeof(Minion))]
    [DisallowMultipleComponent]
    public abstract class MinionAttack : NetworkBehaviour
    {
        [Min(0.1f)] public float Range = 2;
        [Min(0)] public float Damage = 10;
        [Min(0)] public float Windup = 0.5f;
        [Min(0.1f)] public float GroupRecovery = 0.75f;
        public UnityEvent OnWindup = new UnityEvent();
        public UnityEvent OnAttack = new UnityEvent();
        protected Minion Unit;
        private GameObject pendingTarget;
        private MinionAttackTurns turns;
        private float strikeAt;
        private float nextAttack;
        protected virtual float TravelTime => 0;
        protected virtual void Awake() => Unit = GetComponent<Minion>();
        public override void OnNetworkSpawn() { nextAttack = Time.time + Random.Range(0, 0.3f); }
        protected virtual void Update()
        {
            if (!Unit.CanAct) { Cancel(); return; }
            var target = Unit.Enemy;
            if (!MinionCombat.IsAlive(target) || MinionCombat.Friendly(Unit, target))
            {
                Cancel();
                if (target != null) Unit.SetEnemy(null);
                return;
            }
            float distance = Vector3.Distance(transform.position, target.transform.position);
            float speed = Unit.GetStat(MinionStat.AttackSpeed);
            if (pendingTarget != null)
            {
                if (target != pendingTarget || distance > Range || speed <= 0) { Cancel(); return; }
                Unit.StopMoving();
                if (Time.time < strikeAt) return;
                PerformAttack(target);
                AttackRpc(false);
                turns.Complete(Unit, GroupRecovery + TravelTime);
                pendingTarget = null;
                turns = null;
                nextAttack = Time.time + 1f / speed + Random.Range(0, 0.2f);
                return;
            }
            if (distance > Range) { Unit.TargetTo(target.transform.position, Range * 0.8f); return; }
            Unit.StopMoving();
            if (speed <= 0 || Time.time < nextAttack) return;
            turns = target.GetComponent<MinionAttackTurns>() ?? target.AddComponent<MinionAttackTurns>();
            if (!turns.TryReserve(Unit, Windup + 0.1f))
            {
                nextAttack = Time.time + Random.Range(0.1f, 0.3f);
                return;
            }
            pendingTarget = target;
            strikeAt = Time.time + Windup;
            AttackRpc(true);
        }
        [Rpc(SendTo.ClientsAndHost)]
        private void AttackRpc(bool preparing)
        {
            if (preparing) OnWindup.Invoke(); else OnAttack.Invoke();
        }
        protected abstract void PerformAttack(GameObject target);
        private void Cancel()
        {
            if (turns != null && pendingTarget != null) turns.Cancel(Unit);
            pendingTarget = null;
            turns = null;
        }
        protected virtual void OnDisable() => Cancel();
        public override void OnNetworkDespawn() => Cancel();
    }
}
