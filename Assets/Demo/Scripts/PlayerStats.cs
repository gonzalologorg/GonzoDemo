using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;


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
        AbilityDamage
    }

    public DefaultStats defaultStats;
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

        if (IsServer && defaultStats != null)
        {
            SetStat(StatType.Health, defaultStats.health);
            SetStat(StatType.Cooldown, defaultStats.cooldown);
            SetStat(StatType.Damage, defaultStats.damage);
            SetStat(StatType.Speed, defaultStats.speed);
            SetStat(StatType.Armor, defaultStats.armor);
            SetStat(StatType.SpecialArmor, defaultStats.specialArmor);
            SetStat(StatType.AbilityDamage, defaultStats.abilityDamage);
        }

        networkStats.OnListChanged += HandleStatsChanged;
        RebuildCache();
    }

    public override void OnNetworkDespawn()
    {
        networkStats.OnListChanged -= HandleStatsChanged;
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
