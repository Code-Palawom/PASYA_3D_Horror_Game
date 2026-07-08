using System;
using Unity.Collections;
using Unity.Netcode;

// Networked struct representing one player in the lobby player list.
// Stored in a NetworkList on GameSessionManager.
[System.Serializable]
public struct PlayerLobbyInfo : INetworkSerializable, IEquatable<PlayerLobbyInfo> {
    public ulong ClientId;
    public FixedString32Bytes PlayerName;
    public byte Role; // cast to/from PlayerRole — set once at connection approval

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref PlayerName);
        serializer.SerializeValue(ref Role);
    }

    public bool Equals(PlayerLobbyInfo other) => ClientId == other.ClientId;
}