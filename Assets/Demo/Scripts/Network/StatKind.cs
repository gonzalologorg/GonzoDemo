using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public struct StatKind : INetworkSerializable,
                          IEquatable<StatKind>
{
    public PlayerStats.StatType statType;
    public float value;

    public StatKind(PlayerStats.StatType statType, float value)
    {
        this.statType = statType;
        this.value = value;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref statType);
        serializer.SerializeValue(ref value);
    }

    public bool Equals(StatKind other)
    {
        return statType == other.statType && value == other.value;
    }
}
