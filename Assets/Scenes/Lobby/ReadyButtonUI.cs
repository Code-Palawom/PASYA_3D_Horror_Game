using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Screen-space ready button that lives on PlayerCanvas.
// Only visible when the player is in the Lobby scene
// (detected by LobbyReadyManager spawning/despawning).
//
// States:
//   "Ready"   — player is not ready, button is clickable
//   "Cancel"  — player is ready, button cancels readiness
//   Disabled  — countdown is locked (≤3s remaining)
//
// Input:
//   Enter key (or any binding set in Inspector) calls TryToggleReady,
//   which is also wired to the on-screen button's onClick.
//   Both paths are blocked while the countdown is locked.
public class ReadyButtonUI : NetworkBehaviour {
    [Header("References")]
    [SerializeField] GameObject panel;
    [SerializeField] Button readyButton;
    [SerializeField] TMP_Text buttonLabel;
    [SerializeField] TMP_Text countdownLabel;
    [SerializeField] Image countdownBar;    // Filled, Horizontal

    [Header("Colors")]
    [SerializeField] Color readyColor = Color.green;
    [SerializeField] Color cancelColor = Color.red;
    [SerializeField] Color disabledColor = Color.grey;

    [Header("Input")]
    [SerializeField] private InputAction _readyAction;

    private bool _isReady;

    // ─────────────────────────────────────────────────────────
    public override void OnNetworkSpawn() {
        // Only the local player manages their own ready button
        if (!IsOwner) { panel.SetActive(false); return; }

        LobbyReadyManager.OnLobbyManagerSpawned += OnLobbyEntered;
        LobbyReadyManager.OnLobbyManagerDespawned += OnLobbyExited;
        LobbyReadyManager.OnCountdownChanged += OnCountdownChanged;
        LobbyReadyManager.OnCountdownCancelled += OnCountdownCancelled;
        LobbyReadyManager.OnCountdownLocked += OnCountdownLocked;

        readyButton.onClick.AddListener(TryToggleReady);

        _readyAction.performed += _ => TryToggleReady();
        _readyAction.Enable();

        // If we spawned mid-lobby (late join scenario)
        if (LobbyReadyManager.Instance != null)
            OnLobbyEntered();
        else
            panel.SetActive(false);
    }

    public override void OnNetworkDespawn() {
        if (!IsOwner) return;

        LobbyReadyManager.OnLobbyManagerSpawned -= OnLobbyEntered;
        LobbyReadyManager.OnLobbyManagerDespawned -= OnLobbyExited;
        LobbyReadyManager.OnCountdownChanged -= OnCountdownChanged;
        LobbyReadyManager.OnCountdownCancelled -= OnCountdownCancelled;
        LobbyReadyManager.OnCountdownLocked -= OnCountdownLocked;

        _readyAction.Disable();
        _readyAction.Dispose();
    }

    // ─────────────────────────────────────────────────────────
    void OnLobbyEntered() {
        _isReady = false;
        panel.SetActive(true);
        SetButtonState(ready: false, locked: false);
        HideCountdown();
    }

    void OnLobbyExited() {
        panel.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────
    void TryToggleReady() {
        if (LobbyReadyManager.Instance == null) return;
        if (!readyButton.interactable) return;  // blocked when countdown is locked

        _isReady = !_isReady;
        SetButtonState(_isReady, locked: false);
        LobbyReadyManager.Instance.SetReadyRpc(NetworkManager.Singleton.LocalClientId, _isReady);
    }

    // ─────────────────────────────────────────────────────────
    void OnCountdownChanged(double endTime, float totalDuration) {
        if (countdownLabel != null) countdownLabel.gameObject.SetActive(true);
        if (countdownBar != null) countdownBar.gameObject.SetActive(true);
    }

    void OnCountdownCancelled() {
        SetButtonState(_isReady, locked: false);
        HideCountdown();
    }

    void OnCountdownLocked() {
        SetButtonState(_isReady, locked: true);
    }

    // ─────────────────────────────────────────────────────────
    void Update() {
        if (!IsOwner) return;
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

    // ─────────────────────────────────────────────────────────
    void SetButtonState(bool ready, bool locked) {
        readyButton.interactable = !locked;

        if (locked) {
            buttonLabel.text = "Starting...";
            buttonLabel.color = disabledColor;
        } else if (ready) {
            buttonLabel.text = "Cancel";
            buttonLabel.color = cancelColor;
        } else {
            buttonLabel.text = "Ready";
            buttonLabel.color = readyColor;
        }
    }

    void HideCountdown() {
        if (countdownLabel != null) countdownLabel.gameObject.SetActive(false);
        if (countdownBar != null) countdownBar.gameObject.SetActive(false);
    }
}