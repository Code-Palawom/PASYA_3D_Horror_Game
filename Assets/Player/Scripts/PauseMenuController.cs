using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PauseMenuController : MonoBehaviour {

    [Header("Panels")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private SettingsPanelController settingsPanel;
    [SerializeField] private InputAction action;

    [SerializeField] private Player player;

    private bool isPaused;

    void Awake() {
        action.performed += _ => OnActionPerformed();
        action.Enable();
    }

    void OnDestroy() {
        action.Disable();
        action.Dispose();
    }

    void Start() {
        pausePanel.SetActive(false);
        settingsPanel.gameObject.SetActive(false);
    }

    // ── Input handler — context-aware ────────────────────────
    void OnActionPerformed() {
        if (settingsPanel.gameObject.activeSelf) {
            OnBackFromSettings();  // settings → pause panel
        } else if (isPaused) {
            Resume();              // pause panel → resume
        } else {
            Pause();               // game → pause panel
        }
    }

    // ── Pause Panel buttons ──────────────────────────────────
    public void OnResumeButton() => Resume();

    public void OnSettingsButton() {
        pausePanel.SetActive(false);
        settingsPanel.gameObject.SetActive(true);
        settingsPanel.Open();
    }

    public void OnBackToMainMenu() {
        Time.timeScale = 1f;

        if (IsMultiplayer())
            NetworkManager.Singleton.Shutdown();

        SceneManager.LoadScene("MainMenu");
    }

    // ── Called by SettingsPanelController's Back button ─────
    public void OnBackFromSettings() {
        settingsPanel.gameObject.SetActive(false);
        pausePanel.SetActive(true);
    }

    // ── Internal ─────────────────────────────────────────────
    void Pause() {
        isPaused = true;

        if (!IsMultiplayer())
            Time.timeScale = 0f;

        pausePanel.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Resume() {
        isPaused = false;

        if (!IsMultiplayer())
            Time.timeScale = 1f;

        pausePanel.SetActive(false);
        settingsPanel.gameObject.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        player.RefreshPOV();  // refresh camera POV after unpausing
    }

    bool IsMultiplayer() {
        return GameModeManager.Instance != null &&
               GameModeManager.Instance.Mode != GameMode.SinglePlayer;
    }
}