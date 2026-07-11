using System;
using System.IO;
using System.Text;
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

        string json = JsonUtility.ToJson(settings, true);
        string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        File.WriteAllText(FilePath, encoded);
        Apply(Current);
        OnSettingsSaved?.Invoke(Current);
        Debug.Log("[SettingsManager] Saved.");
    }

    // ── Load ────────────────────────────────────────────────
    private void Load() {
        if (!File.Exists(FilePath)) {
            Debug.Log("[SettingsManager] No file found — using defaults.");
            Save(Current); // Save defaults to file for future loads)
            return;
        }

        try {
            string encoded = File.ReadAllText(FilePath);
            string json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            Current = JsonUtility.FromJson<GameSettings>(json);
            Debug.Log("[SettingsManager] Loaded.");
        } catch (Exception e) {
            Debug.LogError($"[SettingsManager] Load error: {e.Message}");
            Current = defaultConfig != null
                ? defaultConfig.ToGameSettings()
                : new GameSettings();
            Save(Current); // Save defaults to file for future loads)
        }

        Apply(Current);
        OnSettingsLoaded?.Invoke(Current);
    }

    // ── Apply to engine ─────────────────────────────────────
    private void Apply(GameSettings s) {
        QualitySettings.SetQualityLevel(s.qualityLevel, true);
        // POV is read by your camera/character via:
        // SettingsManager.Instance.Current.isFirstPerson

        QualitySettings.vSyncCount = s.vsyncEnabled ? 1 : 0;

        int maxRefreshRate = Mathf.RoundToInt(DeviceFrameRate.GetMaxRefreshRate());
        int fps = s.targetFrameRate > 0 ? s.targetFrameRate : maxRefreshRate;

        // Clamp against this device's max refresh rate in case the save came
        // from a different device (e.g. cloud save restore, or device swap).
        fps = Mathf.Min(fps, maxRefreshRate);

        Application.targetFrameRate = fps;

        Debug.Log(fps);
        Debug.Log(s.vsyncEnabled);
    }
}