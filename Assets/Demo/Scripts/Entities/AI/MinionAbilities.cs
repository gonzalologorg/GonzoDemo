using Unity.Netcode;
using UnityEngine;

namespace GonzoDemo.AI
{
    [RequireComponent(typeof(Minion))]
    public class MinionAbilities : NetworkBehaviour
    {
        [SerializeField] private MinionAbility[] catalog = new MinionAbility[0];
        [SerializeField, Min(1)] private int slotCount = 4;
        private NetworkList<int> slots = new NetworkList<int>();
        private NetworkList<float> readyAt = new NetworkList<float>();
        private Minion unit;
        private bool executing;
        private void Awake() => unit = GetComponent<Minion>();
        public int SlotCount => slots.Count;
        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            slots.Clear(); readyAt.Clear();
            for (int i = 0; i < slotCount; i++) { slots.Add(-1); readyAt.Add(0); }
        }
        public MinionAbility GetAbility(int slot) => slot >= 0 && slot < slots.Count && slots[slot] >= 0 && slots[slot] < catalog.Length ? catalog[slots[slot]] : null;
        public float CooldownRemaining(int slot) => slot >= 0 && slot < readyAt.Count ? Mathf.Max(0, readyAt[slot] - (float)NetworkManager.ServerTime.Time) : 0;
        /// <summary>Catalog indices must match on every peer. -1 clears the slot.</summary>
        public bool Grant(int slot, int catalogIndex)
        {
            if (!unit.CanAct || executing || slot < 0 || slot >= slots.Count || catalogIndex < -1 || catalogIndex >= catalog.Length) return false;
            slots[slot] = catalogIndex;
            // Preserve cooldown when replacing a slot, preventing equip/unequip cooldown bypasses.
            return true;
        }
        public bool TryUse(int slot, GameObject target)
        {
            var ability = GetAbility(slot);
            if (!unit.CanAct || executing || ability == null || CooldownRemaining(slot) > 0 ||
                unit.GetStat(MinionStat.Chakra) < ability.ChakraCost || !ability.CanUse(unit, target)) return false;
            executing = true;
            try
            {
                readyAt[slot] = (float)NetworkManager.ServerTime.Time + Mathf.Max(0, ability.Cooldown);
                unit.SetChakra(unit.GetStat(MinionStat.Chakra) - Mathf.Max(0, ability.ChakraCost));
                ability.Execute(unit, target);
            }
            finally { executing = false; }
            return true;
        }
    }
}
