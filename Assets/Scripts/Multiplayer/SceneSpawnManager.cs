using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

// Manual player spawning system.
// - Player Prefab must be REMOVED from NetworkManager's Player Prefab field
// - This manager handles spawning each player individually when they connect
// - Players are spawned only in scenes that have spawn data configured
// - Same player object persists across Lobby → Level (DontDestroyOnLoad via NGO)
// - On scene load, existing players are repositioned to new spawn points
public class SceneSpawnManager : MonoBehaviour {
    [Header("Player Prefab (assign here, NOT in NetworkManager)")]
    [SerializeField] GameObject playerPrefab;

    [Header("One asset per scene")]
    [SerializeField] List<SceneSpawnData> sceneSpawnAssets;

    // Scenes where players should exist
    private readonly HashSet<string> _spawnScenes = new();

    void Start() {
        StartCoroutine(SubscribeWhenReady());
    }

    IEnumerator SubscribeWhenReady() {
        yield return new WaitUntil(() =>
            NetworkManager.Singleton != null &&
            NetworkManager.Singleton.SceneManager != null);

        // Build set of scene names that have spawn data
        foreach (var asset in sceneSpawnAssets)
            if (!string.IsNullOrEmpty(asset.sceneName))
                _spawnScenes.Add(asset.sceneName);

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.SceneManager.OnSceneEvent += OnSceneEvent;

        Debug.Log("[SceneSpawnManager] Ready. Spawn scenes: " +
                  string.Join(", ", _spawnScenes));
    }

    void OnDestroy() {
        if (NetworkManager.Singleton == null) return;
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        if (NetworkManager.Singleton.SceneManager != null)
            NetworkManager.Singleton.SceneManager.OnSceneEvent -= OnSceneEvent;
    }

    // ─────────────────────────────────────────────────────────
    // Client connects — spawn their player if we're in a spawn scene
    // ─────────────────────────────────────────────────────────
    void OnClientConnected(ulong clientId) {
        if (!NetworkManager.Singleton.IsServer) return;

        string currentScene = SceneManager.GetActiveScene().name;

        if (!_spawnScenes.Contains(currentScene)) {
            Debug.Log($"[SceneSpawnManager] Client {clientId} connected in " +
                      $"'{currentScene}' — not a spawn scene, skipping.");
            return;
        }

        StartCoroutine(SpawnAndPosition(clientId, currentScene));
    }

    // ─────────────────────────────────────────────────────────
    // Scene events — wait for all clients then reposition
    // ─────────────────────────────────────────────────────────
    void OnSceneEvent(SceneEvent sceneEvent) {
        if (sceneEvent.SceneEventType == SceneEventType.LoadComplete) {
            if (!NetworkManager.Singleton.IsServer) {
                if (sceneEvent.ClientId == NetworkManager.Singleton.LocalClientId)
                    Debug.Log($"[SceneSpawnManager] (Client) Finished loading '{sceneEvent.SceneName}'.");
                return;
            }

            return;
        }

        if (sceneEvent.SceneEventType != SceneEventType.LoadEventCompleted) return;
        if (!NetworkManager.Singleton.IsServer) return;

        string loadedScene = sceneEvent.SceneName;
        if (!_spawnScenes.Contains(loadedScene)) return;

        // Safety fallback: ensure coordinator is initialized before player
        // spawning begins. QuizAssignmentCoordinator.Start() handles the
        // primary init for scene-placed gates, but this covers the case
        // where gates are runtime-spawned after LoadEventCompleted.
        if (QuizAssignmentCoordinator.Instance != null &&
            GameSessionManager.Instance != null) {
            string setName = GameSessionManager.Instance.SelectedQuizSetName.Value.ToString();
            if (!string.IsNullOrEmpty(setName))
                QuizAssignmentCoordinator.Instance.Initialize(setName);
        }

        StartCoroutine(RepositionAllPlayers(loadedScene));
    }

    // ─────────────────────────────────────────────────────────
    // Spawn a new player object and position it
    // ─────────────────────────────────────────────────────────
    IEnumerator SpawnAndPosition(ulong clientId, string sceneName) {
        // Check if this client already has a player object
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var existingClient) &&
            existingClient.PlayerObject != null) {
            // Already has a player — just reposition
            yield return RepositionClient(clientId, sceneName);
            yield break;
        }

        // Spawn a new player object for this client
        var go = Instantiate(playerPrefab);
        var netObj = go.GetComponent<NetworkObject>();
        netObj.SpawnAsPlayerObject(clientId, destroyWithScene: false);

        Debug.Log($"[SceneSpawnManager] Spawned player for client {clientId}.");

        // Wait for spawn to complete
        yield return new WaitUntil(() =>
            NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var c) &&
            c.PlayerObject != null);

        yield return RepositionClient(clientId, sceneName);
    }

    // ─────────────────────────────────────────────────────────
    // Reposition all connected players once all have loaded
    // ─────────────────────────────────────────────────────────
    IEnumerator RepositionAllPlayers(string sceneName) {
        yield return null;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList) {
            if (client.PlayerObject == null)
                yield return SpawnAndPosition(client.ClientId, sceneName); // spawn instead of skip
            else
                yield return RepositionClient(client.ClientId, sceneName);
        }
    }

    // ─────────────────────────────────────────────────────────
    // Reposition one player to their assigned spawn point
    // ─────────────────────────────────────────────────────────
    IEnumerator RepositionClient(ulong clientId, string sceneName) {
        var asset = sceneSpawnAssets.FirstOrDefault(a => a.sceneName == sceneName);
        if (asset == null || asset.spawnPoints == null || asset.spawnPoints.Count == 0) {
            Debug.LogWarning($"[SceneSpawnManager] No spawn data for '{sceneName}'.");
            yield break;
        }

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
            yield break;

        if (client.PlayerObject == null) yield break;

        int index = (int)(clientId % (ulong)asset.spawnPoints.Count);
        SpawnPointData sp = asset.spawnPoints[index];

        client.PlayerObject.GetComponent<Player>().TeleportClientRpc(sp.position, sp.rotation);

        Debug.Log($"[SceneSpawnManager] Client {clientId} → spawn {index} in '{sceneName}'.");
    }
}