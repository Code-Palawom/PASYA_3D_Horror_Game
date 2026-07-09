using System;
using Unity.Collections;
using Unity.Netcode;

// Serializable inventory slot transmitted over the network via NetworkList.
// Uses FixedString64Bytes so it implements INetworkSerializable without custom logic.
public struct NetworkInventorySlot : INetworkSerializable, IEquatable<NetworkInventorySlot> {
    public FixedString64Bytes ItemID;
    public int Quantity;

    public bool IsEmpty => Quantity <= 0 || ItemID.IsEmpty;

    public static NetworkInventorySlot Empty => new NetworkInventorySlot {
        ItemID = default,
        Quantity = 0
    };

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
        serializer.SerializeValue(ref ItemID);
        serializer.SerializeValue(ref Quantity);
    }

    public bool Equals(NetworkInventorySlot other) =>
        ItemID == other.ItemID && Quantity == other.Quantity;

    public override string ToString() =>
        IsEmpty ? "[Empty]" : $"[{ItemID} x{Quantity}]";
}