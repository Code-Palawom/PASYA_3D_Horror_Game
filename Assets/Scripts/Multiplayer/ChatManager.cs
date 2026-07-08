using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

public class ChatManager : NetworkBehaviour {
    public static ChatManager Instance { get; private set; }

    public readonly List<ChatMessage> Messages = new();
    public UnityEvent<ChatMessage> OnMessageReceived = new();
    public UnityEvent OnChatCleared = new();

    void Awake() {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public override void OnNetworkSpawn() {
        if (IsServer)
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        else
            StartCoroutine(RequestHistoryDelayed());
    }

    public override void OnNetworkDespawn() {
        if (IsServer && NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;

        if (Instance == this) Instance = null;
    }

    // ─── Public API ───────────────────────────────────────────

    public void SendChatMessage(string content) {
        if (!IsSpawned || string.IsNullOrWhiteSpace(content)) return;
        SendChatMessageRpc(NetworkManager.Singleton.LocalClientId, content);
    }

    public void SendSystemMessage(string content) {
        if (!IsServer) return;

        var msg = new ChatMessage {
            SenderId = ulong.MaxValue,
            SenderName = "[System]",
            Content = content
        };

        BroadcastMessageRpc(msg);
    }

    public void ClearChat() {
        if (!IsServer) return;
        Messages.Clear();
        ClearChatRpc();
    }

    // ─── History Sync ─────────────────────────────────────────

    void OnClientConnected(ulong clientId) { }

    IEnumerator RequestHistoryDelayed() {
        yield return new WaitForSeconds(1f); ; // wait for ChatUI.InitChat to register listeners
        RequestHistoryRpc();
    }

    [Rpc(SendTo.Server)]
    void RequestHistoryRpc(RpcParams rpcParams = default) {
        Debug.Log("[RPC][Server] RequestHistory");
        ulong requesterId = rpcParams.Receive.SenderClientId;
        SyncHistoryRpc(Messages.ToArray(), RpcTarget.Single(requesterId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams)]
    void SyncHistoryRpc(ChatMessage[] history, RpcParams rpcParams = default) {
        Debug.Log("[RPC][SpecifiedInParams] SyncHistory");
        foreach (var msg in history) {
            Messages.Add(msg);
            OnMessageReceived.Invoke(msg);
        }
    }

    // ─── RPCs ─────────────────────────────────────────────────

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    void SendChatMessageRpc(ulong senderId, string content) {
        Debug.Log("[RPC][Server] SendChatMessage");

        // Slash-commands are intercepted here and never broadcast as chat.
        // ChatCommandProcessor checks the sender's role (from GameSessionManager.Players)
        // before doing anything, and replies privately to the sender only.
        if (ChatCommandProcessor.TryHandle(senderId, content, reply => SendPrivateSystemMessage(senderId, reply)))
            return;

        string senderName = ResolvePlayerName(senderId);

        var msg = new ChatMessage {
            SenderId = senderId,
            SenderName = senderName,
            Content = content
        };

        BroadcastMessageRpc(msg);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void NotifyCorrectAnswerRpc(ulong clientId, string questionText) {
        Debug.Log("[RPC][Server] NotifyCorrectAnswer");
        string name = ResolvePlayerName(clientId);
        SendSystemMessage($"{name} answered correctly: {questionText}");
    }

    [Rpc(SendTo.ClientsAndHost)]
    void BroadcastMessageRpc(ChatMessage msg) {
        Debug.Log("[RPC][ClientsAndHost] BroadcastMessage");
        Messages.Add(msg);
        OnMessageReceived.Invoke(msg);
    }

    [Rpc(SendTo.ClientsAndHost)]
    void ClearChatRpc() {
        Debug.Log("[RPC][ClientsAndHost] ClearChat");
        Messages.Clear();
        OnChatCleared.Invoke();
    }

    // Sends a system-style message to ONE client only — used for command
    // feedback/denials so they don't clutter or leak into public chat.
    void SendPrivateSystemMessage(ulong targetClientId, string content) {
        var msg = new ChatMessage {
            SenderId = ulong.MaxValue,
            SenderName = "[System]",
            Content = content
        };

        ReceivePrivateMessageRpc(msg, RpcTarget.Single(targetClientId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams)]
    void ReceivePrivateMessageRpc(ChatMessage msg, RpcParams rpcParams = default) {
        Debug.Log("[RPC][SpecifiedInParams] ReceivePrivateMessage");
        // Not added to Messages — this is local-only feedback, not shared chat history.
        OnMessageReceived.Invoke(msg);
    }

    // ─── Helpers ──────────────────────────────────────────────

    string ResolvePlayerName(ulong clientId) {
        if (GameSessionManager.Instance != null) {
            foreach (var player in GameSessionManager.Instance.Players) {
                if (player.ClientId == clientId)
                    return player.PlayerName.ToString();
            }
        }

        return $"Player {clientId}";
    }
}