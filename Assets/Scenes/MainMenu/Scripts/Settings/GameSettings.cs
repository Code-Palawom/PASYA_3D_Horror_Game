using System;

[Serializable]
public class GameSettings {
    public string playerName = "Player";
    public bool isFirstPerson = true;
    public int qualityLevel = 1;   // index into QualitySettings.names
    public bool showDebugOverlay = false;
    public bool showNameTags = true;
}