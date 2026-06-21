using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Main Menu UI — full navigation tree.
// Lives ONLY in the MainMenu scene.
//
// MainPanel
//  ├── Single Player → Level Select → Quiz Select → Start
//  │                    → StartHost() (no LAN broadcast) → Level scene directly
//  │
//  └── Multiplayer (panel)
//       ├── Host → Level Select → Quiz Select → Start Hosting
//       │           → StartHost() → broadcast on LAN → Lobby scene
//       └── Join → LAN room list/detail → Join
//                   → StartClient() → server auto-syncs scene
//
// Level Select and Quiz Select are shared by both Single Player and Host —
// only the final Start behavior differs, branched by _pendingMode.
public class MainMenuUI : MonoBehaviour {
    [Header("Scene Names")]
    [SerializeField] string lobbySceneName = "Lobby";

    [Header("Networking")]
    [SerializeField] ushort gamePort = 7777;

    [Header("GameSessionManager Prefab")]
    [Tooltip("Must be registered in NetworkPrefabsList.")]
    [SerializeField] GameObject gameSessionManagerPrefab;

    [Header("ChatManager Prefab")]
    [Tooltip("Must be registered in NetworkPrefabsList.")]
    [SerializeField] GameObject chatManagerPrefab;

    [Header("Level Options")]
    [Tooltip("One entry per playable level. Must match exact scene names in Build Settings.")]
    [SerializeField] List<LevelOption> availableLevels;

    // ── Main Panel (top-level) ─────────────────────────────
    [Header("Main Panel")]
    [SerializeField] GameObject mainPanel;
    [SerializeField] Button singlePlayerButton;
    [SerializeField] Button multiplayerButton;
    [SerializeField] Button characterButton;
    [SerializeField] Button settingsButton;
    [SerializeField] Button aboutButton;
    [SerializeField] Button exitButton;

    // ── Multiplayer Panel (Host / Join) ────────────────────
    [Header("Multiplayer Panel")]
    [SerializeField] GameObject multiplayerPanel;
    [SerializeField] Button hostButton;
    [SerializeField] Button multiplayerJoinButton;   // navigates INTO the Join panel
    [SerializeField] Button multiplayerBackButton;

    // ── Join Panel (room list + detail) ────────────────────
    [Header("Join Panel")]
    [SerializeField] GameObject joinPanel;
    [SerializeField] Button refreshButton;
    [SerializeField] Transform hostListContainer;
    [SerializeField] LobbyListItemUI hostListItemPrefab;
    [SerializeField] RoomDetailPanelUI roomDetailPanel;
    [SerializeField] Button joinButton;        // the actual "connect" action
    [SerializeField] TMP_Text joinStatusText;
    // Note: no separate Back button here — MultiplayerPanel's persistent
    // Back button (top tab row) handles exiting Multiplayer entirely,
    // since Join is now the default content shown within it.

    // ── Level Select Panel (shared by Host + Single Player) ─
    [Header("Level Select Panel")]
    [SerializeField] GameObject levelSelectPanel;
    [SerializeField] Transform levelListContainer;
    [SerializeField] LevelSelectItemUI levelItemPrefab;     // has a preview image
    [SerializeField] Button levelNextButton;
    [SerializeField] Button levelBackButton;

    // ── Quiz Select Panel (shared by Host + Single Player) ─
    [Header("Quiz Select Panel")]
    [SerializeField] GameObject quizSelectPanel;
    [SerializeField] Transform quizListContainer;
    [SerializeField] SelectableListItemUI quizItemPrefab;      // text-only, no image
    [SerializeField] Button startButton;
    [SerializeField] TMP_Text startButtonLabel;
    [SerializeField] Button quizBackButton;
    [SerializeField] TMP_Text statusText;

    // ── Placeholder Panels ──────────────────────────────────
    [Header("Character Panel")]
    [SerializeField] GameObject characterPanel;
    [SerializeField] Button characterBackButton;

    [Header("Settings Panel")]
    [SerializeField] SettingsUI settingsPanel;
    [SerializeField] Button settingsBackButton;

    [Header("About Panel")]
    [SerializeField] GameObject aboutPanel;
    [SerializeField] Button aboutBackButton;

    // ─────────────────────────────────────────────────────────
    private GameMode _pendingMode = GameMode.None;
    private string _selectedLevelSceneName;
    private string _selectedQuizSetName;
    private DiscoveredHost _selectedHost;

    private readonly List<LevelSelectItemUI> _levelItems = new();
    private readonly List<SelectableListItemUI> _quizItems = new();
    private readonly List<LobbyListItemUI> _spawnedHostItems = new();
    private readonly List<DiscoveredHost> _discoveredHosts = new();

    // ─────────────────────────────────────────────────────────
    void Start() {
        // Main panel
        singlePlayerButton.onClick.AddListener(() => EnterWizard(GameMode.SinglePlayer));
        multiplayerButton.onClick.AddListener(ShowMultiplayerPanel);
        characterButton.onClick.AddListener(ShowCharacterPanel);
        settingsButton.onClick.AddListener(ShowSettingsPanel);
        aboutButton.onClick.AddListener(ShowAboutPanel);
        exitButton.onClick.AddListener(OnExitClicked);

        // Multiplayer panel
        hostButton.onClick.AddListener(() => EnterWizard(GameMode.Host));
        multiplayerJoinButton.onClick.AddListener(ShowJoinPanel);
        multiplayerBackButton.onClick.AddListener(ShowMainPanel);

        // Level select
        levelNextButton.onClick.AddListener(ShowQuizSelectPanel);
        levelBackButton.onClick.AddListener(OnLevelBackClicked);

        // Quiz select
        quizBackButton.onClick.AddListener(ShowLevelSelectPanel);
        startButton.onClick.AddListener(OnStartClicked);

        refreshButton.onClick.AddListener(OnRefreshHostsClicked);
        joinButton.onClick.AddListener(OnJoinClicked);

        // Placeholder panels
        characterBackButton.onClick.AddListener(ShowMainPanel);
        settingsBackButton.onClick.AddListener(ShowMainPanel);
        aboutBackButton.onClick.AddListener(ShowMainPanel);

        ShowMainPanel();
    }

    // ─────────────────────────────────────────────────────────
    // PANEL SWITCHING
    // ─────────────────────────────────────────────────────────
    // MultiplayerPanel (the Host/Join/Back tab row) is intentionally
    // NOT included here — its visibility is managed separately via
    // SetMultiplayerTabRowVisible() so it can persist across Host's
    // Level Select step and the Join room browser, only disappearing
    // once Quiz Select is reached.
    void HideAllContentPanels() {
        mainPanel.SetActive(false);
        joinPanel.SetActive(false);
        levelSelectPanel.SetActive(false);
        quizSelectPanel.SetActive(false);
        characterPanel.SetActive(false);
        settingsPanel.Hide();
        aboutPanel.SetActive(false);
    }

    void SetMultiplayerTabRowVisible(bool visible) => multiplayerPanel.SetActive(visible);

    void ShowMainPanel() {
        _pendingMode = GameMode.None;
        HideAllContentPanels();
        SetMultiplayerTabRowVisible(false);
        mainPanel.SetActive(true);
    }

    void ShowMultiplayerPanel() {
        // Host is the default content shown when entering Multiplayer
        EnterWizard(GameMode.Host);
    }

    void ShowCharacterPanel() { HideAllContentPanels(); SetMultiplayerTabRowVisible(false); characterPanel.SetActive(true); }
    void ShowSettingsPanel() {
        HideAllContentPanels();
        SetMultiplayerTabRowVisible(false);
        settingsPanel.Show();
    }
    void ShowAboutPanel() { HideAllContentPanels(); SetMultiplayerTabRowVisible(false); aboutPanel.SetActive(true); }

    void OnExitClicked() {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ─────────────────────────────────────────────────────────
    void ShowJoinPanel() {
        HideAllContentPanels();
        SetMultiplayerTabRowVisible(true);   // Host/Join tabs stay visible
        joinPanel.SetActive(true);
        roomDetailPanel?.ShowEmpty();
        joinButton.interactable = false;

        // Auto-search on entry — removes the extra click, matches how
        // most multiplayer browsers behave. The manual Refresh button
        // stays available for re-searching afterward.
        OnRefreshHostsClicked();
    }

    // ─────────────────────────────────────────────────────────
    void EnterWizard(GameMode mode) {
        _pendingMode = mode;
        _selectedLevelSceneName = null;
        _selectedQuizSetName = null;

        ShowLevelSelectPanel();
    }

    void ShowLevelSelectPanel() {
        HideAllContentPanels();

        bool isHostFlow = _pendingMode == GameMode.Host;

        // Tab row stays visible only when this is the Host flow —
        // Single Player never shows Host/Join tabs at all.
        SetMultiplayerTabRowVisible(isHostFlow);

        // When the tab row's own Back button is already visible (Host flow),
        // this panel's own Back button would be a redundant duplicate —
        // hide it. Single Player has no tab row, so it needs its own.
        levelBackButton.gameObject.SetActive(!isHostFlow);

        levelSelectPanel.SetActive(true);

        PopulateLevelList();
        levelNextButton.interactable = !string.IsNullOrEmpty(_selectedLevelSceneName);
    }

    // Only reachable from Single Player now (the button is hidden
    // during the Host flow), so it always returns to MainPanel.
    void OnLevelBackClicked() => ShowMainPanel();

    void ShowQuizSelectPanel() {
        HideAllContentPanels();
        SetMultiplayerTabRowVisible(false);   // Host/Join tabs disappear here, per design

        quizSelectPanel.SetActive(true);

        PopulateQuizList();
        startButtonLabel.text = _pendingMode == GameMode.Host ? "Start Hosting" : "Start Single Player";
        startButton.interactable = !string.IsNullOrEmpty(_selectedQuizSetName);
        statusText.text = "";
    }

    // ─────────────────────────────────────────────────────────
    // LEVEL LIST
    // ─────────────────────────────────────────────────────────
    void PopulateLevelList() {
        foreach (var item in _levelItems) Destroy(item.gameObject);
        _levelItems.Clear();

        if (availableLevels == null || availableLevels.Count == 0) {
            Debug.LogWarning("[MainMenuUI] No levels configured in Available Levels.");
            return;
        }

        foreach (var level in availableLevels) {
            var item = Instantiate(levelItemPrefab, levelListContainer);
            item.Setup(level, OnLevelSelected);
            _levelItems.Add(item);
        }
    }

    void OnLevelSelected(string sceneName) {
        _selectedLevelSceneName = sceneName;
        levelNextButton.interactable = true;

        foreach (var item in _levelItems)
            item.SetSelected(item.Value == sceneName);
    }

    // ─────────────────────────────────────────────────────────
    // QUIZ LIST
    // ─────────────────────────────────────────────────────────
    void PopulateQuizList() {
        foreach (var item in _quizItems) Destroy(item.gameObject);
        _quizItems.Clear();

        if (QuizRepository.Instance == null) {
            Debug.LogWarning("[MainMenuUI] QuizRepository.Instance is null.");
            return;
        }

        var names = QuizRepository.Instance.GetAllSetNames();
        if (names.Count == 0) {
            Debug.LogWarning("[MainMenuUI] No quiz sets available.");
            return;
        }

        foreach (var name in names) {
            var item = Instantiate(quizItemPrefab, quizListContainer);
            item.Setup(name, name, OnQuizSelected);
            _quizItems.Add(item);
        }
    }

    void OnQuizSelected(string quizName) {
        _selectedQuizSetName = quizName;
        startButton.interactable = true;

        foreach (var item in _quizItems)
            item.SetSelected(item.Value == quizName);
    }

    // ─────────────────────────────────────────────────────────
    // START — branches based on which entry point opened the wizard
    // ─────────────────────────────────────────────────────────
    void OnStartClicked() {
        if (_pendingMode == GameMode.Host)
            StartAsHost();
        else if (_pendingMode == GameMode.SinglePlayer)
            StartAsSinglePlayer();
    }

    void StartAsHost() {
        GameModeManager.Instance.SetHostMode(_selectedQuizSetName, _selectedLevelSceneName);

        statusText.text = "Starting host...";
        startButton.interactable = false;

        ConfigureTransport("0.0.0.0", gamePort);
        NetworkManager.Singleton.OnServerStarted += OnHostServerStarted;
        NetworkManager.Singleton.StartHost();
    }

    void OnHostServerStarted() {
        NetworkManager.Singleton.OnServerStarted -= OnHostServerStarted;

        SpawnGameSessionManager(
            GameModeManager.Instance.SelectedQuizSetName,
            GameModeManager.Instance.SelectedLevelSceneName
        );
        SpawnChatManager();

        int questionCount = QuizRepository.Instance
            .GetSetByName(GameModeManager.Instance.SelectedQuizSetName)?.questions.Count ?? 0;

        LanDiscovery.Instance.StartHostBroadcast(
            hostName: SystemInfo.deviceName,
            quizName: GameModeManager.Instance.SelectedQuizSetName,
            levelSceneName: GameModeManager.Instance.SelectedLevelSceneName,
            questionCount: questionCount,
            gamePort: gamePort,
            playerNamesProvider: GetCurrentPlayerNames
        );

        NetworkManager.Singleton.SceneManager.LoadScene(lobbySceneName, LoadSceneMode.Single);
    }

    void StartAsSinglePlayer() {
        GameModeManager.Instance.SetSinglePlayerMode(_selectedQuizSetName, _selectedLevelSceneName);

        statusText.text = "Starting...";
        startButton.interactable = false;

        ConfigureTransport("127.0.0.1", gamePort);
        NetworkManager.Singleton.OnServerStarted += OnSinglePlayerServerStarted;
        NetworkManager.Singleton.StartHost();
    }

    void OnSinglePlayerServerStarted() {
        NetworkManager.Singleton.OnServerStarted -= OnSinglePlayerServerStarted;

        SpawnGameSessionManager(
            GameModeManager.Instance.SelectedQuizSetName,
            GameModeManager.Instance.SelectedLevelSceneName
        );
        SpawnChatManager();

        // No LAN broadcast, no Lobby — straight to the chosen level
        NetworkManager.Singleton.SceneManager.LoadScene(
            GameModeManager.Instance.SelectedLevelSceneName, LoadSceneMode.Single);
    }

    // ─────────────────────────────────────────────────────────
    // NetworkList<T> implements IEnumerable<T> explicitly, so LINQ's
    // .Select() can't resolve directly on it — foreach works fine though.
    // ─────────────────────────────────────────────────────────
    List<string> GetCurrentPlayerNames() {
        var names = new List<string>();
        if (GameSessionManager.Instance != null)
            foreach (var p in GameSessionManager.Instance.Players)
                names.Add(p.PlayerName.ToString());
        return names;
    }

    // ─────────────────────────────────────────────────────────
    // JOIN — LAN discovery
    // ─────────────────────────────────────────────────────────
    void OnRefreshHostsClicked() {
        foreach (var item in _spawnedHostItems) Destroy(item.gameObject);
        _spawnedHostItems.Clear();
        _discoveredHosts.Clear();

        joinButton.interactable = false;
        joinStatusText.text = "Searching for LAN hosts...";
        roomDetailPanel?.ShowEmpty();

        LanDiscovery.Instance.StartClientDiscovery(OnHostDiscovered, duration: 4f);
    }

    void OnHostDiscovered(DiscoveredHost host) {
        string levelDisplayName = ResolveLevelDisplayName(host.LevelSceneName);

        var item = Instantiate(hostListItemPrefab, hostListContainer);
        item.Setup(host, levelDisplayName, OnHostSelected);
        _spawnedHostItems.Add(item);
        _discoveredHosts.Add(host);

        joinStatusText.text = $"{_spawnedHostItems.Count} game(s) found.";
    }

    void OnHostSelected(DiscoveredHost host) {
        _selectedHost = host;
        joinButton.interactable = true;
        foreach (var item in _spawnedHostItems) item.SetSelected(false);
        int index = _discoveredHosts.IndexOf(host);
        if (index >= 0 && index < _spawnedHostItems.Count)
            _spawnedHostItems[index].SetSelected(true);
        var levelOption = availableLevels.FirstOrDefault(l => l.sceneName == host.LevelSceneName);
        roomDetailPanel.Show(
            host,
            levelOption?.displayName ?? host.LevelSceneName,
            levelOption?.previewImage
        );
    }

    string ResolveLevelDisplayName(string sceneName) =>
        availableLevels.FirstOrDefault(l => l.sceneName == sceneName)?.displayName ?? sceneName;

    void OnJoinClicked() {
        if (_selectedHost == null) return;

        GameModeManager.Instance.SetClientMode(_selectedHost.Address, _selectedHost.GamePort);
        ConfigureTransport(_selectedHost.Address, _selectedHost.GamePort);

        joinStatusText.text = "Connecting...";

        // Set player name payload before connecting
        NetworkManager.Singleton.NetworkConfig.ConnectionData = System.Text.Encoding.UTF8.GetBytes(GameModeManager.Instance.LocalPlayerName);

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientJoined;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientJoinFailed;
        NetworkManager.Singleton.StartClient();
    }

    void OnClientJoined(ulong clientId) {
        if (clientId != NetworkManager.Singleton.LocalClientId) return;

        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientJoined;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientJoinFailed;

        joinStatusText.text = "Connected!";
        // Server controls scene sync automatically once connected.
    }

    void OnClientJoinFailed(ulong clientId) {
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientJoined;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientJoinFailed;

        // Read the disconnect reason sent by the server's approval callback
        string reason = NetworkManager.Singleton.DisconnectReason;

        joinStatusText.text = reason switch {
            ConnectionApprovalHandler.ReasonFull => "Cannot join — lobby is full (4/4).",
            ConnectionApprovalHandler.ReasonInProgress => "Cannot join — game is already in progress.",
            ConnectionApprovalHandler.ReasonCountdown => "Cannot join — game is about to start.",
            _ => "Failed to connect."
        };
    }

    // ─────────────────────────────────────────────────────────
    void ConfigureTransport(string address, ushort port) {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetConnectionData(address, port);
    }

    void SpawnGameSessionManager(string quizSetName, string levelSceneName) {
        if (GameSessionManager.Instance != null) {
            GameSessionManager.Instance.SetSelectedQuizSet(quizSetName);
            GameSessionManager.Instance.SetSelectedLevel(levelSceneName);
            return;
        }

        var go = Instantiate(gameSessionManagerPrefab);
        go.GetComponent<NetworkObject>().Spawn();

        var session = go.GetComponent<GameSessionManager>();
        session.SetSelectedQuizSet(quizSetName);
        session.SetSelectedLevel(levelSceneName);
    }

    void SpawnChatManager() {
        if (ChatManager.Instance != null) return;

        var go = Instantiate(chatManagerPrefab);
        go.GetComponent<NetworkObject>().Spawn();
        ChatManager.Instance.SendSystemMessage("Welcome ALPHA Tester(s). Game breaking bugs are to be expected, embrace yourselves :)");
    }
}