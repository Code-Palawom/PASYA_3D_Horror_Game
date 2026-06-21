using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Handles NGO connection approval on the server side.
// Must register its callback BEFORE StartHost()/StartClient() is called.
// Uses Awake() to guarantee registration before any other Start() runs.
public class ConnectionApprovalHandler : MonoBehaviour {
    public const int MaxPlayers = 4;

    public const string ReasonFull = "REASON:FULL";
    public const string ReasonInProgress = "REASON:IN_PROGRESS";
    public const string ReasonCountdown = "REASON:COUNTDOWN";

    // Server-side cache: clientId → player name, populated during approval,
    // consumed and cleared by GameSessionManager.OnClientConnected.
    public static readonly Dictionary<ulong, string> PendingNames = new();

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
        Debug.Log("[ConnectionApprovalHandler] Approval callback registered.");
    }

    void OnDisable() {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.ConnectionApprovalCallback = null;
    }

    // ─────────────────────────────────────────────────────────
    void ApprovalCheck(
        NetworkManager.ConnectionApprovalRequest request,
        NetworkManager.ConnectionApprovalResponse response) {
        // Host always approves itself
        if (request.ClientNetworkId == NetworkManager.Singleton.LocalClientId) {
            response.Approved = true;
            response.CreatePlayerObject = false;

            Debug.Log("[ConnectionApproval] Host approved.");
            return;
        }

        int currentPlayers = NetworkManager.Singleton.ConnectedClientsList.Count;

        // 1. Max players reached
        if (currentPlayers >= MaxPlayers) {
            Deny(response, ReasonFull);
            Debug.Log($"[ConnectionApproval] Rejected {request.ClientNetworkId} — lobby full.");
            return;
        }

        // 2. Level scene already loaded (game in progress)
        string activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        bool inLevel = GameSessionManager.Instance != null &&
                       activeScene == GameSessionManager.Instance
                           .SelectedLevelSceneName.Value.ToString();

        if (inLevel) {
            Deny(response, ReasonInProgress);
            Debug.Log($"[ConnectionApproval] Rejected {request.ClientNetworkId} — game in progress.");
            return;
        }

        // 3. Countdown has started in the Lobby
        if (LobbyReadyManager.Instance != null && LobbyReadyManager.Instance.IsCountdownActive) {
            Deny(response, ReasonCountdown);
            Debug.Log($"[ConnectionApproval] Rejected {request.ClientNetworkId} — countdown active.");
            return;
        }

        // All checks passed — decode player name from payload
        string playerName = "Player";
        if (request.Payload != null && request.Payload.Length > 0)
            playerName = System.Text.Encoding.UTF8.GetString(request.Payload);

        // Cache it so GameSessionManager can consume it in OnClientConnected
        PendingNames[request.ClientNetworkId] = playerName;

        response.Approved = true;
        response.CreatePlayerObject = false;

        Debug.Log($"[ConnectionApproval] Approved {request.ClientNetworkId} as '{playerName}' ({currentPlayers + 1}/{MaxPlayers}).");
    }

    static void Deny(NetworkManager.ConnectionApprovalResponse response, string reason) {
        response.Approved = false;
        response.Reason = reason;
    }
}