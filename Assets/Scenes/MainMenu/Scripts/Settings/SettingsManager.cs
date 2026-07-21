using System;
using System.IO;
using UnityEngine;

public class SettingsManager : MonoBehaviour {
    public static SettingsManager Instance { get; private set; }
    public GameSettings Current { get; private set; }

    [Header("Defaults")]
    [Tooltip("Assign the DefaultSettingsConfig asset here")]
    [SerializeField] private DefaultSettingsConfig defaultConfig;

    private const string FileName = "save.dat";
    private string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    public event Action<GameSettings> OnSettingsLoaded;
    public event Action<GameSettings> OnSettingsSaved;

    void Awake() {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Seed Current with defaults before Load so there's always a valid value
        Current = defaultConfig != null
            ? defaultConfig.ToGameSettings()
            : new GameSettings();

        Load();
    }

    // ── Save ────────────────────────────────────────────────
    public void Save(GameSettings settings) {
        Current = settings;

        File.WriteAllText(FilePath, settings.ToKeyValueString());
        Apply(Current);
        OnSettingsSaved?.Invoke(Current);
        Debug.Log("[SettingsManager] Saved.");
    }

    // ── Load ────────────────────────────────────────────────
    private void Load() {
        if (!File.Exists(FilePath)) {
            Debug.Log("[SettingsManager] No file found — using defaults.");
            // Current is already seeded from defaultConfig in Awake
            Apply(Current);
            OnSettingsLoaded?.Invoke(Current);
            return;
        }

        try {
            string raw = File.ReadAllText(FilePath);
            Current = GameSettings.FromKeyValueString(raw);
            Debug.Log("[SettingsManager] Loaded.");
        } catch (Exception e) {
            Debug.LogError($"[SettingsManager] Load error: {e.Message}");
            Current = defaultConfig != null
                ? defaultConfig.ToGameSettings()
                : new GameSettings();
        }

        Apply(Current);
        OnSettingsLoaded?.Invoke(Current);
    }

    // ── Apply to engine ─────────────────────────────────────
    private void Apply(GameSettings s) {
        QualitySettings.SetQualityLevel(s.qualityLevel, true);
        // POV is read by your camera/character via:
        // SettingsManager.Instance.Current.isFirstPerson
    }
}