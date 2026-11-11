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
            MaxChakra,
            AttackSpeed
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
                SetStat(StatType.AttackSpeed, defaultStats != null ? defaultStats.attackSpeed : 1f);
                SetHealth(Mathf.RoundToInt(GetStat(StatType.MaxHealth)));
                SetChakra(Mathf.RoundToInt(GetStat(StatType.MaxChakra)));
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

        public int GetHealth() => Mathf.RoundToInt(GetStat(StatType.Health));

        public void SetHealth(int amount)
        {
            if (!IsServer)
                return;

            int maximum = Mathf.Max(0, Mathf.FloorToInt(GetStat(StatType.MaxHealth)));
            SetNetworkStat(StatType.Health, Mathf.Clamp(amount, 0, maximum));
        }

        public void AddHealth(int amount)
        {
            long target = (long)GetHealth() + amount;
            int maximum = Mathf.Max(0, Mathf.FloorToInt(GetStat(StatType.MaxHealth)));
            SetHealth((int)Math.Min(maximum, Math.Max(0L, target)));
        }

        public int GetChakra() => Mathf.RoundToInt(GetStat(StatType.Chakra));

        public void SetChakra(int amount)
        {
            if (!IsServer)
                return;

            int maximum = Mathf.Max(0, Mathf.FloorToInt(GetStat(StatType.MaxChakra)));
            SetNetworkStat(StatType.Chakra, Mathf.Clamp(amount, 0, maximum));
        }

        public void AddChakra(int amount)
        {
            long target = (long)GetChakra() + amount;
            int maximum = Mathf.Max(0, Mathf.FloorToInt(GetStat(StatType.MaxChakra)));
            SetChakra((int)Math.Min(maximum, Math.Max(0L, target)));
        }

        public void SetStat(StatType type, float value)
        {
            if (!IsServer) return;
            if (float.IsNaN(value) || float.IsInfinity(value)) return;
            if (type == StatType.Health || type == StatType.Chakra)
            {
                Debug.LogWarning($"Use SetHealth or SetChakra for {type}.", this);
                return;
            }
            if (type == StatType.MaxHealth || type == StatType.MaxChakra || type == StatType.AttackSpeed)
                value = Mathf.Max(0f, value);

            SetNetworkStat(type, value);
            if (type == StatType.MaxHealth && GetHealth() > value)
                SetHealth(Mathf.FloorToInt(value));
            else if (type == StatType.MaxChakra && GetChakra() > value)
                SetChakra(Mathf.FloorToInt(value));
        }

        public bool TryUseChakra(float amount)
        {
            if (!IsServer || !IsSpawned || float.IsNaN(amount) || float.IsInfinity(amount) || amount < 0f)
                return false;
            int chakraCost = Mathf.CeilToInt(amount);
            int chakra = GetChakra();
            if (chakra < chakraCost) return false;
            if (chakraCost > 0) SetChakra(chakra - chakraCost);
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
