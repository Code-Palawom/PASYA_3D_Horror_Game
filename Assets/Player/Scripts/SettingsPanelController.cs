using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class SettingsPanelController : MonoBehaviour {

    [Header("Quality")]
    [SerializeField] private TextMeshProUGUI qualityLabel;
    [SerializeField] private Button prevQualityButton;
    [SerializeField] private Button nextQualityButton;

    [Header("POV")]
    [SerializeField] private Button firstPersonButton;
    [SerializeField] private Button thirdPersonButton;

    [Header("NameTag")]
    [SerializeField] private Toggle nameTagToggle;

    [Header("Debug")]
    [SerializeField] private Toggle debugToggle;
    [SerializeField] private DebugSetting debug;

    [Header("Panel Actions")]
    [SerializeField] private Button backButton;
    [SerializeField] private PauseMenuController pauseMenu;

    // ── Working state ────────────────────────────────────────
    private string[] _qualityNames;
    private int _qualityIndex;
    private bool _isFirstPerson;
    private bool _showDebug;
    private bool _nameTag;

    void Awake() {
        _qualityNames = QualitySettings.names;

        prevQualityButton.onClick.AddListener(OnPrevQuality);
        nextQualityButton.onClick.AddListener(OnNextQuality);
        firstPersonButton.onClick.AddListener(OnSelectFirstPerson);
        thirdPersonButton.onClick.AddListener(OnSelectThirdPerson);
        debugToggle.onValueChanged.AddListener(OnDebugToggled);
        nameTagToggle.onValueChanged.AddListener(OnNameTagToggled);
        backButton.onClick.AddListener(() => pauseMenu.OnBackFromSettings());
    }

    // ── Called by PauseMenuController when opening ──────────
    public void Open() {
        GameSettings s = SettingsManager.Instance.Current;

        //_qualityIndex = Mathf.Clamp(s.qualityLevel, 0, _qualityNames.Length - 1);
        Debug.Log(_qualityIndex);
        RefreshQualityLabel();

        _isFirstPerson = s.isFirstPerson;
        RefreshPOVButtons();

        _nameTag = s.showNameTags;
        nameTagToggle.SetIsOnWithoutNotify(_nameTag);

        _showDebug = s.showDebugOverlay;
        debugToggle.SetIsOnWithoutNotify(_showDebug);
    }

    // ── Quality carousel ─────────────────────────────────────
    void OnPrevQuality() {
        _qualityIndex = (_qualityIndex - 1 + _qualityNames.Length) % _qualityNames.Length;
        RefreshQualityLabel();
        AutoSave(s => s.qualityLevel = _qualityIndex);
    }

    void OnNextQuality() {
        _qualityIndex = (_qualityIndex + 1) % _qualityNames.Length;
        RefreshQualityLabel();
        AutoSave(s => s.qualityLevel = _qualityIndex);
    }

    void RefreshQualityLabel() {
        qualityLabel.text = _qualityNames[_qualityIndex];
    }

    // ── POV toggle ───────────────────────────────────────────
    void OnSelectFirstPerson() {
        _isFirstPerson = true;
        RefreshPOVButtons();
        AutoSave(s => s.isFirstPerson = true);
    }

    void OnSelectThirdPerson() {
        _isFirstPerson = false;
        RefreshPOVButtons();
        AutoSave(s => s.isFirstPerson = false);
    }

    void RefreshPOVButtons() {
        firstPersonButton.interactable = !_isFirstPerson;
        thirdPersonButton.interactable = _isFirstPerson;
        firstPersonButton.image.color = _isFirstPerson ? Color.blue : Color.gray;
        thirdPersonButton.image.color = !_isFirstPerson ? Color.blue : Color.gray;
    }

    // ── Debug toggle ─────────────────────────────────────────
    void OnDebugToggled(bool value) {
        _showDebug = value;
        AutoSave(s => s.showDebugOverlay = value);
        debug.RefreshDebugMode();
    }

    void OnNameTagToggled(bool value) {
        _nameTag = value;
        PlayerNameDisplay.All.ForEach(p => p.SetNameTagVisible(_nameTag));
        AutoSave(s => s.showNameTags = value);
    }

    // ── Auto-save on every change ────────────────────────────
    private void AutoSave(Action<GameSettings> mutate) {
        if (SettingsManager.Instance == null) return;
        SettingsManager.Instance.Save(mutate);
    }
}