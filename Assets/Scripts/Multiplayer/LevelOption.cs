using UnityEngine;

// One selectable level entry shown in the Host/Single Player level dropdowns,
// and used to resolve display name + preview image for discovered LAN hosts.
// Configure these manually in MainMenuUI's Inspector list — one entry
// per playable level scene in your Build Settings.
[System.Serializable]
public class LevelOption {
    public string displayName;     // shown in lists, e.g. "Forest Temple"
    public string sceneName;       // exact scene name in Build Settings, e.g. "Level_Forest"
    public Sprite previewImage;    // shown in the Join tab's room detail panel
}