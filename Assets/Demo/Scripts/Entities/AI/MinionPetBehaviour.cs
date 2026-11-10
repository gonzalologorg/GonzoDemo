using UnityEngine;

namespace GonzoDemo.AI
{
    [RequireComponent(typeof(Minion))]
    public class MinionPetBehaviour : MonoBehaviour
    {
        [Min(0.5f)] public float FollowDistance = 3;
        [Min(1)] public float CombatLeash = 20;
        private Minion unit;
        private void Awake() => unit = GetComponent<Minion>();
        private void OnEnable() => MinionCombat.DamageDealt += OnDamage;
        private void OnDisable() => MinionCombat.DamageDealt -= OnDamage;
        private void OnDamage(GameObject victim, GameObject attacker, float amount)
        {
            if (!unit.CanAct || unit.Owner == null) return;
            GameObject candidate = victim == unit.Owner ? attacker : attacker == unit.Owner ? victim : null;
            if (!MinionCombat.IsAlive(candidate) || MinionCombat.Friendly(unit, candidate) ||
                Vector3.Distance(unit.Owner.transform.position, candidate.transform.position) > CombatLeash) return;
            if (!MinionCombat.IsAlive(unit.Enemy)) unit.SetEnemy(candidate);
        }
        private void Update()
        {
            if (!unit.CanAct || unit.Owner == null) return;
            if (!MinionCombat.IsAlive(unit.Owner)) { unit.SetEnemy(null); unit.SetOwner(null); return; }
            if (unit.Enemy != null && (!MinionCombat.IsAlive(unit.Enemy) ||
                Vector3.Distance(unit.Owner.transform.position, unit.Enemy.transform.position) > CombatLeash ||
                Vector3.Distance(unit.Owner.transform.position, transform.position) > CombatLeash)) unit.SetEnemy(null);
            if (unit.Enemy != null) return;
            // Join a fight already in progress even if the pet was assigned after the initial hit.
            foreach (var candidate in GetComponent<MinionSensor>().Nearby)
            {
                var enemy = candidate != null ? candidate.GetComponent<Minion>() : null;
                if (enemy != null && enemy.Enemy == unit.Owner && MinionCombat.IsAlive(candidate) &&
                    !MinionCombat.Friendly(unit, candidate) && Vector3.Distance(unit.Owner.transform.position, candidate.transform.position) <= CombatLeash)
                { unit.SetEnemy(candidate); return; }
            }
            if (Vector3.Distance(transform.position, unit.Owner.transform.position) > FollowDistance)
                unit.TargetTo(unit.Owner.transform.position, FollowDistance * 0.8f);
            else unit.StopMoving();
        }
    }
}
