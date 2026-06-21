using System.Collections;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Handles starting the game as Host, Server, or Client.
// On successful Host start, loads the gameplay scene — clients
// connected to that host automatically follow via NGO's scene sync.
public class GameBootstrap : MonoBehaviour {
    [Header("UI (optional — wire if you have a lobby screen)")]
    [SerializeField] TMP_InputField ipAddressInput;
    [SerializeField] TMP_InputField portInput;
    [SerializeField] TMP_Text statusText;

    [Header("Defaults")]
    [SerializeField] string defaultIP = "127.0.0.1";
    [SerializeField] ushort defaultPort = 7777;

    [Header("Scene Loading")]
    [Tooltip("Name of the gameplay scene to load after Host starts. Must be added to Build Settings.")]
    [SerializeField] string gameplaySceneName = "Gameplay";

    // ─────────────────────────────────────────────────────────
    // Called by UI buttons
    // ─────────────────────────────────────────────────────────

    // Host: runs both server + client locally, then loads gameplay scene. </summary>
    public void StartHost() {
        ApplyConnectionSettings();

        if (!NetworkManager.Singleton.StartHost()) {
            SetStatus("Failed to start host.");
            return;
        }

        SetStatus("Started as Host. Loading game...");

        // Host loads the scene — NGO automatically syncs this to all
        // clients that connect afterward, and to clients already connected.
        NetworkManager.Singleton.SceneManager.LoadScene(
            gameplaySceneName, LoadSceneMode.Single);
    }

    // Client: joins an existing host. Scene load is automatic via NGO. </summary>
    public void StartClient() {
        ApplyConnectionSettings();

        if (!NetworkManager.Singleton.StartClient()) {
            SetStatus("Failed to start client.");
            return;
        }

        SetStatus($"Connecting to {GetIP()}:{GetPort()}...");

        NetworkManager.Singleton.OnClientConnectedCallback += OnConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnDisconnected;
    }

    // Server only: headless server, no local player, loads gameplay scene. </summary>
    public void StartServer() {
        ApplyConnectionSettings();

        if (!NetworkManager.Singleton.StartServer()) {
            SetStatus("Failed to start server.");
            return;
        }

        SetStatus("Started as Server. Loading game...");

        NetworkManager.Singleton.SceneManager.LoadScene(
            gameplaySceneName, LoadSceneMode.Single);
    }

    public void Disconnect() {
        NetworkManager.Singleton.Shutdown();
        SetStatus("Disconnected");
    }

    // ─────────────────────────────────────────────────────────
    void ApplyConnectionSettings() {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport == null) return;

        transport.SetConnectionData(GetIP(), GetPort());
    }

    string GetIP() => (ipAddressInput != null && !string.IsNullOrWhiteSpace(ipAddressInput.text))
                        ? ipAddressInput.text
                        : defaultIP;

    ushort GetPort() => (portInput != null && ushort.TryParse(portInput.text, out ushort p))
                        ? p
                        : defaultPort;

    // ─────────────────────────────────────────────────────────
    void OnConnected(ulong clientId) {
        // Only react to OUR OWN connection, not other clients joining
        if (clientId != NetworkManager.Singleton.LocalClientId) return;

        SetStatus("Connected! Loading game...");
        NetworkManager.Singleton.OnClientConnectedCallback -= OnConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnDisconnected;

        // No manual scene load needed here — the host's LoadScene()
        // call automatically brings connected clients along via NGO.
    }

    void OnDisconnected(ulong clientId) {
        if (clientId != NetworkManager.Singleton.LocalClientId) return;

        SetStatus("Failed to connect or disconnected.");
        NetworkManager.Singleton.OnClientConnectedCallback -= OnConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnDisconnected;
    }

    void SetStatus(string message) {
        if (statusText != null) statusText.text = message;
        Debug.Log($"[GameBootstrap] {message}");
    }
}