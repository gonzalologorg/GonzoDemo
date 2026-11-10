using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

namespace GonzoDemo.AI
{
    /// <summary>Drop prefabs can contain ItemSack, ExperienceBall or a currency pickup.</summary>
    [RequireComponent(typeof(Minion))]
    public class MinionRewards : MonoBehaviour
    {
        [Serializable]
        public class Drop
        {
            public NetworkObject Prefab;
            [Range(0, 1)] public float Chance = 1;
            [Min(1)] public int Count = 1;
        }
        public Drop[] Drops = new Drop[0];
        public UnityEvent OnReward = new UnityEvent();
        private Minion unit;
        private void Awake() => unit = GetComponent<Minion>();
        private void OnEnable() => unit.Died += Reward;
        private void OnDisable() => unit.Died -= Reward;
        private void Reward(Minion minion)
        {
            if (!minion.IsServer) return;
            foreach (var drop in Drops)
            {
                if (drop == null || drop.Prefab == null || UnityEngine.Random.value >= drop.Chance) continue;
                for (int i = 0; i < drop.Count; i++)
                {
                    Vector2 offset = UnityEngine.Random.insideUnitCircle * 0.5f;
                    var pickup = Instantiate(drop.Prefab, transform.position + new Vector3(offset.x, 0.2f, offset.y), Quaternion.identity);
                    pickup.Spawn();
                }
            }
            OnReward.Invoke();
        }
    }
}
