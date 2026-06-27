using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Scene-placed NetworkBehaviour in the Lobby scene.
// Tracks who is ready, manages the countdown, and triggers the level load.
//
// Flow:
//   Player presses Ready  → added to _readyPlayers
//   All players ready     → countdown starts
//   Any player unreadies  → countdown cancelled (if not locked yet)
//   Countdown hits 3s     → _countdownLocked = true, button disabled for all
//   Countdown hits 0      → server loads level scene
public class LobbyReadyManager : NetworkBehaviour {
    public static LobbyReadyManager Instance { get; private set; }

    [SerializeField] float countdownDuration = 10f;

    // ── Networked state ───────────────────────────────────────
    private NetworkList<ulong> _readyPlayers;

    private NetworkVariable<double> _countdownEndTime = new(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<bool> _countdownLocked = new(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // ── Static events for UI ──────────────────────────────────
    public static event Action OnLobbyManagerSpawned;
    public static event Action OnLobbyManagerDespawned;
    public static event Action<ulong, bool> OnPlayerReadyChanged;   // clientId, isReady
    public static event Action<double, float> OnCountdownChanged;   // endTime, totalDuration
    public static event Action OnCountdownCancelled;
    public static event Action OnCountdownLocked;

    // ── Public accessors ──────────────────────────────────────
    public bool IsCountdownActive => _countdownEndTime.Value > 0;
    public bool IsCountdownLocked => _countdownLocked.Value;
    public double CountdownEndTime => _countdownEndTime.Value;
    public float TotalDuration => countdownDuration;

    public bool IsReady(ulong clientId) {
        foreach (var id in _readyPlayers)
            if (id == clientId) return true;
        return false;
    }

    // ─────────────────────────────────────────────────────────
    void Awake() {
        _readyPlayers = new NetworkList<ulong>(
            new List<ulong>(),
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
    }

    public override void OnNetworkSpawn() {
        Instance = this;

        _readyPlayers.OnListChanged += OnReadyListChanged;
        _countdownEndTime.OnValueChanged += OnCountdownEndTimeChanged;
        _countdownLocked.OnValueChanged += OnCountdownLockedChanged;

        OnLobbyManagerSpawned?.Invoke();
    }

    public override void OnNetworkDespawn() {
        _readyPlayers.OnListChanged -= OnReadyListChanged;
        _countdownEndTime.OnValueChanged -= OnCountdownEndTimeChanged;
        _countdownLocked.OnValueChanged -= OnCountdownLockedChanged;

        if (Instance == this) Instance = null;
        OnLobbyManagerDespawned?.Invoke();
    }

    // ─────────────────────────────────────────────────────────
    // NetworkVariable callbacks — fire events for all UI listeners
    // ─────────────────────────────────────────────────────────
    void OnReadyListChanged(NetworkListEvent<ulong> change) {
        bool isReady = change.Type == NetworkListEvent<ulong>.EventType.Add;
        OnPlayerReadyChanged?.Invoke(change.Value, isReady);

        if (IsServer) CheckShouldStartCountdown();
    }

    void OnCountdownEndTimeChanged(double prev, double current) {
        if (current > 0)
            OnCountdownChanged?.Invoke(current, countdownDuration);
        else
            OnCountdownCancelled?.Invoke();
    }

    void OnCountdownLockedChanged(bool prev, bool current) {
        if (current) OnCountdownLocked?.Invoke();
    }

    // ─────────────────────────────────────────────────────────
    // Called by ReadyButtonUI (local player only)
    // ─────────────────────────────────────────────────────────
    [Rpc(SendTo.Server)]
    public void SetReadyRpc(ulong clientId, bool ready) {
        if (_countdownLocked.Value) return;   // can't change after lock

        if (ready) {
            bool alreadyIn = false;
            foreach (var id in _readyPlayers)
                if (id == clientId) { alreadyIn = true; break; }

            if (!alreadyIn) _readyPlayers.Add(clientId);
        } else {
            _readyPlayers.Remove(clientId);
            CancelCountdown();
        }
    }

    // ─────────────────────────────────────────────────────────
    // Server-side countdown logic
    // ─────────────────────────────────────────────────────────
    void CheckShouldStartCountdown() {
        int totalPlayers = NetworkManager.Singleton.ConnectedClientsList.Count;
        if (totalPlayers == 0) return;

        // All players must be ready
        if (_readyPlayers.Count >= totalPlayers && !IsCountdownActive)
            StartCountdown();
    }

    void StartCountdown() {
        _countdownLocked.Value = false;
        _countdownEndTime.Value = NetworkManager.Singleton.ServerTime.Time + countdownDuration;
        StartCoroutine(CountdownRoutine());
    }

    void CancelCountdown() {
        StopAllCoroutines();
        _countdownEndTime.Value = 0;
        _countdownLocked.Value = false;
    }

    IEnumerator CountdownRoutine() {
        // Wait until 3 seconds remain — then lock the button
        float lockDelay = countdownDuration - 3f;
        if (lockDelay > 0) yield return new WaitForSeconds(lockDelay);

        if (!IsCountdownActive) yield break;
        _countdownLocked.Value = true;

        // Wait the final 3 seconds
        yield return new WaitForSeconds(2.8f);
        ShowLoadingScreenClientRpc();
        yield return new WaitForSeconds(0.2f);

        if (!IsCountdownActive) yield break;

        // Load the level for everyone
        string levelScene = GameSessionManager.Instance != null
            ? GameSessionManager.Instance.SelectedLevelSceneName.Value.ToString()
            : "";

        if (string.IsNullOrEmpty(levelScene)) {
            Debug.LogError("[LobbyReadyManager] No level scene set in GameSessionManager.");
            CancelCountdown();
            yield break;
        }

        NetworkManager.Singleton.SceneManager.LoadScene(levelScene, UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    [Rpc(SendTo.ClientsAndHost)]
    void ShowLoadingScreenClientRpc() {
        if (!NetworkManager.Singleton.LocalClient.PlayerObject.TryGetComponent<Player>(out var localPlayer)) return;
        localPlayer.ShowLoadingScreenClientRpc();
    }
}