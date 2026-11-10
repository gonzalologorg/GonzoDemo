using UnityEngine;

namespace GonzoDemo.AI
{
    /// <summary>Shared immutable definition. Per-unit cooldowns live in MinionAbilities, never in this asset.</summary>
    public abstract class MinionAbility : ScriptableObject
    {
        [Min(0)] public float ChakraCost;
        [Min(0)] public float Cooldown = 1;
        public virtual bool CanUse(Minion unit, GameObject target) => true;
        public abstract void Execute(Minion unit, GameObject target);
    }
}
