using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Screen-space Lobby UI — shows connected players, quiz name, level name.
// The Start Game button is GONE — replaced by the per-player ReadyButtonUI
// on each player's own PlayerCanvas.
// This canvas is optional; the primary lobby display is the
// LobbyWorldCanvasController (world-space, visible to everyone in 3D).
public class LobbyUI : MonoBehaviour {
    [Header("Session Info")]
    [SerializeField] TMP_Text quizNameLabel;
    [SerializeField] TMP_Text questionCountLabel;
    [SerializeField] TMP_Text levelNameLabel;
    [SerializeField] TMP_Text playerCountLabel;    // e.g. "2 / 4"

    [Header("Players")]
    [SerializeField] Transform playerListContainer;
    [SerializeField] LobbyPlayerItemUI playerItemPrefab;

    private readonly List<LobbyPlayerItemUI> _spawnedItems = new();

    // ─────────────────────────────────────────────────────────
    void Start() {
        if (GameSessionManager.Instance == null) {
            Debug.LogError("[LobbyUI] GameSessionManager.Instance is null. " +
                            "Did you reach this scene through the Main Menu's Host/Join flow?");
            return;
        }

        GameSessionManager.Instance.Players.OnListChanged += _ => RefreshPlayerList();
        GameSessionManager.Instance.SelectedQuizSetName.OnValueChanged += (_, _) => RefreshQuizInfo();
        GameSessionManager.Instance.SelectedLevelSceneName.OnValueChanged += (_, _) => RefreshLevelInfo();

        LobbyReadyManager.OnPlayerReadyChanged += OnPlayerReadyChanged;

        RefreshPlayerList();
        RefreshQuizInfo();
        RefreshLevelInfo();
    }

    void OnDestroy() {
        if (GameSessionManager.Instance != null) {
            GameSessionManager.Instance.Players.OnListChanged -= _ => RefreshPlayerList();
            GameSessionManager.Instance.SelectedQuizSetName.OnValueChanged -= (_, _) => RefreshQuizInfo();
            GameSessionManager.Instance.SelectedLevelSceneName.OnValueChanged -= (_, _) => RefreshLevelInfo();
        }

        LobbyReadyManager.OnPlayerReadyChanged -= OnPlayerReadyChanged;
    }

    // ─────────────────────────────────────────────────────────
    void OnPlayerReadyChanged(ulong clientId, bool isReady) => RefreshPlayerList();

    void RefreshPlayerList() {
        foreach (var item in _spawnedItems) Destroy(item.gameObject);
        _spawnedItems.Clear();

        foreach (var player in GameSessionManager.Instance.Players) {
            bool ready = LobbyReadyManager.Instance != null &&
                         LobbyReadyManager.Instance.IsReady(player.ClientId);

            var item = Instantiate(playerItemPrefab, playerListContainer);
            item.Setup(player.PlayerName.ToString(), ready);
            _spawnedItems.Add(item);
        }

        if (playerCountLabel != null)
            playerCountLabel.text = $"{GameSessionManager.Instance.Players.Count} / {ConnectionApprovalHandler.MaxPlayers}";
    }

    void RefreshQuizInfo() {
        string setName = GameSessionManager.Instance.SelectedQuizSetName.Value.ToString();
        if (quizNameLabel != null) quizNameLabel.text = setName;

        var set = QuizRepository.Instance?.GetSetByName(setName);
        if (questionCountLabel != null)
            questionCountLabel.text = set != null ? $"{set.questions.Count} questions" : "—";
    }

    void RefreshLevelInfo() {
        if (levelNameLabel != null)
            levelNameLabel.text = GameSessionManager.Instance
                .SelectedLevelSceneName.Value.ToString();
    }
}