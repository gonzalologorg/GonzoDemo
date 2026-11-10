using UnityEngine;

namespace GonzoDemo.AI
{
    [CreateAssetMenu(menuName = "Minions/Abilities/Self Heal")]
    public class MinionHealAbility : MinionAbility
    {
        [Min(0)] public float Healing = 25;
        public override bool CanUse(Minion unit, GameObject target) => unit.GetStat(MinionStat.Health) < unit.GetStat(MinionStat.MaxHealth);
        public override void Execute(Minion unit, GameObject target) => unit.SetHealth(unit.GetStat(MinionStat.Health) + Healing);
    }
}
