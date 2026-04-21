using UnityEngine;
using UnityEngine.UI;

public class DebugModeSetting : MonoBehaviour {
    public SaveManager saveManager;

    [SerializeField] private Toggle toggle;
    private bool currentDebugSetting = false;

    void Start() {
        currentDebugSetting = saveManager.GetOneData(0);
        UpdateDebugMode();
    }

    public void DebugModeSettings(Toggle value) {
        currentDebugSetting = value.isOn;
        saveManager.SaveOneData(value.isOn, "debugMode");
    }

    private void UpdateDebugMode() {
        toggle.isOn = currentDebugSetting;
    }
}
