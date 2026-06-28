using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkSessionManager : MonoBehaviour {
    public static NetworkSessionManager Instance { get; private set; }

    void Awake() {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnEnable() {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback += OnDisconnected;
    }

    void OnDisable() {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnDisconnected;
    }

    void OnDisconnected(ulong clientId) {
        // Only handle on pure clients, not host
        if (NetworkManager.Singleton.IsHost) return;
        if (clientId != NetworkManager.Singleton.LocalClientId) return;

        Debug.Log("[NetworkSessionManager] Disconnected from host, returning to main menu.");
        string disconnectReason = NetworkManager.Singleton.DisconnectReason == "Host disconnected..." ? "Host disconnected..." : "Returning to Main Menu...";
        StartCoroutine(ReturnToMainMenu(disconnectReason));
    }

    public void HostShutdown() {
        if (!NetworkManager.Singleton.IsHost) {
            Debug.LogWarning("[NetworkSessionManager] HostShutdown called but not host.");
            return;
        }

        Debug.Log("[NetworkSessionManager] Host shutting down session.");

        foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds) {
            if (clientId == NetworkManager.Singleton.LocalClientId) continue;
            NetworkManager.Singleton.DisconnectClient(clientId, "Host disconnected...");
        }

        StartCoroutine(ShutdownAfterDelay(0.2f));
    }

    public void ClientDisconnect() {
        if (!NetworkManager.Singleton.IsClient) {
            Debug.LogWarning("[NetworkSessionManager] ClientDisconnect called but not a client.");
            return;
        }

        Debug.Log("[NetworkSessionManager] Client disconnecting.");
        NetworkManager.Singleton.Shutdown();
        StartCoroutine(ReturnToMainMenu("Returning to Main Menu..."));
    }

    public void LeaveSession() {
        if (NetworkManager.Singleton.IsHost)
            HostShutdown();
        else
            ClientDisconnect();
    }

    IEnumerator ShutdownAfterDelay(float delay) {
        yield return new WaitForSeconds(delay);
        NetworkManager.Singleton.Shutdown();
        StartCoroutine(ReturnToMainMenu("Returning to Main Menu..."));
    }

    IEnumerator ReturnToMainMenu(string message) {
        if (GameSessionManager.Instance != null)
            Destroy(GameSessionManager.Instance.gameObject);

        if (LanDiscovery.Instance != null)
            LanDiscovery.Instance.StopHostBroadcast();

        Debug.Log("[NetworkSessionManager] Returning to Main Menu.");
        LoadingScreenController.Instance.Show(message);
        yield return new WaitForSeconds(0.5f);
        SceneManager.LoadScene("MainMenu");
        yield return new WaitForSeconds(2f);
        LoadingScreenController.Instance.Hide();
    }
}