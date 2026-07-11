using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

// Handles NGO connection approval on the server side.
// Must register its callback BEFORE StartHost()/StartClient() is called.
// Uses Awake() to guarantee registration before any other Start() runs.
public class ConnectionApprovalHandler : MonoBehaviour {
    public const int MaxPlayers = 4;

    public const string ReasonFull = "REASON:FULL";
    public const string ReasonInProgress = "REASON:IN_PROGRESS";
    public const string ReasonCountdown = "REASON:COUNTDOWN";
    public const string ReasonVersionMismatch = "REASON:VERSION_MISMATCH";
    public const string ReasonDuplicateName = "REASON:DUPLICATE_NAME";

    // Server-side cache: clientId → player name, populated during approval,
    // consumed and cleared by GameSessionManager.OnClientConnected.
    public static readonly Dictionary<ulong, string> PendingNames = new();
    public static readonly Dictionary<ulong, PlayerRole> PendingRoles = new();

    // Names currently claimed this session — covers players mid-handshake
    // (in PendingNames) AND players already fully connected. Kept separate
    // from PendingNames because that dictionary is cleared once
    // GameSessionManager consumes it, so it can't be used alone to detect
    // a name collision against an already-connected player.
    public static readonly Dictionary<ulong, string> ActiveNames = new();

    public static string GameVersion => Application.version;

    void Awake() {
        // Must happen before StartHost() — Awake() runs before Start()
        // on any other component, so this is guaranteed to register first.
        if (NetworkManager.Singleton != null) {
            Register();
        } else {
            // Fallback: if NetworkManager hasn't run its own Awake yet,
            // defer by one frame using a coroutine-free approach.
            // Unity calls Awake on components top-to-bottom in Inspector
            // order — move NetworkManager above this component if this fires.
            Debug.LogWarning("[ConnectionApprovalHandler] NetworkManager.Singleton not ready in Awake. " +
                             "Move NetworkManager.cs above ConnectionApprovalHandler in the Inspector.");
        }
    }

    // Also try in Start as a safety net
    void Start() {
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.ConnectionApprovalCallback == null)
            Register();
    }

    void Register() {
        NetworkManager.Singleton.ConnectionApprovalCallback = ApprovalCheck;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        Debug.Log("[ConnectionApprovalHandler] Approval callback registered.");
    }

    void OnDisable() {
        if (NetworkManager.Singleton != null) {
            NetworkManager.Singleton.ConnectionApprovalCallback = null;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    // Keep ActiveNames in sync — PendingNames/PendingRoles are consumed
    // elsewhere (GameSessionManager), but ActiveNames must persist for the
    // full duration a client stays connected, then clear on disconnect.
    void OnClientDisconnected(ulong clientId) {
        ActiveNames.Remove(clientId);
        PendingNames.Remove(clientId);
        PendingRoles.Remove(clientId);
    }

    // ─────────────────────────────────────────────────────────
    void ApprovalCheck(
        NetworkManager.ConnectionApprovalRequest request,
        NetworkManager.ConnectionApprovalResponse response) {
        // Host self‑approval — role comes straight from the host's own signed-in profile.
        if (request.ClientNetworkId == NetworkManager.Singleton.LocalClientId) {
            response.Approved = true;
            response.CreatePlayerObject = false;

            string hostName = AuthManager.Instance != null && AuthManager.Instance.CurrentProfile != null
                ? AuthManager.Instance.CurrentProfile.DisplayName
                : "Player";

            PendingRoles[request.ClientNetworkId] =
                AuthManager.Instance != null && AuthManager.Instance.CurrentProfile != null
                    ? AuthManager.Instance.CurrentProfile.RoleEnum
                    : PlayerRole.Player;

            PendingNames[request.ClientNetworkId] = hostName;
            ActiveNames[request.ClientNetworkId] = hostName;

            Debug.Log($"[ConnectionApproval] Host approved as '{hostName}'.");
            return;
        }

        // Decode version + name + role from JSON payload
        string version = "";
        string playerName = AuthManager.Instance.CurrentProfile?.DisplayName ?? "Player";
        PlayerRole role = PlayerRole.Player;

        if (request.Payload != null && request.Payload.Length > 0) {
            string json = System.Text.Encoding.UTF8.GetString(request.Payload);
            try {
                var payload = JsonUtility.FromJson<ConnectionPayload>(json);
                version = payload.version ?? "";
                playerName = payload.playerName ?? "Player";
                role = (PlayerRole)payload.role;
            } catch {
                Deny(response, ReasonVersionMismatch);
                Debug.Log($"[ConnectionApproval] Rejected {request.ClientNetworkId} — invalid payload.");
                return;
            }
        }

        // 1. Version mismatch
        if (version != GameVersion) {
            Deny(response, ReasonVersionMismatch);
            Debug.Log($"[ConnectionApproval] Rejected {request.ClientNetworkId} — version mismatch. " +
                      $"Client: '{version}' Host: '{GameVersion}'");
            return;
        }

        // 2. Max players
        int currentPlayers = NetworkManager.Singleton.ConnectedClientsList.Count;
        if (currentPlayers >= MaxPlayers) {
            Deny(response, ReasonFull);
            Debug.Log($"[ConnectionApproval] Rejected {request.ClientNetworkId} — lobby full.");
            return;
        }

        // 3. Game in progress
        string activeScene = SceneManager.GetActiveScene().name;
        bool inLevel = GameSessionManager.Instance != null &&
                       activeScene == GameSessionManager.Instance.SelectedLevelSceneName.Value.ToString();
        if (inLevel) {
            Deny(response, ReasonInProgress);
            Debug.Log($"[ConnectionApproval] Rejected {request.ClientNetworkId} — game in progress.");
            return;
        }

        // 4. Countdown active
        if (LobbyReadyManager.Instance != null && LobbyReadyManager.Instance.IsCountdownActive) {
            Deny(response, ReasonCountdown);
            Debug.Log($"[ConnectionApproval] Rejected {request.ClientNetworkId} — countdown active.");
            return;
        }

        // 5. Duplicate name — same Firestore account signed in on another
        // client already connected (or mid-handshake) this session.
        foreach (var kvp in ActiveNames) {
            if (string.Equals(kvp.Value, playerName, System.StringComparison.OrdinalIgnoreCase)) {
                Deny(response, ReasonDuplicateName);
                Debug.Log($"[ConnectionApproval] Rejected {request.ClientNetworkId} — duplicate name '{playerName}'.");
                return;
            }
        }

        // All checks passed
        PendingNames[request.ClientNetworkId] = playerName;
        PendingRoles[request.ClientNetworkId] = role;
        ActiveNames[request.ClientNetworkId] = playerName;
        response.Approved = true;
        response.CreatePlayerObject = false;
        Debug.Log($"[ConnectionApproval] Approved {request.ClientNetworkId} as '{playerName}' (Role: {role}) ({currentPlayers + 1}/{MaxPlayers}).");
    }

    static void Deny(NetworkManager.ConnectionApprovalResponse response, string reason) {
        response.Approved = false;
        response.Reason = reason;
    }

    [System.Serializable]
    private class ConnectionPayload {
        public string version;
        public string playerName;
        public byte role; // cast to/from PlayerRole
    }
}