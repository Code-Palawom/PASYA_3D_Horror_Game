using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.VisualScripting;
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

    // ── Connection Mode Toggle (LAN / Online) ──────────────────
    [Header("Connection Mode Toggle")]
    [Tooltip("Two buttons in the Multiplayer tab row that switch between LAN and Online mode.")]
    [SerializeField] Button hostModeButton;

    // ── Online — Private Join (join code input) ────────────────
    // Wire these into the existing joinPanel in the Inspector.
    // Online sessions are discovered into the same hostListContainer as LAN.
    [Header("Online — Join by Code")]
    [SerializeField] TMP_InputField onlineJoinCodeInput;

    // ── Online Host Options ────────────────────────────────────
    [Header("Online Host Options")]
    [Tooltip("Panel inside the multiplayer tab row, visible only when Online mode is active.")]
    [SerializeField] Toggle publicSessionToggle;

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
    // Unified session list — holds both LAN and online items for the merged join panel
    private readonly List<LobbyListItemUI> _allSessionItems = new();
    private readonly List<DiscoveredHost> _discoveredHosts = new();
    private readonly List<OnlineDiscoveredSession> _discoveredOnlineSessions = new();
    private OnlineDiscoveredSession _selectedOnlineSession;
    private CancellationTokenSource _joinCodeDebounce;
    private ConnectionMode _activeConnectionMode = ConnectionMode.LAN;
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
        TipsManager.Instance.FetchTipsOnce();
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
        multiplayerJoinButton.onClick.AddListener(OnMultiplayerJoinClicked);
        multiplayerBackButton.onClick.AddListener(ShowMainPanel);

        // Level select
        levelNextButton.onClick.AddListener(ShowQuizSelectPanel);
        levelBackButton.onClick.AddListener(OnLevelBackClicked);

        // Quiz select
        quizBackButton.onClick.AddListener(ShowLevelSelectPanel);
        startButton.onClick.AddListener(OnStartClicked);

        // Category dropdown
        categoryDropdown.onValueChanged.AddListener(OnCategoryFilterChanged);

        // Join panel (LAN)
        refreshButton.onClick.AddListener(OnRefreshHostsClicked);
        joinButton.onClick.AddListener(OnJoinClicked);

        // Connection mode toggle (LAN / Online)
        hostModeButton.onClick.AddListener(() => SetConnectionModeTab());

        // Online join panel — private code entry
        onlineJoinCodeInput.onValueChanged.AddListener(OnJoinCodeChanged);

        // Online join panel — public session discovery
        // Placeholder panels
        characterBackButton.onClick.AddListener(ShowMainPanel);
        settingsBackButton.onClick.AddListener(ShowMainPanel);
        aboutBackButton.onClick.AddListener(ShowMainPanel);

        // ── Firebase quiz sets ────────────────────────────────
        SetStatus("Checking for updates...", Color.gray);

        // Populate local SO sets immediately (no Firebase needed)
        List<QuizSetMetaEntry> localMeta = QuizRepository.Instance.GetLocalSetMeta();
        InitDropdown(localMeta);
        foreach (var entry in localMeta)
            SpawnQuizCard(entry);

        if (FirebaseManager.Instance.IsReady) OnFirebaseReady();

        // Wait for Firebase — Firestore cache + listener start after Init() completes
        FirebaseManager.Instance.OnFirebaseReady += OnFirebaseReady;

        AuthManager.Instance.OnAuthStateChanged += (user) => {
            if (user == null) {
                ActionbarToastNotification.Instance.ShowLocalToast("Logged out.", ToastType.Info);
            }
        };

        TipsManager.Instance.LoadCacheImmediately();
        ShowMainPanel();
    }

    // ─────────────────────────────────────────────────────────
    // CATEGORY DROPDOWN
    // ─────────────────────────────────────────────────────────

    // Builds dropdown from known categories. Always "All" at index 0.
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

    // Adds a category to the dropdown if not already present.
    // Called when a new set arrives via OnSetReady.
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

    // Fires per verified set as each background download completes.
    // Adds card live; hides it immediately if it doesn't match the active filter.
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

    void ShowMultiplayerPanel() {
        // Reset connection mode to LAN on each fresh entry into the multiplayer flow
        _activeConnectionMode = ConnectionMode.LAN;
        if (publicSessionToggle != null) publicSessionToggle.SetIsOnWithoutNotify(false);
        EnterWizard(GameMode.Host);
    }

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
    // Unified join panel — shows both LAN and online sessions in the same list.
    void ShowJoinPanel() {
        HideAllContentPanels();
        SetMultiplayerTabRowVisible(true);
        multiplayerJoinButton.image.color = Color.orange;
        hostButton.image.color = Color.white;
        joinPanel.SetActive(true);
        roomDetailPanel.ShowEmpty();
        joinButton.interactable = false;
        if (onlineJoinCodeInput != null) onlineJoinCodeInput.text = "";
        if (joinStatusText != null) joinStatusText.text = "";
        OnRefreshHostsClicked();
    }

    // Always shows the unified join panel regardless of connection mode.
    void OnMultiplayerJoinClicked() => ShowJoinPanel();

    // Switches between LAN and Online modes — only affects host-side options.
    // Join always shows the same unified panel.
    void SetConnectionModeTab() {
        if(_activeConnectionMode == ConnectionMode.LAN) {
            _activeConnectionMode = ConnectionMode.Relay;
            hostModeButton.GetComponentInChildren<TMP_Text>().text = "Switch to LAN";
            publicSessionToggle.gameObject.SetActive(true);
        } else {
            _activeConnectionMode = ConnectionMode.LAN;
            hostModeButton.GetComponentInChildren<TMP_Text>().text = "Switch to NET";
            publicSessionToggle.gameObject.SetActive(false);
        }
    }

    // ─────────────────────────────────────────────────────────
    void EnterWizard(GameMode mode) {
        _pendingMode = mode;
        _selectedLevelSceneName = null;
        _selectedQuizSetName = null;
        _selectedQuizSetId = null;
        ShowLevelSelectPanel();

        hostButton.image.color = Color.orange;
        multiplayerJoinButton.image.color = Color.white;
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
        ConnectionApprovalHandler.Instance.Register();

        if (_pendingMode == GameMode.Host)
            StartAsHost();
        else if (_pendingMode == GameMode.SinglePlayer)
            StartCoroutine(StartAsSinglePlayer());
    }

    async void StartAsHost() {
        GameModeManager.Instance.SetHostMode(_selectedQuizSetName, _selectedLevelSceneName);
        GameModeManager.Instance.SetConnectionMode(_activeConnectionMode);

        bool isPublic = _activeConnectionMode == ConnectionMode.Relay && publicSessionToggle != null && publicSessionToggle.isOn;
        GameModeManager.Instance.SetIsPublicSession(isPublic);

        statusText.text = "";
        startButton.interactable = false;

        if (_activeConnectionMode == ConnectionMode.Relay) {
            LoadingScreenController.Instance.Show("Creating online game...", 1f, 0f, 0.3f);
            try {
                string joinCode = await RelayManager.Instance.CreateRelayAsync(maxPlayers: ConnectionApprovalHandler.MaxPlayers);
                GameModeManager.Instance.SetRelayJoinCode(joinCode);

                int questionCount = QuizRepository.Instance.GetSetByName(_selectedQuizSetName).questions.Count;

                await LobbyManager.Instance.CreateLobbyAsync(
                    hostName: AuthManager.Instance.CurrentProfile.DisplayName ?? SettingsManager.Instance.PlayerName,
                    quizSetName: _selectedQuizSetName,
                    levelSceneName: _selectedLevelSceneName,
                    questionCount: questionCount,
                    relayJoinCode: joinCode,
                    maxPlayers: ConnectionApprovalHandler.MaxPlayers,
                    isPublic
                );

                LoadingScreenController.Instance.SetMessage("Starting host...");
                LoadingScreenController.Instance.SetProgress(1f, 0.3f, 0.9f);
            } catch (Exception e) {
                Debug.LogError($"[Host] {e.Message}");
                LoadingScreenController.Instance.SetMessage("Failed to create online game.", LoadingScreenController.MessageColor.Error);
                LoadingScreenController.Instance.Hide(3f);
                startButton.interactable = true;
                return;
            }
        } else {
            // ── LAN path: direct transport on all interfaces ──
            ConfigureTransport("0.0.0.0", gamePort);
            LoadingScreenController.Instance.Show("Starting host...");
        }

        await Task.Delay(1000);

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

        // LAN only: broadcast via UDP so clients can discover this host.
        // Skipped in Relay mode — clients connect via join code instead.
        if (!GameModeManager.Instance.IsRelayMode) {
            LanDiscovery.Instance.StartHostBroadcast(
                hostName: AuthManager.Instance.CurrentUser?.DisplayName ?? SystemInfo.deviceName,
                quizName: GameModeManager.Instance.SelectedQuizSetName,
                levelSceneName: GameModeManager.Instance.SelectedLevelSceneName,
                questionCount: questionCount,
                gamePort: gamePort,
                playerNamesProvider: GetCurrentPlayerNames
            );
        }

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
    // JOIN — unified LAN + Online discovery
    // ─────────────────────────────────────────────────────────
    void OnRefreshHostsClicked() {
        foreach (var item in _allSessionItems) Destroy(item.gameObject);
        _allSessionItems.Clear();
        _discoveredHosts.Clear();
        _discoveredOnlineSessions.Clear();

        _selectedHost = null;
        _selectedOnlineSession = null;
        joinButton.interactable = false;
        joinStatusText.text = "Searching...";
        roomDetailPanel.ShowEmpty();

        // Both run concurrently — LAN via UDP broadcast, online via Lobby query
        LanDiscovery.Instance.StartClientDiscovery(OnHostDiscovered, duration: 4f);
        _ = QueryOnlineSessionsAsync();
    }

    void OnHostDiscovered(DiscoveredHost host) {
        string levelDisplayName = ResolveLevelDisplayName(host.LevelSceneName);

        var item = Instantiate(hostListItemPrefab, hostListContainer);
        item.Setup(host, levelDisplayName, (h) => OnHostSelected(h, item));
        _allSessionItems.Add(item);
        _discoveredHosts.Add(host);

        UpdateSessionCountText();
    }

    void OnHostSelected(DiscoveredHost host, LobbyListItemUI selectedItem) {
        _selectedHost = host;
        _selectedOnlineSession = null;
        joinButton.interactable = true;

        foreach (var item in _allSessionItems) item.SetSelected(false);
        selectedItem.SetSelected(true);

        var levelOption = availableLevels.FirstOrDefault(l => l.sceneName == host.LevelSceneName);
        roomDetailPanel.Show(
            host,
            levelOption?.displayName ?? host.LevelSceneName,
            levelOption?.previewImage
        );
    }

    async Task QueryOnlineSessionsAsync() {
        try {
            var sessions = await LobbyManager.Instance.QueryPublicSessionsAsync();
            foreach (var session in sessions) {
                var item = Instantiate(hostListItemPrefab, hostListContainer);
                item.SetupOnline(session, ResolveLevelDisplayName(session.LevelSceneName),
                    (s) => OnOnlineSessionSelected(s, item));
                _allSessionItems.Add(item);
                _discoveredOnlineSessions.Add(session);
            }
        } catch (Exception e) {
            Debug.LogWarning($"[Online Discovery] {e.Message}");
        }
        UpdateSessionCountText();
    }

    void OnOnlineSessionSelected(OnlineDiscoveredSession session, LobbyListItemUI selectedItem) {
        _selectedOnlineSession = session;
        _selectedHost = null;
        joinButton.interactable = true;
        roomDetailPanel.ShowEmpty();

        foreach (var item in _allSessionItems) item.SetSelected(false);
        selectedItem.SetSelected(true);

        var levelOption = availableLevels.FirstOrDefault(l => l.sceneName == session.LevelSceneName);
        roomDetailPanel.ShowOnline(
            session,
            levelOption?.displayName ?? session.LevelSceneName,
            levelOption?.previewImage
        );
    }

    void UpdateSessionCountText() {
        int count = _allSessionItems.Count;
        joinStatusText.text = count == 0 ? "No lobbies found." : $"{count} lobb{(count > 1 ? "ies" : "y")} found.";
    }

    string ResolveLevelDisplayName(string sceneName) =>
        availableLevels.FirstOrDefault(l => l.sceneName == sceneName)?.displayName ?? sceneName;

    void OnJoinClicked() {
        // Join code takes priority — lets the player override a selected LAN
        // session by typing a relay code without deselecting the list item.
        string code = onlineJoinCodeInput != null ? onlineJoinCodeInput.text.Trim().ToUpper() : "";
        if (_selectedHost != null)
            JoinGame();
        else if(_selectedOnlineSession != null)
            JoinOnlineSession(_selectedOnlineSession.RelayJoinCode);
        else if(code.Length >= 6)
            JoinOnlineSession(code);
    }

    async void JoinGame() {
        if (_selectedHost == null) return;

        GameModeManager.Instance.SetClientMode(_selectedHost.Address, _selectedHost.GamePort);
        GameModeManager.Instance.SetConnectionMode(ConnectionMode.LAN);
        ConfigureTransport(_selectedHost.Address, _selectedHost.GamePort);

        joinStatusText.text = "";
        SetConnectionPayload();

        LoadingScreenController.Instance.Show($"Joining \"{_selectedHost.HostName}\" Lobby...");
        await Task.Delay(1000);

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientJoined;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientJoinFailed;
        NetworkManager.Singleton.StartClient();
    }

    // ── Online Host Options ───────────────────────────────────

    // Toggle value is read directly in StartAsHost() — no extra callback needed.

    // ── Online Join — Private (join code) ─────────────────────

    // Debounced handler — fires 600 ms after the user stops typing.
    // Looks up the relay code in Unity Lobby and previews the session in
    // roomDetailPanel, mirroring what OnHostSelected does for LAN sessions.
    // Private relay sessions (no lobby entry) show a "private session" note instead.
    async void OnJoinCodeChanged(string raw) {
        _selectedHost = null;
        _selectedOnlineSession = null;
        joinButton.interactable = false;

        foreach (var item in _allSessionItems) item.SetSelected(false);

        _joinCodeDebounce?.Cancel();
        _joinCodeDebounce?.Dispose();
        _joinCodeDebounce = new CancellationTokenSource();
        var token = _joinCodeDebounce.Token;

        string code = raw.Trim().ToUpper();

        if (code.Length < 6) {
            roomDetailPanel.ShowEmpty();
            if (joinStatusText != null) joinStatusText.text = "";
            // Only disable if no LAN host is selected either
            if (_selectedHost == null)
                joinButton.interactable = false;
            return;
        }

        try { await Task.Delay(600, token); } catch (OperationCanceledException) { return; }

        if (joinStatusText != null) joinStatusText.text = "Looking up...";

        OnlineDiscoveredSession session = null;
        try { session = await LobbyManager.Instance.FindSessionByCodeAsync(code); } catch { /* network error — treat as not found */ }

        if (token.IsCancellationRequested) return;

        if (session != null) {
            // Public session — show details exactly like OnHostSelected does for LAN
            var levelOption = availableLevels.FirstOrDefault(l => l.sceneName == session.LevelSceneName);

            // Bridge to DiscoveredHost so roomDetailPanel needs no changes
            roomDetailPanel.Show(
                new DiscoveredHost {
                    HostName = session.HostName,
                    QuizSetName = session.QuizSetName,
                    LevelSceneName = session.LevelSceneName,
                    QuestionCount = session.QuestionCount,
                    PlayerCount = session.PlayerCount,
                    PlayerNames = new(),
                    Address = "0.0.0.0",
                    GamePort = 0,
                    PingMs = -1   // not applicable for relay
                },
                levelOption.displayName ?? session.LevelSceneName,
                levelOption.previewImage
            );
            joinStatusText.text = $"Private lobby by <b>{session.HostName}</b> found.";
            joinButton.interactable = true;
        } else {
            // No public lobby found — private relay session or invalid code
            roomDetailPanel.ShowEmpty();
            joinStatusText.text = "No private lobby found.";
        }
    }

    // ── Shared relay connect logic ────────────────────────────

    async void JoinOnlineSession(string relayJoinCode) {
        joinButton.interactable = false;
        if (joinStatusText != null) joinStatusText.text = "Connecting...";
        LoadingScreenController.Instance.Show("Joining online game...");

        try {
            await RelayManager.Instance.JoinRelayAsync(relayJoinCode);
        } catch (Exception e) {
            if (joinStatusText != null) joinStatusText.text = "Connection failed. Try again.";
            Debug.LogError($"[Online Join] {e.Message}");
            joinButton.interactable = true;
            LoadingScreenController.Instance.Hide(3f);
            return;
        }

        GameModeManager.Instance.SetClientMode("0.0.0.0", 0);
        GameModeManager.Instance.SetConnectionMode(ConnectionMode.Relay);
        SetConnectionPayload();

        await Task.Delay(1000);

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientJoined;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientJoinFailed;
        NetworkManager.Singleton.StartClient();
    }

    // Shared by all join paths (LAN + Online). Reads from Firebase profile.
    // Trusted as-is by the host — no server-side verification (see ConnectionApprovalHandler).
    void SetConnectionPayload() {
        PlayerRole localRole = AuthManager.Instance.CurrentProfile != null ? AuthManager.Instance.CurrentProfile.RoleEnum : PlayerRole.Player;

        var payload = new ConnectionPayload {
            version = Application.version,
            playerName = AuthManager.Instance.CurrentProfile.DisplayName ?? "Player",
            role = (byte)localRole
        };

        NetworkManager.Singleton.NetworkConfig.ConnectionData =
            System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));
    }

    // Must match the ConnectionPayload class shape expected by ConnectionApprovalHandler.
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

        joinButton.interactable = _selectedHost != null
            || (onlineJoinCodeInput != null && onlineJoinCodeInput.text.Trim().Length >= 6);

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