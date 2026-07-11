using System.Collections;
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
    [SerializeField] LevelSelectItemUI levelItemPrefab;
    [SerializeField] Button levelNextButton;
    [SerializeField] Button levelBackButton;

    // ── Quiz Select Panel (shared by Host + Single Player) ─
    [Header("Quiz Select Panel")]
    [SerializeField] GameObject quizSelectPanel;
    [SerializeField] Transform quizListContainer;
    [SerializeField] QuizSetItemUI quizItemPrefab;
    [SerializeField] Button startButton;
    [SerializeField] TMP_Text startButtonLabel;
    [SerializeField] Button quizBackButton;

    // ── Category Filter Dropdown ───────────────────────────────
    [Header("Category Filter")]
    [Tooltip("TMP_Dropdown for filtering quiz sets by category.")]
    [SerializeField] TMP_Dropdown categoryDropdown;

    // ── Quiz Select Status Bar ─────────────────────────────────
    [Header("Quiz Status Bar")]
    [SerializeField] TMP_Text statusText;
    [SerializeField] Color statusColorOk = new Color(0.2f, 0.8f, 0.2f);
    [SerializeField] Color statusColorErr = new Color(0.9f, 0.2f, 0.2f);

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
    private string _selectedQuizSetId;
    private DiscoveredHost _selectedHost;

    private readonly List<LevelSelectItemUI> _levelItems = new();
    private readonly List<QuizSetItemUI> _quizItems = new();
    private readonly List<LobbyListItemUI> _spawnedHostItems = new();
    private readonly List<DiscoveredHost> _discoveredHosts = new();
    private readonly HashSet<string> _renderedQuizSetIds = new();

    // Tracks known categories — "All" is always index 0
    private readonly List<string> _categories = new();
    private string _activeCategory = ""; // empty = All

    // ─────────────────────────────────────────────────────────
    void OnEnable() {
        QuizFetcher.Instance.OnSetReady += HandleSetReady;
        QuizFetcher.Instance.OnFetchStatus += HandleFetchStatus;
    }

    void OnDisable() {
        QuizFetcher.Instance.OnSetReady -= HandleSetReady;
        QuizFetcher.Instance.OnFetchStatus -= HandleFetchStatus;

        if (FirebaseManager.Instance != null)
            FirebaseManager.Instance.OnFirebaseReady -= OnFirebaseReady;
    }

    // Called once when Firebase finishes initializing.
    // Populates Firestore-backed quiz sets and attaches the _meta listener.
    void OnFirebaseReady() {
        // Unsubscribe — only needs to fire once
        Debug.Log("[MainMenuUI] Firebase ready, loading Firestore-backed quiz sets...");
        FirebaseManager.Instance.OnFirebaseReady -= OnFirebaseReady;

        // Load Firestore cache and merge with local SO dropdown
        List<QuizSetMetaEntry> cached = QuizFetcher.Instance.LoadCacheImmediately();

        var allMeta = new List<QuizSetMetaEntry>(QuizRepository.Instance.GetAllMeta());
        allMeta.AddRange(cached);
        InitDropdown(allMeta);

        foreach (var entry in cached)
            if (entry.hasLocalData)
                SpawnQuizCard(entry);

        // Attach _meta snapshot listener — fires immediately + on every remote change
        QuizFetcher.Instance.StartListening();
    }

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

        // Category dropdown
        categoryDropdown.onValueChanged.AddListener(OnCategoryFilterChanged);

        // Join panel
        refreshButton.onClick.AddListener(OnRefreshHostsClicked);
        joinButton.onClick.AddListener(OnJoinClicked);

        // Placeholder panels
        characterBackButton.onClick.AddListener(ShowMainPanel);
        settingsBackButton.onClick.AddListener(ShowMainPanel);
        aboutBackButton.onClick.AddListener(ShowMainPanel);

        // ── Firebase quiz sets ────────────────────────────────
        SetStatus("Checking for updates...", Color.gray);

        // Step 1: populate local SO sets immediately (no Firebase needed)
        List<QuizSetMetaEntry> localMeta = QuizRepository.Instance.GetLocalSetMeta();
        InitDropdown(localMeta);
        foreach (var entry in localMeta)
            SpawnQuizCard(entry);

        if(FirebaseManager.Instance.IsReady) OnFirebaseReady();

        // Step 2: wait for Firebase — Firestore cache + listener start after Init() completes
        FirebaseManager.Instance.OnFirebaseReady += OnFirebaseReady;

        ShowMainPanel();
    }

    // ─────────────────────────────────────────────────────────
    // CATEGORY DROPDOWN
    // ─────────────────────────────────────────────────────────

    /// <summary>Builds dropdown from known categories. Always "All" at index 0.</summary>
    void InitDropdown(List<QuizSetMetaEntry> entries) {
        _categories.Clear();

        var cats = entries
            .Where(e => !string.IsNullOrWhiteSpace(e.category))
            .Select(e => e.category)
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        _categories.AddRange(cats);
        RebuildDropdownOptions();
    }

    /// <summary>
    /// Adds a category to the dropdown if not already present.
    /// Called when a new set arrives via OnSetReady.
    /// </summary>
    void TryAddCategory(string category) {
        if (string.IsNullOrWhiteSpace(category)) return;
        if (_categories.Contains(category)) return;

        _categories.Add(category);
        _categories.Sort();
        RebuildDropdownOptions();
    }

    void RebuildDropdownOptions() {
        // Preserve current selection if possible
        string previousActive = _activeCategory;

        categoryDropdown.ClearOptions();
        var options = new List<string> { "All" };
        options.AddRange(_categories);
        categoryDropdown.AddOptions(options);

        // Restore selection — find "All" (0) or the previously selected category
        int idx = string.IsNullOrEmpty(previousActive)
            ? 0
            : options.IndexOf(previousActive);
        categoryDropdown.SetValueWithoutNotify(idx < 0 ? 0 : idx);
    }

    void OnCategoryFilterChanged(int index) {
        // index 0 = "All" → empty filter string
        _activeCategory = index == 0 ? "" : _categories[index - 1];
        ApplyFilterToAllCards();
    }

    void ApplyFilterToAllCards() {
        foreach (var item in _quizItems)
            item.ApplyFilter(_activeCategory);
    }

    // ─────────────────────────────────────────────────────────
    // STATUS BAR
    // ─────────────────────────────────────────────────────────

    void HandleFetchStatus(bool success, string message) {
        SetStatus(message, success ? statusColorOk : statusColorErr);
    }

    void SetStatus(string message, Color color) {
        if (statusText == null) return;
        statusText.text = message;
        statusText.color = color;
    }

    // ─────────────────────────────────────────────────────────
    // QUIZ LIST
    // ─────────────────────────────────────────────────────────

    void SpawnQuizCard(QuizSetMetaEntry entry) {
        if (_renderedQuizSetIds.Contains(entry.setId)) return;
        _renderedQuizSetIds.Add(entry.setId);

        var item = Instantiate(quizItemPrefab, quizListContainer);
        item.Setup(entry, OnQuizSelected);

        // Respect active filter immediately
        item.ApplyFilter(_activeCategory);

        _quizItems.Add(item);

        // Add category to dropdown if new
        TryAddCategory(entry.category);
    }

    /// <summary>
    /// Fires per verified set as each background download completes.
    /// Adds card live; hides it immediately if it doesn't match the active filter.
    /// </summary>
    void HandleSetReady(QuizSetMetaEntry entry) {
        SpawnQuizCard(entry);
    }

    void OnQuizSelected(string setName, string setId) {
        _selectedQuizSetName = setName;
        _selectedQuizSetId = setId;
        startButton.interactable = true;

        foreach (var item in _quizItems)
            item.SetSelected(item.SetId == setId);
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

    void ShowMultiplayerPanel() => EnterWizard(GameMode.Host);

    void ShowCharacterPanel() {
        HideAllContentPanels();
        SetMultiplayerTabRowVisible(false);
        characterPanel.SetActive(true);
    }

    void ShowSettingsPanel() {
        HideAllContentPanels();
        SetMultiplayerTabRowVisible(false);
        settingsPanel.Show();
    }

    void ShowAboutPanel() {
        HideAllContentPanels();
        SetMultiplayerTabRowVisible(false);
        aboutPanel.SetActive(true);
    }

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
        roomDetailPanel.ShowEmpty();
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
        _selectedQuizSetId = null;
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
    // START
    // ─────────────────────────────────────────────────────────

    // ─────────────────────────────────────────────────────────
    // START — branches based on which entry point opened the wizard
    // ─────────────────────────────────────────────────────────
    void OnStartClicked() {
        if (_pendingMode == GameMode.Host)
            StartCoroutine(StartAsHost());
        else if (_pendingMode == GameMode.SinglePlayer)
            StartCoroutine(StartAsSinglePlayer());
    }

    IEnumerator StartAsHost() {
        GameModeManager.Instance.SetHostMode(_selectedQuizSetName, _selectedLevelSceneName);

        statusText.text = "";
        startButton.interactable = false;
        ConfigureTransport("0.0.0.0", gamePort);

        LoadingScreenController.Instance.Show("Starting host...");
        yield return new WaitForSeconds(1f);

        NetworkManager.Singleton.OnServerStarted += OnHostServerStarted;
        NetworkManager.Singleton.StartHost();

        LoadingScreenController.Instance.SetMessage("Entering lobby...");
    }

    void OnHostServerStarted() {
        NetworkManager.Singleton.OnServerStarted -= OnHostServerStarted;

        SpawnGameSessionManager(
            GameModeManager.Instance.SelectedQuizSetName,
            GameModeManager.Instance.SelectedLevelSceneName
        );
        SpawnChatManager();

        // Call this when the game ends, not on start:
        // QuizFetcher.Instance.IncrementPlayCount(_selectedQuizSetId);

        int questionCount = QuizRepository.Instance
            .GetSetByName(GameModeManager.Instance.SelectedQuizSetName)?.questions.Count ?? 0;

        LanDiscovery.Instance.StartHostBroadcast(
            hostName: AuthManager.Instance.CurrentUser?.DisplayName ?? SystemInfo.deviceName,
            quizName: GameModeManager.Instance.SelectedQuizSetName,
            levelSceneName: GameModeManager.Instance.SelectedLevelSceneName,
            questionCount: questionCount,
            gamePort: gamePort,
            playerNamesProvider: GetCurrentPlayerNames
        );

        NetworkManager.Singleton.SceneManager.LoadScene(lobbySceneName, LoadSceneMode.Single);
    }

    IEnumerator StartAsSinglePlayer() {
        GameModeManager.Instance.SetSinglePlayerMode(_selectedQuizSetName, _selectedLevelSceneName);

        statusText.text = "";
        startButton.interactable = false;
        ConfigureTransport("127.0.0.1", gamePort);

        LoadingScreenController.Instance.Show("Loading...");
        yield return new WaitForSeconds(1f);

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
        // Call this when the game ends, not on start:
        // QuizFetcher.Instance.IncrementPlayCount(_selectedQuizSetId);

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
        roomDetailPanel.ShowEmpty();

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
        StartCoroutine(JoinGame());
    }

    IEnumerator JoinGame() {
        if (_selectedHost == null) yield break;

        GameModeManager.Instance.SetClientMode(_selectedHost.Address, _selectedHost.GamePort);
        ConfigureTransport(_selectedHost.Address, _selectedHost.GamePort);

        joinStatusText.text = "";

        // Resolve this client's role from their signed-in Firebase profile.
        // Trusted as-is by the host — no server-side verification (see ConnectionApprovalHandler).
        PlayerRole localRole = AuthManager.Instance != null && AuthManager.Instance.CurrentProfile != null
            ? AuthManager.Instance.CurrentProfile.RoleEnum
            : PlayerRole.Player;

        var payload = new ConnectionPayload {
            version = Application.version,
            playerName = AuthManager.Instance.CurrentProfile?.DisplayName ?? "Player",
            role = (byte)localRole
        };

        string json = JsonUtility.ToJson(payload);
        NetworkManager.Singleton.NetworkConfig.ConnectionData = System.Text.Encoding.UTF8.GetBytes(json);

        LoadingScreenController.Instance.Show($"Joining \"{_selectedHost.HostName}\" Lobby...");
        yield return new WaitForSeconds(1f);

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientJoined;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientJoinFailed;
        NetworkManager.Singleton.StartClient();
    }

    // Must match the private ConnectionPayload class shape expected by
    // ConnectionApprovalHandler on the host side.
    [System.Serializable]
    private class ConnectionPayload {
        public string version;
        public string playerName;
        public byte role;
    }

    void OnClientJoined(ulong clientId) {
        if (clientId != NetworkManager.Singleton.LocalClientId) return;

        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientJoined;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientJoinFailed;

        LoadingScreenController.Instance.SetMessage("Entering lobby...");
        //joinStatusText.text = "Connected!";
        // Server controls scene sync automatically once connected.
    }

    void OnClientJoinFailed(ulong clientId) {
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientJoined;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientJoinFailed;

        // Read the disconnect reason sent by the server's approval callback
        string reason = NetworkManager.Singleton.DisconnectReason;

        //joinStatusText.text = reason switch {
        //    ConnectionApprovalHandler.ReasonFull => "Cannot join — lobby is full (4/4).",
        //    ConnectionApprovalHandler.ReasonInProgress => "Cannot join — game is already in progress.",
        //    ConnectionApprovalHandler.ReasonCountdown => "Cannot join — game is about to start.",
        //    _ => "Failed to connect."
        //};

        LoadingScreenController.Instance.SetMessage(reason switch {
            ConnectionApprovalHandler.ReasonFull => "Cannot join - lobby is full.",
            ConnectionApprovalHandler.ReasonInProgress => "Cannot join - game is already in progress.",
            ConnectionApprovalHandler.ReasonCountdown => "Cannot join - game is about to start.",
            ConnectionApprovalHandler.ReasonVersionMismatch => "Cannot join - version mismatch.",
            ConnectionApprovalHandler.ReasonDuplicateName => "Cannot join - duplicate player name.",
            _ => "Failed to connect."
        }, LoadingScreenController.MessageColor.Error);

        LoadingScreenController.Instance.Hide(3f);
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
        ChatManager.Instance.SendSystemMessage(
            "Welcome ALPHA Tester(s). Game breaking bugs are to be expected, embrace yourselves :)");
    }
}