using GonzoDemo;
using UnityEngine;

namespace GonzoDemo.AI
{
    [RequireComponent(typeof(Minion))]
    public class MinionEnemyBehaviour : MonoBehaviour
    {
        [Min(1)] public float LeashRadius = 15;
        private Minion unit;
        private MinionSensor sensor;
        private Vector3 home;
        private bool initialized;
        private bool returning;
        private void Awake() { unit = GetComponent<Minion>(); sensor = GetComponent<MinionSensor>(); }
        private void Update()
        {
            if (!unit.IsSpawned) { initialized = false; return; }
            if (!unit.CanAct || unit.Owner != null) return;
            if (!initialized) { home = transform.position; initialized = true; returning = false; }
            if (returning)
            {
                if (Vector3.Distance(transform.position, home) > 1) { unit.MoveTo(home); return; }
                returning = false;
                unit.StopMoving();
            }
            if (unit.Enemy != null)
            {
                if (!MinionCombat.IsAlive(unit.Enemy) || Vector3.Distance(home, unit.Enemy.transform.position) > LeashRadius ||
                    Vector3.Distance(home, transform.position) > LeashRadius)
                {
                    unit.SetEnemy(null);
                    returning = true;
                    unit.MoveTo(home);
                }
                return;
            }
            GameObject closest = null;
            float distance = float.MaxValue;
            foreach (var candidate in sensor.Nearby)
            {
                if (!MinionCombat.IsAlive(candidate) || candidate.GetComponent<PlayerStats>() == null ||
                    Vector3.Distance(home, candidate.transform.position) > LeashRadius) continue;
                float squared = (candidate.transform.position - transform.position).sqrMagnitude;
                if (squared < distance) { distance = squared; closest = candidate; }
            }
            if (closest != null) unit.SetEnemy(closest);
        }
    }
}
