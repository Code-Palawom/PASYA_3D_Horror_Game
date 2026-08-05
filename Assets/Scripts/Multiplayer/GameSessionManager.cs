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

    // Single source of truth for score -> XP conversion. Used by
    // PlayerStatsRecorder (Firestore write), PodiumScreenUI (local count-up),
    // and PodiumPlayerCardUI (other players' "+XP" label) — change this one
    // constant instead of the multiplier in three places.
    public const float XpFromScoreMultiplier = 0.75f;
    public static int CalculateXp(int score) => Mathf.RoundToInt(score * XpFromScoreMultiplier);

    public NetworkVariable<FixedString64Bytes> SessionId = new(
        "", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

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
        public string SkinId;            // cached from NetworkCharacterAppearance at spawn — survives disconnect
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

            // Use the public lobby ID if this is a listed session; otherwise
            // (LAN / direct-connect / private relay) generate a session GUID.
            string sessionId = LobbyManager.Instance != null && LobbyManager.Instance.HostedLobbyId != null
                ? LobbyManager.Instance.HostedLobbyId
                : Guid.NewGuid().ToString();
            SessionId.Value = new FixedString64Bytes(sessionId);

            string hostName = AuthManager.Instance.CurrentProfile?.DisplayName ?? SettingsManager.Instance.Current.playerName;

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
        UpdateLobby();
        ChatManager.Instance.SendSystemMessage($"<b>{name}</b> joined the game.");
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
            Debug.Log($"[GameSessionManager] '{stats.PlayerName}' disconnected — stats frozen.");
            ChatManager.Instance.SendSystemMessage($"<b>{stats.PlayerName}</b> left the game.");
        }

        UpdateLobby();
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

    async void UpdateLobby() {
        await LobbyManager.Instance.UpdatePlayerCountAsync(NetworkManager.Singleton.ConnectedClientsIds.Count);
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

    // Called by QuizSideEffect.ApplyWithDuration() via RPC when effect applied.
    public void RecordSideEffect(ulong clientId) {
        if (!IsServer) return;
        if (!_stats.TryGetValue(clientId, out var stats)) return;
        stats.SideEffectsReceived++;
    }

    // Called once by NetworkCharacterAppearance right after it sets its
    // skinId NetworkVariable at spawn. Cached here (not read live off the
    // NetworkVariable at results time) so it's still available for players
    // who've since disconnected — their PlayerObject/NetworkVariable is
    // gone by then, but _disconnectedPlayers keeps this copy.
    public void RecordSkin(ulong clientId, string skinId) {
        if (!IsServer) return;
        if (!_stats.TryGetValue(clientId, out var stats)) return;
        stats.SkinId = skinId;
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

    [Rpc(SendTo.Server)]
    public void RecordSkinRpc(FixedString32Bytes skinId, RpcParams rpcParams = default) {
        RecordSkin(rpcParams.Receive.SenderClientId, skinId.ToString());
    }

    // ─────────────────────────────────────────────────────────
    // End of game — host sends all stats to every client
    // ─────────────────────────────────────────────────────────

    // Call on the server when the level ends to broadcast results.
    // Merges live + disconnected players, orders by score descending.
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
        var skinIds = all.Select(s => new FixedString32Bytes(s.SkinId ?? "")).ToArray();

        ReceiveResultsRpc(clientIds, playerNames, scores,
                          questionsAnswered, questionsCorrect,
                          sideEffects, disconnected, skinIds);
    }

    [Rpc(SendTo.Everyone)]
    void ReceiveResultsRpc(ulong[] clientIds, FixedString64Bytes[] playerNames, int[] scores,
                           int[] questionsAnswered, int[] questionsCorrect,
                           int[] sideEffects, bool[] disconnected, FixedString32Bytes[] skinIds) {
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
                Disconnected = disconnected[i],
                SkinId = skinIds[i].ToString()
            };
        }

        OnResultsReceived?.Invoke(results);
    }

    // ─────────────────────────────────────────────────────────
    // Voice mute state — client reports their own VivoxManager mute
    // state, server is authoritative and writes it into Players.
    // ─────────────────────────────────────────────────────────

    public void SetPlayerMuted(ulong clientId, bool muted) {
        if (!IsServer) return;

        for (int i = 0; i < Players.Count; i++) {
            if (Players[i].ClientId != clientId) continue;

            var info = Players[i];
            if (info.IsMuted == muted) {
                Debug.Log($"[VoiceSync] SetPlayerMuted: no-op, already {muted}");
                return;
            }

            info.IsMuted = muted;
            Players[i] = info; // structs are value types - must reassign the element
            Debug.Log($"[VoiceSync] SetPlayerMuted: wrote IsMuted={muted} for clientId={clientId}");
            return;
        }

        Debug.Log($"[VoiceSync] SetPlayerMuted: no matching clientId={clientId} found in Players (count={Players.Count})");
    }

    [Rpc(SendTo.Server)]
    public void SetMutedRpc(bool muted, RpcParams rpcParams = default) {
        SetPlayerMuted(rpcParams.Receive.SenderClientId, muted);
    }

    public void SetPlayerMicOn(ulong clientId, bool micOn) {
        Debug.Log($"[VoiceSync] SetPlayerMicOn called: clientId={clientId}, micOn={micOn}, IsServer={IsServer}");
        if (!IsServer) return;

        for (int i = 0; i < Players.Count; i++) {
            if (Players[i].ClientId != clientId) continue;

            var info = Players[i];
            if (info.IsMicOn == micOn) {
                Debug.Log($"[VoiceSync] SetPlayerMicOn: no-op, already {micOn}");
                return;
            }

            info.IsMicOn = micOn;
            Players[i] = info; // structs are value types - must reassign the element
            Debug.Log($"[VoiceSync] SetPlayerMicOn: wrote IsMicOn={micOn} for clientId={clientId}");
            return;
        }

        Debug.Log($"[VoiceSync] SetPlayerMicOn: no matching clientId={clientId} found in Players (count={Players.Count})");
    }

    [Rpc(SendTo.Server)]
    public void SetMicOnRpc(bool micOn, RpcParams rpcParams = default) {
        SetPlayerMicOn(rpcParams.Receive.SenderClientId, micOn);
    }

    // ─────────────────────────────────────────────────────────
    public void SetSelectedQuizSet(string setName) {
        if (IsServer) SelectedQuizSetName.Value = setName;
    }

    public void SetSelectedLevel(string sceneName) {
        if (IsServer) SelectedLevelSceneName.Value = sceneName;
    }
}