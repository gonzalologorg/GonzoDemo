using System.Collections.Generic;
using UnityEngine;

namespace GonzoDemo.AI
{
    /// <summary>Timed additive modifiers. SetBaseStat changes the underlying value while buffs are active.</summary>
    [RequireComponent(typeof(Minion))]
    public class MinionBuffs : MonoBehaviour
    {
        private sealed class Buff { public int Id; public MinionStat Stat; public float Amount; public float Until; }
        private readonly List<Buff> active = new List<Buff>();
        private readonly Dictionary<MinionStat, float> bases = new Dictionary<MinionStat, float>();
        private Minion unit;
        private int nextId;
        private void Awake() => unit = GetComponent<Minion>();
        public int Add(MinionStat stat, float amount, float seconds)
        {
            if (!unit.CanAct || !Minion.Finite(amount) || !Minion.Finite(seconds) || seconds <= 0 ||
                stat < MinionStat.Speed || stat > MinionStat.MagicResistance) return -1;
            if (!bases.ContainsKey(stat)) bases.Add(stat, unit.GetStat(stat));
            var buff = new Buff { Id = ++nextId, Stat = stat, Amount = amount, Until = Time.time + seconds };
            active.Add(buff); Recalculate(stat); return buff.Id;
        }
        public void SetBaseStat(MinionStat stat, float value)
        {
            if (!unit.CanAct || !Minion.Finite(value)) return;
            if (bases.ContainsKey(stat)) { bases[stat] = Mathf.Max(0, value); Recalculate(stat); }
            else unit.SetStat(stat, value);
        }
        public void Remove(int id)
        {
            if (!unit.CanAct) return;
            int index = active.FindIndex(buff => buff.Id == id);
            if (index < 0) return;
            var stat = active[index].Stat;
            active.RemoveAt(index); Recalculate(stat);
            if (!active.Exists(buff => buff.Stat == stat)) bases.Remove(stat);
        }
        private void Recalculate(MinionStat stat)
        {
            float value = bases[stat];
            foreach (var buff in active) if (buff.Stat == stat) value += buff.Amount;
            unit.SetStat(stat, value);
        }
        private void Update()
        {
            if (!unit.IsSpawned || unit.IsDead) { active.Clear(); bases.Clear(); return; }
            if (!unit.CanAct) return;
            for (int i = active.Count - 1; i >= 0; i--) if (Time.time >= active[i].Until) Remove(active[i].Id);
        }
        private void OnDisable()
        {
            if (unit != null && unit.CanAct) foreach (var pair in bases) unit.SetStat(pair.Key, pair.Value);
            active.Clear(); bases.Clear();
        }
    }
}
