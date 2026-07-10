using UnityEngine;

// Tweak default settings from the Inspector without touching code.
// Create via: Assets → Create → Settings → Default Config
[CreateAssetMenu(fileName = "DefaultSettingsConfig", menuName = "Settings/Default Config")]
public class DefaultSettingsConfig : ScriptableObject {
    [Header("Player")]
    public string playerName = "Player";

    [Header("POV")]
    public bool isFirstPerson = true;

    [Header("Graphics")]
    [Tooltip("Index into QualitySettings.names (0 = lowest, highest index = best)")]
    [Range(0, 2)]
    public int qualityLevel = 2;

    [Header("Frame Rate")]
    public int targetFrameRate = 60;

    [Header("VSync")]
    public int vsyncEnabled = 1;

    [Header("Show Name Tag")]
    public bool showNameTags = true;

    [Header("Debug Mode")]
    public bool showDebug = false;

    // Converts this config into a GameSettings instance.
    public GameSettings ToGameSettings() => new GameSettings {
        playerName = playerName,
        isFirstPerson = isFirstPerson,
        qualityLevel = Mathf.Clamp(qualityLevel, 0, QualitySettings.names.Length - 1),
        showNameTags = showNameTags,
        showDebugOverlay = showDebug
    };
}