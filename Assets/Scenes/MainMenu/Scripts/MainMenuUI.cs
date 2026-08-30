using Firebase.Firestore;
using PrimeTween;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using Unity.Cinemachine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum PanelSlideDirection { Left, Right, Up, Down }

// NOTE: These are now plain data classes, populated only from code
// (see BuildPanelAnimConfigs() / BuildContentAnimConfigs() below).
// They are intentionally no longer [System.Serializable] / Inspector-facing.
public class PanelAnimConfig {
    public GameObject panel;
    public bool enableSlide = true;
    public bool enableFade = true;
    public PanelSlideDirection enterFrom = PanelSlideDirection.Right;
    public PanelSlideDirection exitTo = PanelSlideDirection.Left;

    // Delay before the show animation starts (slide + fade). Not applied on hide.
    public float showDelay = 0f;

    // Final resolved values (no more overrideMotion flag — just set what you want per panel).
    public float slideDuration = 0.3f;
    public Ease slideEase = Ease.OutQuad;
    public float fadeDuration = 0.2f;
    public Ease fadeEase = Ease.OutQuad;
}

public class PanelContentItemConfig {
    public RectTransform item;
    public PanelSlideDirection moveFrom = PanelSlideDirection.Down;
    public float moveDistance = 30f;
    public float duration = 0.3f;
    public Ease ease = Ease.OutBack;
}

public class PanelContentAnimConfig {
    // The panel these items belong to — must match one of the panels registered
    // in BuildPanelAnimConfigs() (or mainPanel, etc).
    public GameObject panel;
    // Items to animate in, in stagger order.
    public List<PanelContentItemConfig> items = new();
    // Delay before the FIRST item starts animating, e.g. to let the panel's own slide-in finish first.
    public float initialDelay = 0f;
    // Seconds between each item's animation start.
    public float staggerDelay = 0.15f;
}

// Lets a specific "coming FROM this panel, going TO this panel" transition
// override the panels' own default enter/exit direction (from PanelAnimConfig).
// Either from or to can be null, meaning "any panel" — e.g. (null, settingsPanel)
// matches every transition landing on settingsPanel regardless of where it
// came from. A more specific (exact from+to) match always wins over a
// wildcard one. Leaving exitDirection/enterDirection null on a match means
// "no override for that side" — the panel's own default still applies.
public class PanelTransitionConfig {
    public GameObject from;
    public GameObject to;
    public PanelSlideDirection? exitDirection;
    public PanelSlideDirection? enterDirection;
    public bool? enableSlide;
}

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
    [SerializeField] GameObject pasyaTitle;
    [SerializeField] Button singlePlayerButton;
    [SerializeField] Button multiplayerButton;
    [SerializeField] Button characterButton;
    [SerializeField] Button settingsButton;
    [SerializeField] Button aboutButton;
    [SerializeField] Button exitButton;
    [SerializeField] GameObject profileDisplay;
    [SerializeField] TMP_Text versionDisplay;
    [SerializeField] TMP_Text versionCheckDisplay;

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

    // ── Subject Select Panel (new wizard step, between Level and Quiz) ────
    [Header("Subject Select Panel")]
    [SerializeField] GameObject subjectSelectPanel;
    [SerializeField] Transform subjectListContainer;
    [SerializeField] SubjectSelectItemUI subjectItemPrefab;
    [SerializeField] Button subjectNextButton;
    [SerializeField] Button subjectBackButton;

    // ── Quiz Select Panel (shared by Host + Single Player) ─
    [Header("Quiz Select Panel")]
    [SerializeField] GameObject quizSelectPanel;
    [SerializeField] Transform quizListContainer;
    [SerializeField] QuizSetItemUI quizItemPrefab;
    [SerializeField] Button startButton;
    [SerializeField] TMP_Text startButtonLabel;
    [SerializeField] Button quizBackButton;

    // ── Filters
    [Header("Subject Select Panel")]
    [SerializeField] TMP_InputField subjectFilterInput;

    [Header("Quiz Select Panel")]
    [SerializeField] TMP_InputField quizFilterInput;

    // ── Quiz Select Status Bar ─────────────────────────────────
    [Header("Quiz Status Bar")]
    [SerializeField] TMP_Text statusText;
    [SerializeField] Color statusColorOk = new Color(0.2f, 0.8f, 0.2f);
    [SerializeField] Color statusColorErr = new Color(0.9f, 0.2f, 0.2f);

    // ── Placeholder Panels ──────────────────────────────────
    [Header("Character Panel")]
    [SerializeField] GameObject characterPanel;
    [SerializeField] CharacterAppearanceController characterAppearance;
    [SerializeField] private SkinDatabaseSO database;
    [SerializeField] Button characterBackButton;
    [SerializeField] ResetCharacterRotation characterRotator;
    [SerializeField] MMCharacterRandomAnimation characterAnimation;

    [Header("Settings Panel")]
    [SerializeField] GameObject settingsPanel;
    [SerializeField] Button settingsBackButton;

    [Header("About Panel")]
    [SerializeField] GameObject aboutPanel;
    [SerializeField] Button aboutBackButton;

    [SerializeField] Button achievementsButton;
    [SerializeField] GameObject achievementsPanel;
    [SerializeField] Button achievementsBackButton;

    [SerializeField] Button completedQuizButton;
    [SerializeField] GameObject completedQuizPanel;
    [SerializeField] Button completedQuizBackButton;

    [SerializeField] GameObject TutorialPanel;

    [Header("Cameras")]
    [SerializeField] private CinemachineCamera cam;
    [SerializeField] private CinemachineCamera characterCam;

    // ── Panel Slide Animation (global fallback defaults) ───────────────────
    // These are still Inspector-tunable as "house defaults"; per-panel
    // configs below can either use them or specify their own values outright.
    [Header("Panel Slide Animation — Global Defaults")]
    [SerializeField] private float panelSlideDuration = 0.25f;
    [SerializeField] private Ease panelSlideEase = Ease.OutQuad;
    [Tooltip("How far off-screen (in pixels) panels slide to/from.")]
    [SerializeField] private float panelSlideDistance = 900f;
    [SerializeField] private float panelFadeDuration = 0.2f;
    [SerializeField] private Ease panelFadeEase = Ease.OutQuad;

    // Panel + content animation configs are now built entirely in code —
    // see BuildPanelAnimConfigs() / BuildContentAnimConfigs() /
    // BuildPanelTransitionConfigs() near the bottom of this section.
    // Nothing here is Inspector-editable anymore.
    private List<PanelAnimConfig> panelAnimConfigs;
    private List<PanelContentAnimConfig> panelContentAnimConfigs;
    private List<PanelTransitionConfig> panelTransitionConfigs;

    private class PanelRuntime {
        public RectTransform rect;
        public CanvasGroup canvasGroup;
        public Vector2 restPos;
        public bool enableSlide;
        public bool enableFade;
        public PanelSlideDirection enterFrom;
        public PanelSlideDirection exitTo;
        public float slideDuration;
        public Ease slideEase;
        public float fadeDuration;
        public Ease fadeEase;
        public float showDelay;
        public Tween tween;
        public Tween fadeTween;
    }

    private class ContentItemRuntime {
        public RectTransform rect;
        public CanvasGroup group;
        public Vector2 restPos;
        public PanelSlideDirection moveFrom;
        public float moveDistance;
        public float duration;
        public Ease ease;
        public Tween moveTween;
        public Tween fadeTween;
    }

    private class ContentGroupRuntime {
        public float initialDelay;
        public float staggerDelay;
        public List<ContentItemRuntime> items = new();
    }

    private readonly Dictionary<GameObject, PanelRuntime> _panelRuntimes = new();
    private readonly Dictionary<GameObject, ContentGroupRuntime> _contentRuntimes = new();
    private GameObject _currentPanel;

    // ─────────────────────────────────────────────────────────
    private GameMode _pendingMode = GameMode.None;
    private string _selectedLevelSceneName;
    private string _selectedSubject;
    private string _selectedQuizSetName;
    private string _selectedQuizSetId;
    private DiscoveredHost _selectedHost;

    private readonly List<LevelSelectItemUI> _levelItems = new();
    private readonly List<SubjectSelectItemUI> _subjectItems = new();
    private readonly List<QuizSetMetaEntry> _allQuizMeta = new(); // full meta, for subject grouping/gating
    private readonly List<QuizSetItemUI> _quizItems = new();
    private string _subjectNameFilter = "";
    private string _quizNameFilter = "";

    // Unified session list — holds both LAN and online items for the merged join panel
    private readonly List<LobbyListItemUI> _allSessionItems = new();
    private readonly List<DiscoveredHost> _discoveredHosts = new();
    private readonly List<OnlineDiscoveredSession> _discoveredOnlineSessions = new();
    private OnlineDiscoveredSession _selectedOnlineSession;
    private CancellationTokenSource _joinCodeDebounce;
    private ConnectionMode _activeConnectionMode = ConnectionMode.LAN;
    private readonly HashSet<string> _renderedQuizSetIds = new();

    private bool isFirstLaunch = true;

    // ─────────────────────────────────────────────────────────
    // CODE-ONLY ANIMATION CONFIG
    // ─────────────────────────────────────────────────────────
    // Everything below replaces what used to be the Inspector lists
    // "Panel Anim Configs" / "Panel Content Anim Configs". Edit these two
    // methods directly to change what animates and how — no Inspector
    // wiring needed anymore.
    private List<PanelAnimConfig> BuildPanelAnimConfigs() {
        var configs = new List<PanelAnimConfig>();

        void Add(GameObject panel, PanelSlideDirection enterFrom, PanelSlideDirection exitTo, bool enableSlide = true, bool enableFade = true, float showDelay = 0f, float? slideDuration = null, Ease? slideEase = null, float? fadeDuration = null, Ease? fadeEase = null) {
            if (panel == null) return;
            configs.Add(new PanelAnimConfig {
                panel = panel,
                enableSlide = enableSlide,
                enableFade = enableFade,
                enterFrom = enterFrom,
                exitTo = exitTo,
                showDelay = showDelay,
                slideDuration = slideDuration ?? panelSlideDuration,
                slideEase = slideEase ?? panelSlideEase,
                fadeDuration = fadeDuration ?? panelFadeDuration,
                fadeEase = fadeEase ?? panelFadeEase
            });
        }

        Add(mainPanel, PanelSlideDirection.Left, PanelSlideDirection.Left);
        Add(joinPanel, PanelSlideDirection.Down, PanelSlideDirection.Down);
        Add(levelSelectPanel, PanelSlideDirection.Down, PanelSlideDirection.Down);
        Add(subjectSelectPanel, PanelSlideDirection.Up, PanelSlideDirection.Up);
        Add(quizSelectPanel, PanelSlideDirection.Up, PanelSlideDirection.Up);
        Add(characterPanel, PanelSlideDirection.Right, PanelSlideDirection.Right);
        Add(aboutPanel, PanelSlideDirection.Down, PanelSlideDirection.Down);
        Add(achievementsPanel, PanelSlideDirection.Right, PanelSlideDirection.Right, false);
        Add(completedQuizPanel, PanelSlideDirection.Right, PanelSlideDirection.Right, false);
        Add(settingsPanel, PanelSlideDirection.Right, PanelSlideDirection.Right);

        return configs;
    }

    private List<PanelContentAnimConfig> BuildContentAnimConfigs() {
        var configs = new List<PanelContentAnimConfig>();

        PanelContentItemConfig Item(RectTransform rect, PanelSlideDirection moveFrom = PanelSlideDirection.Left, float moveDistance = 130f, float duration = 0.3f, Ease ease = Ease.OutBack) {
            return new PanelContentItemConfig {
                item = rect,
                moveFrom = moveFrom,
                moveDistance = moveDistance,
                duration = duration,
                ease = ease
            };
        }

        // Main panel content, staggered in on first show — the real 10 items,
        // in on-screen order.
        var mainPanelItems = new List<PanelContentItemConfig> {
            Item(pasyaTitle.GetComponent<RectTransform>()),
            Item(singlePlayerButton.GetComponent<RectTransform>()),
            Item(multiplayerButton.GetComponent<RectTransform>()),
            Item(characterButton.GetComponent<RectTransform>()),
            Item(settingsButton.GetComponent<RectTransform>()),
            Item(aboutButton.GetComponent<RectTransform>()),
            Item(exitButton.GetComponent<RectTransform>()),
            Item(profileDisplay.GetComponent<RectTransform>(), PanelSlideDirection.Right),
            Item(versionDisplay.GetComponent<RectTransform>(), PanelSlideDirection.Right),
            Item(versionCheckDisplay.GetComponent<RectTransform>()),
        };

        configs.Add(new PanelContentAnimConfig {
            panel = mainPanel,
            items = mainPanelItems,
            initialDelay = 0f,
            staggerDelay = 0.15f
        });

        return configs;
    }

    // Per-(from,to) transition overrides. Use this when a panel needs to
    // animate differently depending on which panel you're coming FROM —
    // e.g. MainMenu -> Level Select feels like a forward wizard step, while
    // MainMenu -> Settings feels more like opening a modal, even though both
    // start at mainPanel. Without a match here, each panel just uses its own
    // default enterFrom/exitTo from BuildPanelAnimConfigs().
    private List<PanelTransitionConfig> BuildPanelTransitionConfigs() {
        var transitions = new List<PanelTransitionConfig>();

        void AddTransition(GameObject from, GameObject to, PanelSlideDirection? exitDirection = null, PanelSlideDirection? enterDirection = null, bool? enableSlide = null) {
            transitions.Add(new PanelTransitionConfig {
                from = from,
                to = to,
                exitDirection = exitDirection,
                enterDirection = enterDirection,
                enableSlide = enableSlide
            });
        }

        AddTransition(mainPanel, levelSelectPanel,
            exitDirection: PanelSlideDirection.Left, enterDirection: PanelSlideDirection.Right);

        AddTransition(levelSelectPanel, mainPanel,
            exitDirection: PanelSlideDirection.Right, enterDirection: PanelSlideDirection.Left);

        AddTransition(settingsPanel, achievementsPanel, enableSlide: false);
        AddTransition(settingsPanel, completedQuizPanel, enableSlide: false);
        AddTransition(achievementsPanel, settingsPanel, enableSlide: false);
        AddTransition(completedQuizPanel, settingsPanel, enableSlide: false);

        return transitions;
    }

    private void Awake() {
        cam.Priority = 0;
        characterCam.Priority = 10;

        panelAnimConfigs = BuildPanelAnimConfigs();
        panelContentAnimConfigs = BuildContentAnimConfigs();
        panelTransitionConfigs = BuildPanelTransitionConfigs();

        foreach (var config in panelAnimConfigs) {
            if (config.panel == null) continue;
            var rect = config.panel.GetComponent<RectTransform>();
            if (rect == null) {
                Debug.LogWarning($"[MainMenuUI] {config.panel.name} has no RectTransform — skipping slide animation for it.");
                continue;
            }

            var canvasGroup = config.panel.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = config.panel.AddComponent<CanvasGroup>();

            _panelRuntimes[config.panel] = new PanelRuntime {
                rect = rect,
                canvasGroup = canvasGroup,
                restPos = rect.anchoredPosition,
                enableSlide = config.enableSlide,
                enableFade = config.enableFade,
                enterFrom = config.enterFrom,
                exitTo = config.exitTo,
                showDelay = config.showDelay,
                slideDuration = config.slideDuration,
                slideEase = config.slideEase,
                fadeDuration = config.fadeDuration,
                fadeEase = config.fadeEase
            };
        }

        foreach (var config in panelContentAnimConfigs) {
            if (config.panel == null || config.items.Count == 0) continue;

            var group = new ContentGroupRuntime {
                initialDelay = config.initialDelay,
                staggerDelay = config.staggerDelay
            };

            foreach (var itemConfig in config.items) {
                if (itemConfig.item == null) continue;

                var canvasGroup = itemConfig.item.GetComponent<CanvasGroup>();
                if (canvasGroup == null) canvasGroup = itemConfig.item.gameObject.AddComponent<CanvasGroup>();

                group.items.Add(new ContentItemRuntime {
                    rect = itemConfig.item,
                    group = canvasGroup,
                    restPos = itemConfig.item.anchoredPosition,
                    moveFrom = itemConfig.moveFrom,
                    moveDistance = itemConfig.moveDistance,
                    duration = itemConfig.duration,
                    ease = itemConfig.ease
                });
            }

            _contentRuntimes[config.panel] = group;
        }
    }

    // ─────────────────────────────────────────────────────────
    void OnEnable() {
        QuizFetcher.Instance.OnSetReady += HandleSetReady;
        QuizFetcher.Instance.OnFetchStatus += HandleFetchStatus;
        QuizFetcher.Instance.OnMetaUpdated += HandleMetaUpdated;
        QuizFetcher.Instance.OnSetRemoved += HandleSetRemoved;
        QuizFetcher.Instance.OnLocalPlayCountSynced += HandleLocalPlayCountSynced;
    }

    void OnDisable() {
        QuizFetcher.Instance.OnSetReady -= HandleSetReady;
        QuizFetcher.Instance.OnFetchStatus -= HandleFetchStatus;
        QuizFetcher.Instance.OnMetaUpdated -= HandleMetaUpdated;
        QuizFetcher.Instance.OnSetRemoved -= HandleSetRemoved;
        QuizFetcher.Instance.OnLocalPlayCountSynced -= HandleLocalPlayCountSynced;

        if (FirebaseManager.Instance != null)
            FirebaseManager.Instance.OnFirebaseReady -= OnFirebaseReady;

        foreach (var runtime in _panelRuntimes.Values)
            runtime.tween.Stop();

        foreach (var content in _contentRuntimes.Values)
            StopContentTweens(content);
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

        foreach (var entry in cached)
            if (entry.hasLocalData)
                SpawnQuizCard(entry);

        // Attach _meta snapshot listener — fires immediately + on every remote change
        QuizFetcher.Instance.StartListening();
        TipsManager.Instance.FetchTipsOnce();
    }

    void Start() {
        if (ActionbarToastNotification.Instance != null) ActionbarToastNotification.Instance.ClearToast();

        // Main panel
        singlePlayerButton.onClick.AddListener(() => EnterWizard(GameMode.SinglePlayer));
        multiplayerButton.onClick.AddListener(ShowMultiplayerPanel);
        characterButton.onClick.AddListener(ShowCharacterPanel);
        settingsButton.onClick.AddListener(ShowSettingsPanel);
        aboutButton.onClick.AddListener(ShowAboutPanel);
        exitButton.onClick.AddListener(OnExitClicked);

        achievementsButton.onClick.AddListener(ShowAchievementsPanel);
        completedQuizButton.onClick.AddListener(ShowCompletedQuizPanel);

        // Multiplayer panel
        hostButton.onClick.AddListener(() => EnterWizard(GameMode.Host));
        multiplayerJoinButton.onClick.AddListener(OnMultiplayerJoinClicked);
        multiplayerBackButton.onClick.AddListener(ShowMainPanel);

        // Level select
        levelNextButton.onClick.AddListener(ShowSubjectSelectPanel);
        levelBackButton.onClick.AddListener(OnLevelBackClicked);

        subjectBackButton.onClick.AddListener(ShowLevelSelectPanel);
        subjectNextButton.onClick.AddListener(ShowQuizSelectPanel);

        // Quiz select
        quizBackButton.onClick.AddListener(ShowSubjectSelectPanel);
        startButton.onClick.AddListener(OnStartClicked);

        // Filters
        subjectFilterInput.onValueChanged.AddListener(OnSubjectFilterChanged);
        quizFilterInput.onValueChanged.AddListener(OnQuizFilterChanged);

        // Join panel (LAN)
        refreshButton.onClick.AddListener(OnRefreshHostsClicked);
        joinButton.onClick.AddListener(OnJoinClicked);

        // Connection mode toggle (LAN / Online)
        hostModeButton.onClick.AddListener(() => SetConnectionModeTab());

        // Online join panel — private code entry
        onlineJoinCodeInput.onValueChanged.AddListener(OnJoinCodeChanged);

        // Online join panel — public session discovery
        // Placeholder panels
        characterBackButton.onClick.AddListener(() => HideCharacterPanel(false));
        settingsBackButton.onClick.AddListener(ShowMainPanel);
        aboutBackButton.onClick.AddListener(ShowMainPanel);
        achievementsBackButton.onClick.AddListener(ShowSettingsPanel);
        completedQuizBackButton.onClick.AddListener(ShowSettingsPanel);


        characterAppearance.ApplySkin(database.GetById(SkinSaveSystem.Load()) ?? (database.skins.Length > 0 ? database.skins[0] : null));

        // ── Firebase quiz sets ────────────────────────────────
        SetStatus("Checking for updates...", Color.gray);

        // Populate local SO sets immediately (no Firebase needed)
        List<QuizSetMetaEntry> localMeta = QuizRepository.Instance.GetLocalSetMeta();
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

        AuthManager.Instance.OnPlayerStatsLoaded += (profile) => {
            if (_currentPanel != null && quizSelectPanel != null && _currentPanel == quizSelectPanel) ApplySubjectFilterAndGating();
        };

        TipsManager.Instance.LoadCacheImmediately();
        StartCoroutine(ShowMainPanelCourotine());
    }

    IEnumerator ShowMainPanelCourotine() {
        yield return new WaitForSeconds(0.1f);
        ShowMainPanel();

        if (SettingsManager.Instance != null && !SettingsManager.Instance.Current.completedTutorial) {
            yield return new WaitForSeconds(1f);

            SetMultiplayerTabRowVisible(false);
            SwitchToPanel(TutorialPanel);
        }
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
        if (_renderedQuizSetIds.Contains(entry.setId) || entry.setId == "Tutorial") return;
        _renderedQuizSetIds.Add(entry.setId);

        _allQuizMeta.Add(entry); // NEW

        var item = Instantiate(quizItemPrefab, quizListContainer);
        item.Setup(entry, OnQuizSelected);

        _quizItems.Add(item);
        RefreshSubjectListIfVisible(); // NEW — keep subject panel live if a card streams in while it's open
    }

    void ShowSubjectSelectPanel() {
        bool isHostFlow = _pendingMode == GameMode.Host;
        subjectNextButton.interactable = false;
        SetMultiplayerTabRowVisible(isHostFlow);

        _subjectNameFilter = "";
        if (subjectFilterInput != null) subjectFilterInput.SetTextWithoutNotify("");

        SwitchToPanel(subjectSelectPanel);
        PopulateSubjectList();
        ApplySubjectNameFilter(); // no-op with empty filter, but keeps state consistent
    }

    void PopulateSubjectList() {
        foreach (var item in _subjectItems) Destroy(item.gameObject);
        _subjectItems.Clear();

        var completed = AuthManager.Instance.CurrentProfile?.CompletedQuizSets ?? new Dictionary<string, Timestamp>();

        var subjects = _allQuizMeta
            .Where(e => !string.IsNullOrWhiteSpace(e.subject))
            .Select(e => e.subject)
            .Distinct()
            .OrderBy(s => s)
            .ToList();

        foreach (var subject in subjects) {
            var setsInSubject = _allQuizMeta.Where(e => e.subject == subject).ToList();
            int completedCount = setsInSubject.Count(e => completed.ContainsKey(e.setId));
            int totalPlayCount = setsInSubject.Sum(e => e.playCount);

            var item = Instantiate(subjectItemPrefab, subjectListContainer);
            item.Setup(subject, completedCount, setsInSubject.Count, totalPlayCount, OnSubjectSelected);
            _subjectItems.Add(item);
        }
    }

    void RefreshSubjectListIfVisible() {
        if (_currentPanel == subjectSelectPanel) {
            PopulateSubjectList();
            ApplySubjectNameFilter();
        }
    }

    void OnSubjectSelected(string subject) {
        _selectedSubject = subject;
        foreach (var item in _subjectItems) item.SetSelected(item.Subject == subject);

        subjectNextButton.interactable = true;
        //ShowQuizSelectPanel();
    }

    // Fires per verified set as each background download completes.
    // Adds card live; hides it immediately if it doesn't match the active filter.
    void HandleSetReady(QuizSetMetaEntry entry) {
        SpawnQuizCard(entry);
    }

    // Meta-only change (name/subject/order/questionCount) on a set we already
    // have fully downloaded — no re-download happened, so this is the only
    // place the card's displayed info gets refreshed.
    void HandleMetaUpdated(QuizSetMetaEntry entry) {
        if (entry.setId == "Tutorial") return;

        int metaIndex = _allQuizMeta.FindIndex(e => e.setId == entry.setId);
        if (metaIndex >= 0) _allQuizMeta[metaIndex] = entry;

        var item = _quizItems.FirstOrDefault(i => i.SetId == entry.setId);
        if (item != null) item.Setup(entry, OnQuizSelected);

        RefreshSubjectListIfVisible();
        if (_currentPanel == quizSelectPanel) ApplySubjectFilterAndGating();
    }

    // Set got deleted or flipped to unverified server-side — drop its card.
    void HandleSetRemoved(string setId) {
        _renderedQuizSetIds.Remove(setId);
        _allQuizMeta.RemoveAll(e => e.setId == setId);

        var item = _quizItems.FirstOrDefault(i => i.SetId == setId);
        if (item != null) {
            _quizItems.Remove(item);
            Destroy(item.gameObject);
        }

        if (_selectedQuizSetId == setId) {
            _selectedQuizSetId = null;
            _selectedQuizSetName = null;
            startButton.interactable = false;
        }

        RefreshSubjectListIfVisible();
        if (_currentPanel == quizSelectPanel) ApplySubjectFilterAndGating();
    }

    void HandleLocalPlayCountSynced(string setId, int count) {
        var entry = _allQuizMeta.FirstOrDefault(e => e.setId == setId);
        if (entry == null) return; // not in the menu's current list yet

        entry.playCount = count;

        var item = _quizItems.FirstOrDefault(i => i.SetId == setId);
        if (item != null) item.Setup(entry, OnQuizSelected);

        RefreshSubjectListIfVisible();
        if (_currentPanel == quizSelectPanel) ApplySubjectFilterAndGating();
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
    // Slides the previous panel out and the target panel in, driven by
    // panelAnimConfigs. Panels not registered there fall back to an
    // instant SetActive. If a panel has an entry in panelContentAnimConfigs,
    // its content staggers in after the panel finishes appearing (or
    // immediately, if the panel itself has slide disabled).
    // Any panel is treated identically here — a panel that needs to react to
    // being shown/hidden (refresh displayed values, flush pending state, etc.)
    // does so by implementing IPanelLifecycle on itself; see NotifyPanelShow /
    // NotifyPanelHide below. No panel gets special-cased in this class.

    private void HideCurrentPanel(PanelSlideDirection? exitOverride = null, bool? enableSlideOverride = null) {
        if (_currentPanel == null) return;

        if (_panelRuntimes.TryGetValue(_currentPanel, out var runtime)) {
            AnimateHidePanel(_currentPanel, runtime, exitOverride, enableSlideOverride);
        } else {
            _currentPanel.SetActive(false);
        }
    }

    private void SwitchToPanel(GameObject targetPanel) {
        if (_currentPanel == targetPanel) return;

        GameObject previousPanel = _currentPanel;
        PanelTransitionConfig transition = FindTransition(previousPanel, targetPanel);

        HideCurrentPanel(transition?.exitDirection, transition?.enableSlide);
        _currentPanel = targetPanel;

        if (_panelRuntimes.TryGetValue(targetPanel, out var inRuntime))
            AnimateShowPanel(targetPanel, inRuntime, transition?.enterDirection, transition?.enableSlide);
        else {
            targetPanel.SetActive(true);
            if (_contentRuntimes.TryGetValue(targetPanel, out var content))
                AnimateContentIn(content);
        }
    }

    // Finds the most specific registered transition for this (from, to) pair.
    // Exact from+to match wins outright; otherwise an "any from -> this to"
    // wildcard is preferred over a "this from -> any to" wildcard. Returns
    // null if nothing matches, meaning both panels just use their own
    // defaults from BuildPanelAnimConfigs().
    private PanelTransitionConfig FindTransition(GameObject from, GameObject to) {
        PanelTransitionConfig anyFromMatch = null;
        PanelTransitionConfig anyToMatch = null;

        foreach (var t in panelTransitionConfigs) {
            bool fromMatches = t.from == null || t.from == from;
            bool toMatches = t.to == null || t.to == to;
            if (!fromMatches || !toMatches) continue;

            if (t.from == from && t.to == to) return t;
            if (t.from == null && t.to == to) anyFromMatch = t;
            else if (t.from == from && t.to == null) anyToMatch = t;
        }

        return anyFromMatch ?? anyToMatch;
    }

    private void AnimateShowPanel(GameObject panel, PanelRuntime r, PanelSlideDirection? enterOverride = null, bool? enableSlideOverride = null) {
        r.tween.Stop();
        r.fadeTween.Stop();

        panel.SetActive(true);

        PanelSlideDirection enterFrom = enterOverride ?? r.enterFrom;
        bool enableSlide = enableSlideOverride ?? r.enableSlide;

        if (!enableSlide) {
            r.rect.anchoredPosition = r.restPos;
        } else {
            r.rect.anchoredPosition = r.restPos + GetSlideOffset(enterFrom, panelSlideDistance);
            r.tween = Tween.UIAnchoredPosition(r.rect, r.restPos, r.slideDuration, r.slideEase, startDelay: r.showDelay);
        }

        if (!r.enableFade) {
            r.canvasGroup.alpha = 1f;
        } else {
            r.canvasGroup.alpha = 0f;
            r.fadeTween = Tween.Alpha(r.canvasGroup, 1f, r.fadeDuration, r.fadeEase, startDelay: r.showDelay);
        }

        if (_contentRuntimes.TryGetValue(panel, out var content))
            AnimateContentIn(content);
    }

    private void AnimateHidePanel(GameObject panel, PanelRuntime r, PanelSlideDirection? exitOverride = null, bool? enableSlideOverride = null) {
        r.tween.Stop();
        r.fadeTween.Stop();

        if (_contentRuntimes.TryGetValue(panel, out var content))
            StopContentTweens(content);

        bool enableSlide = enableSlideOverride ?? r.enableSlide;

        if (!r.enableFade && !enableSlide) {
            panel.SetActive(false);
            return;
        }

        if (r.enableFade) {
            r.fadeTween = Tween.Alpha(r.canvasGroup, 0f, r.fadeDuration, r.fadeEase)
                .OnComplete(() => {
                    panel.SetActive(false);
                    r.canvasGroup.alpha = 1f;
                    r.rect.anchoredPosition = r.restPos;
                });
        } else {
            r.canvasGroup.alpha = 1f;
        }

        if (enableSlide) {
            PanelSlideDirection exitTo = exitOverride ?? r.exitTo;
            var slideTween = Tween.UIAnchoredPosition(r.rect, r.restPos + GetSlideOffset(exitTo, panelSlideDistance), r.slideDuration, r.slideEase);

            if (!r.enableFade) {
                slideTween.OnComplete(() => {
                    panel.SetActive(false);
                    r.rect.anchoredPosition = r.restPos;
                });
            }

            r.tween = slideTween;
        }
    }


    // Staggered content reveal for a panel's inner items (e.g. main panel nav buttons).
    // Each item may have its own direction/distance/duration/ease (see PanelContentItemConfig).
    private void AnimateContentIn(ContentGroupRuntime content) {
        // First pass: snap ALL items to their own "from" state immediately,
        // before any tween starts. Prevents a flash-at-rest-position during
        // startDelay, and avoids a Layout Group resetting anchoredPosition
        // out from under a still-pending delayed tween.
        for (int i = 0; i < content.items.Count; i++) {
            var item = content.items[i];
            item.moveTween.Stop();
            item.fadeTween.Stop();

            Vector2 offset = GetSlideOffset(item.moveFrom, item.moveDistance);
            item.rect.anchoredPosition = item.restPos + offset;
            item.group.alpha = 0f;
        }

        // Second pass: kick off staggered tweens using each item's own
        // resolved motion, with explicit startValue/endValue so PrimeTween
        // never re-samples a "current" value once startDelay elapses.
        for (int i = 0; i < content.items.Count; i++) {
            var item = content.items[i];
            Vector2 offset = GetSlideOffset(item.moveFrom, item.moveDistance);
            Vector2 fromPos = item.restPos + offset;
            float delay = content.initialDelay + i * content.staggerDelay;
            // Main panel (10 items, see BuildContentAnimConfigs) skips the stagger
            // delay after first launch — only the very first reveal staggers in.
            if (content.items.Count == 10 && !isFirstLaunch) delay = 0;

            item.moveTween = Tween.UIAnchoredPosition(item.rect, fromPos, item.restPos, item.duration, item.ease, startDelay: delay);
            item.fadeTween = Tween.Alpha(item.group, 0f, 1f, item.duration, Ease.Linear, startDelay: delay);
        }

        if (isFirstLaunch) isFirstLaunch = false;
    }

    private void StopContentTweens(ContentGroupRuntime content) {
        foreach (var item in content.items) {
            item.moveTween.Stop();
            item.fadeTween.Stop();
        }
    }

    private static Vector2 GetSlideOffset(PanelSlideDirection dir, float distance) {
        return dir switch {
            PanelSlideDirection.Left => Vector2.left * distance,
            PanelSlideDirection.Right => Vector2.right * distance,
            PanelSlideDirection.Up => Vector2.up * distance,
            PanelSlideDirection.Down => Vector2.down * distance,
            _ => Vector2.zero
        };
    }

    void SetMultiplayerTabRowVisible(bool visible) => multiplayerPanel.SetActive(visible);

    void ShowMainPanel() {
        cam.Priority = 10;
        characterCam.Priority = 0;

        _pendingMode = GameMode.None;
        SetMultiplayerTabRowVisible(false);
        SwitchToPanel(mainPanel);
    }

    void ShowMultiplayerPanel() {
        // Reset connection mode to LAN on each fresh entry into the multiplayer flow
        _activeConnectionMode = ConnectionMode.LAN;
        if (publicSessionToggle != null) publicSessionToggle.SetIsOnWithoutNotify(false);
        EnterWizard(GameMode.Host);
    }

    void ShowCharacterPanel() {
        cam.Priority = 0;
        characterCam.Priority = 10;

        SetMultiplayerTabRowVisible(false);
        characterAnimation.SetIsCustomizing(true);
        SwitchToPanel(characterPanel);

        ActionbarToastNotification.Instance.ClearToast();
    }

    public void HideCharacterPanel(bool isSaved) {
        if (!isSaved) characterAppearance.ApplySkin(database.GetById(SkinSaveSystem.Load()) ?? (database.skins.Length > 0 ? database.skins[0] : null));

        cam.Priority = 10;
        characterCam.Priority = 0;

        _pendingMode = GameMode.None;
        SetMultiplayerTabRowVisible(false);
        characterAnimation.SetAnimationState("idle");
        characterRotator.ResetRotation();
        characterAnimation.SetIsCustomizing(false);
        SwitchToPanel(mainPanel);
    }

    void ShowSettingsPanel() {
        SetMultiplayerTabRowVisible(false);
        SwitchToPanel(settingsPanel);

        ActionbarToastNotification.Instance.ClearToast();
    }

    void ShowAboutPanel() {
        SetMultiplayerTabRowVisible(false);
        SwitchToPanel(aboutPanel);
        ActionbarToastNotification.Instance.ClearToast();
    }

    void ShowAchievementsPanel() {
        SetMultiplayerTabRowVisible(false);
        SwitchToPanel(achievementsPanel);
        ActionbarToastNotification.Instance.ClearToast();
    }

    void ShowCompletedQuizPanel() {
        SetMultiplayerTabRowVisible(false);
        SwitchToPanel(completedQuizPanel);
        ActionbarToastNotification.Instance.ClearToast();
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
        SetMultiplayerTabRowVisible(true);
        multiplayerJoinButton.image.color = Color.orange;
        hostButton.image.color = Color.white;
        SwitchToPanel(joinPanel);
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
        if (_activeConnectionMode == ConnectionMode.LAN) {
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

        if (mode == GameMode.SinglePlayer) {
            hostModeButton.gameObject.SetActive(false);
        } else {
            hostModeButton.gameObject.SetActive(true);
        }

        hostButton.image.color = Color.orange;
        multiplayerJoinButton.image.color = Color.white;

        ActionbarToastNotification.Instance.ClearToast();
    }

    void ShowLevelSelectPanel() {
        bool isHostFlow = _pendingMode == GameMode.Host;

        // Tab row stays visible only when this is the Host flow —
        // Single Player never shows Host/Join tabs at all.
        SetMultiplayerTabRowVisible(isHostFlow);

        // When the tab row's own Back button is already visible (Host flow),
        // this panel's own Back button would be a redundant duplicate —
        // hide it. Single Player has no tab row, so it needs its own.
        levelBackButton.gameObject.SetActive(!isHostFlow);

        SwitchToPanel(levelSelectPanel);

        PopulateLevelList();
        levelNextButton.interactable = !string.IsNullOrEmpty(_selectedLevelSceneName);
    }

    // Only reachable from Single Player now (the button is hidden
    // during the Host flow), so it always returns to MainPanel.
    void OnLevelBackClicked() => ShowMainPanel();

    void ShowQuizSelectPanel() {
        SetMultiplayerTabRowVisible(false);

        _quizNameFilter = "";
        if (quizFilterInput != null) quizFilterInput.SetTextWithoutNotify("");

        SwitchToPanel(quizSelectPanel);
        startButtonLabel.text = _pendingMode == GameMode.Host ? "Create Lobby" : "Start Game";
        startButton.interactable = false; // nothing selected yet after entering this panel
        statusText.text = "";

        ApplySubjectFilterAndGating();
    }

    void ApplySubjectFilterAndGating() {
        var completed = AuthManager.Instance.CurrentProfile?.CompletedQuizSets ?? new Dictionary<string, Timestamp>();

        // Show only cards matching the selected subject AND the name filter.
        foreach (var item in _quizItems)
            item.gameObject.SetActive(item.MatchesSubject(_selectedSubject) && MatchesNameFilter(item.SetName, _quizNameFilter));

        // Gate in order within the subject.
        var subjectItems = _quizItems
            .Where(i => i.MatchesSubject(_selectedSubject))
            .OrderBy(i => i.Order)
            .ToList();

        bool isFirstLocked = true;
        for (int i = 0; i < subjectItems.Count; i++) {
            subjectItems[i].transform.SetSiblingIndex(i);

            if (i == 0) {
                subjectItems[i].SetLocked(false);
                continue;
            }

            var prev = subjectItems[i - 1];
            bool locked = !completed.ContainsKey(prev.SetId);

            if (isFirstLocked && locked) {
                isFirstLocked = false;
                subjectItems[i].SetLocked(true, prev.SetName);
            } else {
                subjectItems[i].SetLocked(locked);
            }

            //subjectItems[i].SetLocked(locked, prev.SetName);
        }
    }

    void OnSubjectFilterChanged(string value) {
        _subjectNameFilter = value.Trim();
        ApplySubjectNameFilter();
    }

    void ApplySubjectNameFilter() {
        foreach (var item in _subjectItems)
            item.gameObject.SetActive(MatchesNameFilter(item.Subject, _subjectNameFilter));
    }

    void OnQuizFilterChanged(string value) {
        _quizNameFilter = value.Trim();
        ApplySubjectFilterAndGating();
    }

    static bool MatchesNameFilter(string name, string filter) =>
        string.IsNullOrEmpty(filter) ||
        (!string.IsNullOrEmpty(name) && name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);

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
        GameModeManager.Instance.SetHostMode(_selectedQuizSetId, _selectedQuizSetName, _selectedLevelSceneName);
        GameModeManager.Instance.SetConnectionMode(_activeConnectionMode);

        bool isPublic = _activeConnectionMode == ConnectionMode.Relay && publicSessionToggle != null && publicSessionToggle.isOn;
        GameModeManager.Instance.SetIsPublicSession(isPublic);

        statusText.text = "";
        startButton.interactable = false;

        if (_activeConnectionMode == ConnectionMode.Relay) {
            LoadingScreenController.Instance.Show("Creating online lobby...", 1f, 0f, 0.3f);
            try {
                string joinCode = await RelayManager.Instance.CreateRelayAsync(maxPlayers: ConnectionApprovalHandler.MaxPlayers);
                GameModeManager.Instance.SetRelayJoinCode(joinCode);

                int questionCount = QuizRepository.Instance.GetSetById(_selectedQuizSetId).questions.Count;

                await LobbyManager.Instance.CreateLobbyAsync(
                    hostName: AuthManager.Instance.CurrentProfile.DisplayName ?? SettingsManager.Instance.Current.playerName,
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
                LoadingScreenController.Instance.SetMessage("Failed to create online lobby.", LoadingScreenController.MessageColor.Error);
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
            GameModeManager.Instance.SelectedQuizSetId,
            GameModeManager.Instance.SelectedQuizSetName,
            GameModeManager.Instance.SelectedLevelSceneName
        );
        SpawnChatManager();

        // Call this when the game ends, not on start:
        QuizFetcher.Instance.IncrementPlayCount(_selectedQuizSetId);

        int questionCount = QuizRepository.Instance.GetSetById(GameModeManager.Instance.SelectedQuizSetId)?.questions.Count ?? 0;

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
        GameModeManager.Instance.SetSinglePlayerMode(_selectedQuizSetId, _selectedQuizSetName, _selectedLevelSceneName);

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
            GameModeManager.Instance.SelectedQuizSetId,
            GameModeManager.Instance.SelectedQuizSetName,
            GameModeManager.Instance.SelectedLevelSceneName
        );
        SpawnChatManager();

        // No LAN broadcast, no Lobby — straight to the chosen level
        // Call this when the game ends, not on start:
        QuizFetcher.Instance.IncrementPlayCount(_selectedQuizSetId);

        NetworkManager.Singleton.SceneManager.LoadScene(GameModeManager.Instance.SelectedLevelSceneName, LoadSceneMode.Single);
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

        if (AuthManager.Instance.CurrentUser == null) return;
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
        else if (_selectedOnlineSession != null)
            JoinOnlineSession(_selectedOnlineSession.RelayJoinCode);
        else if (code.Length >= 6)
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
        LoadingScreenController.Instance.Show("Joining online lobby...");

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
            playerName = AuthManager.Instance.CurrentProfile?.DisplayName ?? SettingsManager.Instance.Current.playerName,
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

    void SpawnGameSessionManager(string quizSetId, string quizSetName, string levelSceneName) {
        if (GameSessionManager.Instance != null) {
            GameSessionManager.Instance.SetSelectedQuizSet(quizSetId, quizSetName);
            GameSessionManager.Instance.SetSelectedLevel(levelSceneName);
            return;
        }

        var go = Instantiate(gameSessionManagerPrefab);
        go.GetComponent<NetworkObject>().Spawn();

        var session = go.GetComponent<GameSessionManager>();
        session.SetSelectedQuizSet(quizSetId, quizSetName);
        session.SetSelectedLevel(levelSceneName);
    }

    void SpawnChatManager() {
        if (ChatManager.Instance != null) return;

        var go = Instantiate(chatManagerPrefab);
        go.GetComponent<NetworkObject>().Spawn();
        ChatManager.Instance.SendSystemMessage(
            "Welcome ALPHA Tester(s). Game breaking bugs are to be expected, embrace yourselves :)");
    }

    public void StartTutorial() {
        List<QuizSetMetaEntry> localMeta = QuizRepository.Instance.GetLocalSetMeta();

        _pendingMode = GameMode.SinglePlayer;
        _selectedLevelSceneName = "Tutorial";
        _selectedQuizSetName = localMeta[0].name;
        _selectedQuizSetId = localMeta[0].setId;

        ActionbarToastNotification.Instance.ClearToast();

        StartCoroutine(StartTutorialScene());
    }

    IEnumerator StartTutorialScene() {
        GameModeManager.Instance.SetSinglePlayerMode(_selectedQuizSetId, _selectedQuizSetName, _selectedLevelSceneName);

        ConfigureTransport("127.0.0.1", gamePort);

        LoadingScreenController.Instance.Show("Loading Tutorial...");
        yield return new WaitForSeconds(1f);

        NetworkManager.Singleton.OnServerStarted += OnTutorialServerStarted;
        NetworkManager.Singleton.StartHost();
    }

    void OnTutorialServerStarted() {
        NetworkManager.Singleton.OnServerStarted -= OnTutorialServerStarted;

        SpawnGameSessionManager(
            GameModeManager.Instance.SelectedQuizSetId,
            GameModeManager.Instance.SelectedQuizSetName,
            GameModeManager.Instance.SelectedLevelSceneName
        );

        if (ChatManager.Instance != null) return;

        var go = Instantiate(chatManagerPrefab);
        go.GetComponent<NetworkObject>().Spawn();
        ChatManager.Instance.SendSystemMessage($"Welcome {SettingsManager.Instance.Current.playerName}.");

        // No LAN broadcast, no Lobby — straight to the chosen level
        // Call this when the game ends, not on start:
        QuizFetcher.Instance.IncrementPlayCount(_selectedQuizSetId);

        NetworkManager.Singleton.SceneManager.LoadScene(GameModeManager.Instance.SelectedLevelSceneName, LoadSceneMode.Single);
    }
}