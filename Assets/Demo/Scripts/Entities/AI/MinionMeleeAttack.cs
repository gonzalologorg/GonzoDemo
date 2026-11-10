using System.Collections.Generic;
using GonzoDemo;
using Unity.Netcode;
using UnityEngine;

namespace GonzoDemo.AI
{
    public class MinionMeleeAttack : MinionAttack
    {
        [Min(0.1f)] public float AreaRadius = 1.25f;
        public LayerMask HitLayers = ~0;
        protected override void PerformAttack(GameObject target)
        {
            var victims = new HashSet<GameObject>();
            victims.Add(target);
            foreach (var collider in Physics.OverlapSphere(target.transform.position, AreaRadius, HitLayers, QueryTriggerInteraction.Ignore))
            {
                var network = collider.GetComponentInParent<NetworkObject>();
                if (network != null) victims.Add(network.gameObject);
            }
            foreach (var victim in victims)
            {
                // Unowned enemies do not damage each other with their area attack.
                if (MinionCombat.Friendly(Unit, victim)) continue;
                if (victim != target && Unit.Owner == null && victim.GetComponent<Minion>() != null) continue;
                MinionCombat.Apply(victim, gameObject, Damage, DamageType.Physical);
            }
        }
    }
}
