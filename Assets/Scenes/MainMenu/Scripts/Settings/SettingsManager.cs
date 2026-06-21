using System;
using System.IO;
using UnityEngine;

public class SettingsManager : MonoBehaviour {
    public static SettingsManager Instance { get; private set; }
    public GameSettings Current { get; private set; }

    [Header("Defaults")]
    [Tooltip("Assign the DefaultSettingsConfig asset here")]
    [SerializeField] private DefaultSettingsConfig defaultConfig;

    private const string FileName = "settings.json";
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

        string json = JsonUtility.ToJson(settings, true);
        var wrapper = new Wrapper {
            payload = json,
            signature = QuizDataIntegrity.ComputeSignature(json)
        };

        File.WriteAllText(FilePath, JsonUtility.ToJson(wrapper, true));
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
            var wrapper = JsonUtility.FromJson<Wrapper>(raw);

            if (!QuizDataIntegrity.Verify(wrapper.payload, wrapper.signature)) {
                Debug.LogWarning("[SettingsManager] Integrity check failed — using defaults.");
                Current = defaultConfig != null
                    ? defaultConfig.ToGameSettings()
                    : new GameSettings();
            } else {
                Current = JsonUtility.FromJson<GameSettings>(wrapper.payload);
                Debug.Log("[SettingsManager] Loaded and verified.");
            }
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

    // ── Internal wrapper ────────────────────────────────────
    [Serializable]
    private class Wrapper {
        public string payload;
        public string signature;
    }
}