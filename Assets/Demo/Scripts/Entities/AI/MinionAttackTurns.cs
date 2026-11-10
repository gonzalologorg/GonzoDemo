using UnityEngine;

namespace GonzoDemo.AI
{
    /// <summary>Lives on the target, so despawning the target also removes its coordination state.</summary>
    public class MinionAttackTurns : MonoBehaviour
    {
        private Minion holder;
        private float availableAt;
        public bool TryReserve(Minion unit, float duration)
        {
            if (holder != null && !holder.CanAct) { holder = null; availableAt = 0; }
            if (Time.time < availableAt) return false;
            holder = unit;
            availableAt = Time.time + duration;
            return true;
        }
        public void Cancel(Minion unit)
        {
            if (holder != unit) return;
            holder = null;
            availableAt = Time.time + 0.25f;
        }
        public void Complete(Minion unit, float recovery)
        {
            if (holder != unit) return;
            holder = null;
            availableAt = Time.time + recovery;
        }
    }
}
