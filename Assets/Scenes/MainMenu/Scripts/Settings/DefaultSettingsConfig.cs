using UnityEngine;

/// <summary>
/// Tweak default settings from the Inspector without touching code.
/// Create via: Assets → Create → Settings → Default Config
/// </summary>
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

    /// <summary>
    /// Converts this config into a GameSettings instance.
    /// </summary>
    public GameSettings ToGameSettings() => new GameSettings {
        playerName = playerName,
        isFirstPerson = isFirstPerson,
        qualityLevel = Mathf.Clamp(qualityLevel, 0, QualitySettings.names.Length - 1)
    };
}