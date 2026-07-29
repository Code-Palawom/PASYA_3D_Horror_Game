using Unity.Collections;
using Unity.Netcode;

public struct ChatMessage : INetworkSerializable {
    public ulong SenderId;
    public FixedString32Bytes SenderName;
    public FixedString4096Bytes Content;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
        serializer.SerializeValue(ref SenderId);
        serializer.SerializeValue(ref SenderName);
        serializer.SerializeValue(ref Content);
    }
}