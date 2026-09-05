using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

[Serializable]
public class GameSettings {
    public string playerName = "Player";
    public bool isFirstPerson = true;
    public int qualityLevel = 1;   // index into QualitySettings.names
    public bool showDebugOverlay = false;
    public bool showNameTags = true;
    public bool vsyncEnabled = false;
    public int targetFrameRate = 60;
    public bool completedTutorial = false;
    public float bgmVolume = 1f;   // linear 0-1
    public float sfxVolume = 1f;   // linear 0-1

    // elementId -> saved offset/scale for a CustomizableUIElement
    public Dictionary<string, ButtonLayoutEntry> buttonLayouts = new Dictionary<string, ButtonLayoutEntry>();

    // ── key|value serialization ──────────────────────────────
    // One "key|value" pair per line. Order doesn't matter on load;
    // unknown keys are ignored, missing keys keep their default value.
    // "layout" lines use a nested "id|x|y|scale" value (see FromKeyValueString).

    public string ToKeyValueString() {
        var sb = new StringBuilder();
        sb.Append("playerName|").Append(playerName).Append('\n');
        sb.Append("isFirstPerson|").Append(isFirstPerson ? 1 : 0).Append('\n');
        sb.Append("qualityLevel|").Append(qualityLevel.ToString(CultureInfo.InvariantCulture)).Append('\n');
        sb.Append("showDebugOverlay|").Append(showDebugOverlay ? 1 : 0).Append('\n');
        sb.Append("showNameTags|").Append(showNameTags ? 1 : 0).Append('\n');
        sb.Append("vsyncEnabled|").Append(vsyncEnabled ? 1 : 0).Append('\n');
        sb.Append("targetFrameRate|").Append(targetFrameRate).Append('\n');
        sb.Append("completedTutorial|").Append(completedTutorial ? 1 : 0).Append('\n');
        sb.Append("bgmVolume|").Append(bgmVolume.ToString(CultureInfo.InvariantCulture)).Append('\n');
        sb.Append("sfxVolume|").Append(sfxVolume.ToString(CultureInfo.InvariantCulture)).Append('\n');

        foreach (var kvp in buttonLayouts) {
            sb.Append("layout|").Append(kvp.Key).Append('|')
              .Append(kvp.Value.x.ToString(CultureInfo.InvariantCulture)).Append('|')
              .Append(kvp.Value.y.ToString(CultureInfo.InvariantCulture)).Append('|')
              .Append(kvp.Value.scale.ToString(CultureInfo.InvariantCulture)).Append('\n');
        }

        return sb.ToString();
    }

    public static GameSettings FromKeyValueString(string raw) {
        var settings = new GameSettings();
        if (string.IsNullOrEmpty(raw)) return settings;

        string[] lines = raw.Split('\n');
        foreach (string line in lines) {
            if (string.IsNullOrWhiteSpace(line)) continue;

            int sep = line.IndexOf('|');
            if (sep < 0) continue; // malformed line, skip

            string key = line.Substring(0, sep).Trim();
            string value = line.Substring(sep + 1).Trim('\r', '\n', ' ');

            switch (key) {
                case "playerName":
                    if (string.IsNullOrEmpty(value)) {
                        settings.playerName = "Player";
                        break;
                    }

                    settings.playerName = value.Length > 30 ? value[..30] : value;
                    break;
                case "isFirstPerson":
                    settings.isFirstPerson = value == "1";
                    break;
                case "qualityLevel":
                    int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out settings.qualityLevel);
                    break;
                case "showDebugOverlay":
                    settings.showDebugOverlay = value == "1";
                    break;
                case "showNameTags":
                    settings.showNameTags = value == "1";
                    break;
                case "vsyncEnabled":
                    settings.vsyncEnabled = value == "1";
                    break;
                case "targetFrameRate":
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out settings.targetFrameRate))
                        if (settings.targetFrameRate < 30) settings.targetFrameRate = 60;
                        else settings.targetFrameRate = 60;
                    break;
                case "completedTutorial":
                    settings.completedTutorial = value == "1";
                    break;
                case "bgmVolume":
                    if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float bgmVol))
                        settings.bgmVolume = Mathf.Clamp01(bgmVol);
                    break;
                case "sfxVolume":
                    if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float sfxVol))
                        settings.sfxVolume = Mathf.Clamp01(sfxVol);
                    break;
                case "layout":
                    // value is "elementId|x|y|scale" — only the outer split (on the first '|')
                    // happened above, so split the remainder ourselves.
                    string[] p = value.Split('|');
                    if (p.Length == 4 &&
                        float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float lx) &&
                        float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float ly) &&
                        float.TryParse(p[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float ls)) {
                        settings.buttonLayouts[p[0]] = new ButtonLayoutEntry(lx, ly, ls);
                    }
                    break;
                default:
                    // unknown key — ignore, keeps format forward-compatible
                    break;
            }
        }

        return settings;
    }
}