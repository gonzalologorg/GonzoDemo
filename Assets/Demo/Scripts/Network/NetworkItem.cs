using System;
using Unity.Collections;
using Unity.Netcode;

public struct NetworkItem : INetworkSerializable,
                            IEquatable<NetworkItem>
{
    public int Slot;
    public FixedString64Bytes ItemId;
    public int Amount;

    public NetworkItem(int slot, FixedString64Bytes itemId, int amount)
    {
        Slot = slot;
        ItemId = itemId;
        Amount = amount;
    }

    public void NetworkSerialize<T>(
        BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref Slot);
        serializer.SerializeValue(ref ItemId);
        serializer.SerializeValue(ref Amount);
    }

    public bool Equals(NetworkItem other)
    {
        return Slot == other.Slot &&
               ItemId.Equals(other.ItemId) &&
               Amount == other.Amount;
    }
}