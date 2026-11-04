using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace GonzoDemo
{
    public class PlayerStats : NetworkBehaviour
    {
        public enum StatType
        {
            Health,
            Cooldown,
            Damage,
            Speed,
            Armor,
            SpecialArmor,
            AbilityDamage,
            MaxHealth,
            Chakra,
            MaxChakra
        }

        public DefaultStats defaultStats;
        private readonly NetworkVariable<int> networkLevel = new NetworkVariable<int>(1);
        private int experience;
        private int experienceLevel = 1;

        public int Level => networkLevel.Value;
        // Experience toward the next level, excluding experience already spent on levels.
        public int Experience => experience;
        public int ExperienceToNextLevel => GetExperienceRequired(experienceLevel);
        public event Action<int> OnLevelChanged;
        public event Action<int> OnExperienceChanged;
        private NetworkList<StatKind> networkStats =
            new NetworkList<StatKind>(
                null,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server
            );

        private readonly Dictionary<StatType, float> stats = new Dictionary<StatType, float>();
        public event Action<StatType, float> OnStatChanged;

        public override void OnNetworkSpawn()
        {
            networkLevel.OnValueChanged += HandleLevelChanged;
            HandleLevelChanged(Level, Level);
            OnExperienceChanged?.Invoke(Experience);

            networkStats.OnListChanged += HandleStatsChanged;
            if (IsServer)
            {
                SetStat(StatType.MaxHealth, defaultStats != null ? defaultStats.health : 100f);
                SetStat(StatType.MaxChakra, defaultStats != null ? defaultStats.chakra : 100f);
                SetStat(StatType.Health, GetStat(StatType.MaxHealth));
                SetStat(StatType.Chakra, GetStat(StatType.MaxChakra));
            }
            if (IsServer && defaultStats != null)
            {
                SetStat(StatType.Cooldown, defaultStats.cooldown);
                SetStat(StatType.Damage, defaultStats.damage);
                SetStat(StatType.Speed, defaultStats.speed);
                SetStat(StatType.Armor, defaultStats.armor);
                SetStat(StatType.SpecialArmor, defaultStats.specialArmor);
                SetStat(StatType.AbilityDamage, defaultStats.abilityDamage);
            }

            RebuildCache();
        }

        public override void OnNetworkDespawn()
        {
            networkLevel.OnValueChanged -= HandleLevelChanged;
            networkStats.OnListChanged -= HandleStatsChanged;
        }

        public static int GetExperienceRequired(int level)
        {
            double required = Math.Ceiling(100d + Math.Pow(50d * (Math.Max(1, level) - 1d), 1.25d));
            return (int)Math.Min(int.MaxValue, required);
        }

        public void AddExperience(int amount)
        {
            if (!IsServer || !IsSpawned || amount <= 0) return;

            ApplyExperience(amount);
            networkLevel.Value = experienceLevel;
            // Notify only the collector. Accumulated XP is not replicated to other players.
            AwardExperienceClientRpc(amount, new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
            });
        }

        [ClientRpc]
        private void AwardExperienceClientRpc(int amount, ClientRpcParams rpcParams = default)
        {
            // The host already applied the award on the server path.
            if (IsOwner && !IsServer) ApplyExperience(amount);
        }

        private void ApplyExperience(int amount)
        {
            long remaining = (long)Experience + amount;
            int level = experienceLevel;
            while (level < int.MaxValue && remaining >= GetExperienceRequired(level))
            {
                remaining -= GetExperienceRequired(level);
                level++;
            }

            experience = (int)Math.Min(int.MaxValue, remaining);
            experienceLevel = level;
            OnExperienceChanged?.Invoke(Experience);
        }

        private void HandleLevelChanged(int previous, int current)
        {
            OnLevelChanged?.Invoke(current);
        }

        public float GetStat(StatType type)
        {
            if (stats.TryGetValue(type, out float value))
            {
                return value;
            }
            return 0f;
        }

        public void SetStat(StatType type, float value)
        {
            if (!IsServer) return;
            if (float.IsNaN(value) || float.IsInfinity(value)) return;
            if (type == StatType.MaxHealth || type == StatType.MaxChakra)
                value = Mathf.Max(0f, value);
            else if (type == StatType.Health)
                value = Mathf.Clamp(value, 0f, GetStat(StatType.MaxHealth));
            else if (type == StatType.Chakra)
                value = Mathf.Clamp(value, 0f, GetStat(StatType.MaxChakra));

            SetNetworkStat(type, value);
            if (type == StatType.MaxHealth && GetStat(StatType.Health) > value)
                SetStat(StatType.Health, value);
            else if (type == StatType.MaxChakra && GetStat(StatType.Chakra) > value)
                SetStat(StatType.Chakra, value);
        }

        public bool TryUseChakra(float amount)
        {
            if (!IsServer || !IsSpawned || float.IsNaN(amount) || float.IsInfinity(amount) || amount < 0f)
                return false;
            float chakra = GetStat(StatType.Chakra);
            if (chakra < amount) return false;
            if (amount > 0f) SetStat(StatType.Chakra, chakra - amount);
            return true;
        }

        private void SetNetworkStat(StatType type, float value)
        {

            for (int i = 0; i < networkStats.Count; i++)
            {
                if (networkStats[i].statType == type)
                {
                    networkStats[i] = new StatKind(type, value);
                    return;
                }
            }

            networkStats.Add(new StatKind(type, value));
        }

        private void HandleStatsChanged(NetworkListEvent<StatKind> change)
        {
            RebuildCache();
        }

        private void RebuildCache()
        {
            stats.Clear();

            foreach (StatKind entry in networkStats)
            {
                stats[entry.statType] = entry.value;
            }
            // Populate the whole cache before notifying listeners that read current/max pairs.
            foreach (StatKind entry in networkStats)
            {
                OnStatChanged?.Invoke(entry.statType, entry.value);
            }
        }

        public float CalculateDamage(float damage, bool isMagic)
        {
            float reductor = isMagic ? GetStat(StatType.SpecialArmor) : GetStat(StatType.Armor);
            damage *= (100 / (100 + reductor));
            return damage;
        }
    }

}
