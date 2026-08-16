using UnityEngine;

public enum GameMode { None, Host, Client, SinglePlayer }
public enum ConnectionMode { LAN, Relay }

// Persistent, non-networked settings carried from the Main Menu
// into Lobby/Level. Exists BEFORE NetworkManager starts, since the
// host/join/singleplayer choice happens before any connection.
public class GameModeManager : MonoBehaviour {
    public static GameModeManager Instance { get; private set; }

    public GameMode Mode { get; private set; } = GameMode.None;

    // ── Connection mode (LAN vs online Relay) ─────────────────
    public ConnectionMode ActiveConnectionMode { get; private set; } = ConnectionMode.LAN;
    public bool IsRelayMode => ActiveConnectionMode == ConnectionMode.Relay;

    // Populated by RelayManager.CreateRelayAsync() — read by Lobby UI to display to the host.
    public string RelayJoinCode { get; private set; } = "";
    public string SelectedQuizSetId { get; private set; } = "";
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
            LocalPlayerName = AuthManager.Instance.CurrentProfile?.DisplayName ?? SettingsManager.Instance.Current.playerName;
        else
            Debug.LogWarning("[GameModeManager] SettingsManager not ready in Start — using default name.");
    }

    public void SetHostMode(string quizSetId, string quizSetName, string levelSceneName) {
        Mode = GameMode.Host;
        SelectedQuizSetId = quizSetId;
        SelectedQuizSetName = quizSetName;
        SelectedLevelSceneName = levelSceneName;
    }

    public void SetSinglePlayerMode(string quizSetId, string quizSetName, string levelSceneName) {
        Mode = GameMode.SinglePlayer;
        SelectedQuizSetId = quizSetId;
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

    // Called from MainMenuUI before StartHost/StartClient to record which
    // transport path was used — other systems (e.g. Lobby UI) read IsRelayMode.
    public void SetConnectionMode(ConnectionMode mode) {
        ActiveConnectionMode = mode;
    }

    // Stored here so the Lobby UI can read it without a direct RelayManager reference.
    public void SetRelayJoinCode(string code) {
        RelayJoinCode = code;
    }

    // ── Online session options ────────────────────────────────
    // True when the host created a Unity Lobby entry for public discovery.
    public bool IsPublicSession { get; private set; }

    public void SetIsPublicSession(bool isPublic) {
        IsPublicSession = isPublic;
    }
}