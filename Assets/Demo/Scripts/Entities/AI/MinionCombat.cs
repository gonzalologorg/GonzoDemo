using System;
using GonzoDemo;
using UnityEngine;

namespace GonzoDemo.AI
{
    public static class MinionCombat
    {
        public static event Action<GameObject, GameObject, float> DamageDealt;
        public static bool IsAlive(GameObject target)
        {
            if (target == null || !target.activeInHierarchy) return false;
            var minion = target.GetComponent<Minion>();
            if (minion != null) return minion.IsSpawned && !minion.IsDead;
            var player = target.GetComponent<PlayerStats>();
            return player != null && player.IsSpawned && player.GetHealth() > 0;
        }
        public static void ReportDamage(GameObject victim, GameObject attacker, float amount)
        {
            if (amount > 0) DamageDealt?.Invoke(victim, attacker, amount);
        }
        public static float Apply(GameObject target, GameObject attacker, float amount, DamageType type)
        {
            if (!IsAlive(target)) return 0;
            var info = new DamageInfo(amount, attacker, attacker, type, Vector3.zero, target.transform.position, null);
            var minion = target.GetComponent<Minion>();
            return minion != null ? minion.ReceiveDamage(info) : DamageResolver.Apply(target.GetComponent<PlayerStats>(), info);
        }
        public static bool Friendly(Minion unit, GameObject other)
        {
            if (other == unit.gameObject || other == unit.Owner) return true;
            var minion = other != null ? other.GetComponent<Minion>() : null;
            return minion != null && unit.Owner != null && minion.Owner == unit.Owner;
        }
    }
}
