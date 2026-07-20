using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

// World-space canvas placed in the Lobby scene (e.g. on a wall or podium).
// Visible to all players in 3D space.
// Shows: quiz name, selected level, player list with ready indicators,
// and the countdown timer.
public class LobbyWorldCanvasController : MonoBehaviour {
    [Header("Session Info")]
    [SerializeField] TMP_Text quizNameLabel;
    [SerializeField] TMP_Text levelNameLabel;
    [SerializeField] TMP_Text playerCountLabel;   // e.g. "2 / 4"
    [SerializeField] TMP_Text lobbyCode;

    [Header("Player List")]
    [SerializeField] Transform playerListContainer;
    [SerializeField] LobbyPlayerItemUI playerItemPrefab;

    [Header("Countdown")]
    [SerializeField] GameObject countdownPanel;
    [SerializeField] TMP_Text countdownLabel;
    [SerializeField] Image countdownBar;

    [Header("Status")]
    [SerializeField] TMP_Text statusLabel;   // e.g. "Waiting for players..." / "Starting!"

    private readonly List<LobbyPlayerItemUI> _playerItems = new();

    // ─────────────────────────────────────────────────────────
    void OnEnable() {
        LobbyReadyManager.OnLobbyManagerSpawned += OnManagerSpawned;
        LobbyReadyManager.OnLobbyManagerDespawned += OnManagerDespawned;
        LobbyReadyManager.OnPlayerReadyChanged += OnPlayerReadyChanged;
        LobbyReadyManager.OnCountdownChanged += OnCountdownChanged;
        LobbyReadyManager.OnCountdownCancelled += OnCountdownCancelled;
        LobbyReadyManager.OnCountdownLocked += OnCountdownLocked;

        // Late subscribe — manager already exists
        if (LobbyReadyManager.Instance != null)
            OnManagerSpawned();
    }

    void OnDisable() {
        LobbyReadyManager.OnLobbyManagerSpawned -= OnManagerSpawned;
        LobbyReadyManager.OnLobbyManagerDespawned -= OnManagerDespawned;
        LobbyReadyManager.OnPlayerReadyChanged -= OnPlayerReadyChanged;
        LobbyReadyManager.OnCountdownChanged -= OnCountdownChanged;
        LobbyReadyManager.OnCountdownCancelled -= OnCountdownCancelled;
        LobbyReadyManager.OnCountdownLocked -= OnCountdownLocked;
    }

    // ─────────────────────────────────────────────────────────
    void OnManagerSpawned() {
        RefreshSessionInfo();
        RefreshPlayerList();
        countdownPanel?.SetActive(false);
        SetStatus("Waiting for players to ready up...");

        // Subscribe to player list changes
        if (GameSessionManager.Instance != null)
            GameSessionManager.Instance.Players.OnListChanged += _ => RefreshPlayerList();
    }

    void OnManagerDespawned() {
        if (GameSessionManager.Instance != null)
            GameSessionManager.Instance.Players.OnListChanged -= _ => RefreshPlayerList();
    }

    // ─────────────────────────────────────────────────────────
    void RefreshSessionInfo() {
        if (GameSessionManager.Instance == null) return;

        quizNameLabel.text = GameSessionManager.Instance.SelectedQuizSetName.Value.ToString();
        levelNameLabel.text = GameSessionManager.Instance.SelectedLevelSceneName.Value.ToString();
        if (GameModeManager.Instance.IsRelayMode) lobbyCode.text = $"Lobby Code: {GameModeManager.Instance.RelayJoinCode}";
        else lobbyCode.gameObject.SetActive(false);
    }

    void RefreshPlayerList() {
        string activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        bool inLevel = GameSessionManager.Instance != null &&
                       activeScene == GameSessionManager.Instance
                           .SelectedLevelSceneName.Value.ToString();
        if (inLevel) return; // Don't show player list in the level scene

        foreach (var item in _playerItems) Destroy(item.gameObject);
        _playerItems.Clear();

        if (GameSessionManager.Instance == null) return;

        foreach (var player in GameSessionManager.Instance.Players) {
            bool isReady = LobbyReadyManager.Instance != null &&
                           LobbyReadyManager.Instance.IsReady(player.ClientId);

            var item = Instantiate(playerItemPrefab, playerListContainer);
            item.Setup(player.PlayerName.ToString(), isReady);
            _playerItems.Add(item);
        }

        if (playerCountLabel != null)
            playerCountLabel.text = $"{GameSessionManager.Instance.Players.Count} / {ConnectionApprovalHandler.MaxPlayers}";
    }

    void OnPlayerReadyChanged(ulong clientId, bool isReady) {
        RefreshPlayerList();
    }

    // ─────────────────────────────────────────────────────────
    void OnCountdownChanged(double endTime, float totalDuration) {
        countdownPanel?.SetActive(true);
        SetStatus("All players ready!");
    }

    void OnCountdownCancelled() {
        countdownPanel?.SetActive(false);
        SetStatus("Waiting for players to ready up...");
    }

    void OnCountdownLocked() {
        SetStatus("Starting!");
    }

    // ─────────────────────────────────────────────────────────
    void Update() {
        if (LobbyReadyManager.Instance == null) return;
        if (!LobbyReadyManager.Instance.IsCountdownActive) return;

        float remaining = Mathf.Max(0f,
            (float)(LobbyReadyManager.Instance.CountdownEndTime
                    - NetworkManager.Singleton.ServerTime.Time));

        float t = LobbyReadyManager.Instance.TotalDuration > 0
            ? remaining / LobbyReadyManager.Instance.TotalDuration
            : 0f;

        if (countdownLabel != null)
            countdownLabel.text = $"{remaining:F1}s";

        if (countdownBar != null)
            countdownBar.fillAmount = t;
    }

    void SetStatus(string msg) {
        if (statusLabel != null) statusLabel.text = msg;
    }
}