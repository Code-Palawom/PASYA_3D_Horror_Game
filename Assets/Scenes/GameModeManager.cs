using UnityEngine;

public enum GameMode { None, Host, Client, SinglePlayer }

// Persistent, non-networked settings carried from the Main Menu
// into Lobby/Level. Exists BEFORE NetworkManager starts, since the
// host/join/singleplayer choice happens before any connection.
public class GameModeManager : MonoBehaviour {
    public static GameModeManager Instance { get; private set; }

    public GameMode Mode { get; private set; } = GameMode.None;
    public string SelectedQuizSetName { get; private set; } = "";
    public string SelectedLevelSceneName { get; private set; } = "";
    public string JoinAddress { get; private set; } = "127.0.0.1";
    public ushort JoinPort { get; private set; } = 7777;
    public string LocalPlayerName { get; set; } = "Player";

    void Awake() {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start() {
        // Start() is safe here — all Awake()s (including SettingsManager's) have run by now
        if (AuthManager.Instance != null)
            LocalPlayerName = AuthManager.Instance.CurrentProfile?.DisplayName ?? "Player";
        else
            Debug.LogWarning("[GameModeManager] SettingsManager not ready in Start — using default name.");
    }

    public void SetHostMode(string quizSetName, string levelSceneName) {
        Mode = GameMode.Host;
        SelectedQuizSetName = quizSetName;
        SelectedLevelSceneName = levelSceneName;
    }

    public void SetSinglePlayerMode(string quizSetName, string levelSceneName) {
        Mode = GameMode.SinglePlayer;
        SelectedQuizSetName = quizSetName;
        SelectedLevelSceneName = levelSceneName;
    }

    public void SetClientMode(string address, ushort port) {
        Mode = GameMode.Client;
        JoinAddress = address;
        JoinPort = port;
        // Joiners don't pick quiz/level — the host's selection applies,
        // synced via GameSessionManager once connected.
    }
}