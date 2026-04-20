using UnityEngine;
using UnityEngine.UI;

public class DebugMode : MonoBehaviour {
    public SaveManager saveManager;
    public DebugSetting debugSetting;

    [SerializeField] private Toggle toggle;
    private bool currentDebugSetting = false;

    void Start() {
        currentDebugSetting = saveManager.GetOneData(0);
        UpdateDebugMode();
    }

    public void DebugModeSetting(Toggle value) {
        Debug.Log(value.isOn);
        currentDebugSetting = value.isOn;
        saveManager.SaveOneData(value.isOn, "debugMode");
        debugSetting.RefreshDebugMode();
    }

    private void UpdateDebugMode() {
        toggle.isOn = currentDebugSetting;
    }
}
