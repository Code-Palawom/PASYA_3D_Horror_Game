using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

// Persistent networked session data — survives MainMenu → Lobby → Level
// scene transitions via DontDestroyOnLoad.

// Spawned ONCE by the server right after StartHost()/StartServer()
// succeeds (see MainMenuUI). Holds the selected quiz set, the selected
// level scene, and the live list of connected players — all synced to
// every client automatically.

// Also owns per-player session stats (server-side), sent to all clients
// at game end via SendResults().
public class GameSessionManager : NetworkBehaviour {
    public static GameSessionManager Instance { get; private set; }

    public NetworkVariable<FixedString128Bytes> SelectedQuizSetName = new(
        "", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<FixedString64Bytes> SelectedLevelSceneName = new(
        "", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);


    public NetworkList<PlayerLobbyInfo> Players;

    // ─────────────────────────────────────────────────────────
    // Session stats — server-side only until game ends
    // ─────────────────────────────────────────────────────────

    [Serializable]
    public class PlayerSessionStats {
        public ulong ClientId;
        public string PlayerName;
        public int Score;
        public int QuestionsAnswered;
        public int QuestionsCorrect;
        public int SideEffectsReceived;
        public bool Disconnected;        // true = left early
    }

    // Live players — keyed by clientId
    private Dictionary<ulong, PlayerSessionStats> _stats = new();

    // Players who disconnected mid-game — preserved for results screen
    private List<PlayerSessionStats> _disconnectedPlayers = new();

    // Fired on all clients when the host sends results at game end
    public static event Action<PlayerSessionStats[]> OnResultsReceived;

    // ─────────────────────────────────────────────────────────
    void Awake() {
        Players = new NetworkList<PlayerLobbyInfo>(
            new List<PlayerLobbyInfo>(),
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        // IMPORTANT: this object is spawned at runtime (not scene-placed),
        // so DontDestroyOnLoad lets it survive the Lobby → Level scene swap.
        DontDestroyOnLoad(gameObject);
    }

    public override void OnNetworkSpawn() {
        Instance = this;

        if (IsServer) {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

            string hostName = GameModeManager.Instance != null
                ? AuthManager.Instance.CurrentProfile?.DisplayName ?? "Host"
                : "Host";

            PlayerRole hostRole = AuthManager.Instance != null && AuthManager.Instance.CurrentProfile != null
                ? AuthManager.Instance.CurrentProfile.RoleEnum
                : PlayerRole.Player;

            AddPlayer(NetworkManager.Singleton.LocalClientId, hostName, hostRole);
        }
    }

    public override void OnNetworkDespawn() {
        if (IsServer && NetworkManager.Singleton != null) {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        if (Instance == this) Instance = null;
    }

    // ─────────────────────────────────────────────────────────
    void OnClientConnected(ulong clientId) {
        if (IsHost && clientId == NetworkManager.Singleton.LocalClientId) return;

        // Consume the name + role cached by ConnectionApprovalHandler during approval
        string name = $"Player {clientId}"; // fallback
        if (ConnectionApprovalHandler.PendingNames.TryGetValue(clientId, out string cachedName)) {
            name = cachedName;
            ConnectionApprovalHandler.PendingNames.Remove(clientId);
        }

        PlayerRole role = PlayerRole.Player; // fallback
        if (ConnectionApprovalHandler.PendingRoles.TryGetValue(clientId, out PlayerRole cachedRole)) {
            role = cachedRole;
            ConnectionApprovalHandler.PendingRoles.Remove(clientId);
        }

        AddPlayer(clientId, name, role);
        ChatManager.Instance.SendSystemMessage($"Player '{name}' joined the game.");
    }

    void OnClientDisconnected(ulong clientId) {
        // Remove from live Players list
        for (int i = Players.Count - 1; i >= 0; i--) {
            if (Players[i].ClientId == clientId) {
                Players.RemoveAt(i);
                break;
            }
        }

        // Freeze stats and move to disconnected list
        if (_stats.TryGetValue(clientId, out var stats)) {
            stats.Disconnected = true;
            _disconnectedPlayers.Add(stats);
            _stats.Remove(clientId);
            Debug.Log($"[GameSessionManager] Player '{stats.PlayerName}' disconnected — stats frozen.");
            ChatManager.Instance.SendSystemMessage($"Player '{stats.PlayerName}' left the game.");
        }
    }

    void AddPlayer(ulong clientId, string name, PlayerRole role) {
        Players.Add(new PlayerLobbyInfo {
            ClientId = clientId,
            PlayerName = new FixedString32Bytes(name),
            Role = (byte)role
        });

        // Initialize stats entry for this player
        _stats[clientId] = new PlayerSessionStats {
            ClientId = clientId,
            PlayerName = name
        };
    }

    // ─────────────────────────────────────────────────────────
    // Stats recording — called server-side by QuizManager and
    // QuizSideEffect via RecordAnswerRpc / RecordSideEffectRpc
    // ─────────────────────────────────────────────────────────

    // Called by QuizManager.ResolveSession() via RPC after answer evaluated.
    public void RecordAnswer(ulong clientId, bool isCorrect, int score) {
        if (!IsServer) return;
        if (!_stats.TryGetValue(clientId, out var stats)) return;

        stats.QuestionsAnswered++;
        if (isCorrect) {
            stats.QuestionsCorrect++;
            stats.Score += score;
        }
    }

    /// Called by QuizSideEffect.ApplyWithDuration() via RPC when effect applied.
    /// </summary>
    public void RecordSideEffect(ulong clientId) {
        if (!IsServer) return;
        if (!_stats.TryGetValue(clientId, out var stats)) return;
        stats.SideEffectsReceived++;
    }

    // ─────────────────────────────────────────────────────────
    // RPCs to report stats server-side from clients
    // ─────────────────────────────────────────────────────────

    [Rpc(SendTo.Server)]
    public void RecordAnswerRpc(bool isCorrect, int score, RpcParams rpcParams = default) {
        Debug.Log("[RPC][Server] RecordAnswer");
        RecordAnswer(rpcParams.Receive.SenderClientId, isCorrect, score);
    }

    [Rpc(SendTo.Server)]
    public void RecordSideEffectRpc(RpcParams rpcParams = default) {
        Debug.Log("[RPC][Server] ReocrdSideEffect");
        RecordSideEffect(rpcParams.Receive.SenderClientId);
    }

    // ─────────────────────────────────────────────────────────
    // End of game — host sends all stats to every client
    // ─────────────────────────────────────────────────────────

    /// Call on the server when the level ends to broadcast results.
    /// Merges live + disconnected players, orders by score descending.
    /// </summary>
    public void SendResults() {
        if (!IsServer) return;

        var all = _stats.Values
            .Concat(_disconnectedPlayers)
            .OrderByDescending(s => s.Score)
            .ToArray();

        // Serialize to flat arrays for RPC (no complex types over network)
        var clientIds = all.Select(s => s.ClientId).ToArray();
        var playerNames = all.Select(s => new FixedString64Bytes(s.PlayerName)).ToArray();
        var scores = all.Select(s => s.Score).ToArray();
        var questionsAnswered = all.Select(s => s.QuestionsAnswered).ToArray();
        var questionsCorrect = all.Select(s => s.QuestionsCorrect).ToArray();
        var sideEffects = all.Select(s => s.SideEffectsReceived).ToArray();
        var disconnected = all.Select(s => s.Disconnected).ToArray();

        ReceiveResultsRpc(clientIds, playerNames, scores,
                          questionsAnswered, questionsCorrect,
                          sideEffects, disconnected);
    }

    [Rpc(SendTo.Everyone)]
    void ReceiveResultsRpc(ulong[] clientIds, FixedString64Bytes[] playerNames, int[] scores,
                           int[] questionsAnswered, int[] questionsCorrect,
                           int[] sideEffects, bool[] disconnected) {
        Debug.Log("[RPC][Everyone] ReceiveResults");
        int count = clientIds.Length;
        var results = new PlayerSessionStats[count];

        for (int i = 0; i < count; i++) {
            results[i] = new PlayerSessionStats {
                ClientId = clientIds[i],
                PlayerName = playerNames[i].ToString(),
                Score = scores[i],
                QuestionsAnswered = questionsAnswered[i],
                QuestionsCorrect = questionsCorrect[i],
                SideEffectsReceived = sideEffects[i],
                Disconnected = disconnected[i]
            };
        }

        OnResultsReceived?.Invoke(results);
    }

    // ─────────────────────────────────────────────────────────
    public void SetSelectedQuizSet(string setName) {
        if (IsServer) SelectedQuizSetName.Value = setName;
    }

    public void SetSelectedLevel(string sceneName) {
        if (IsServer) SelectedLevelSceneName.Value = sceneName;
    }
}