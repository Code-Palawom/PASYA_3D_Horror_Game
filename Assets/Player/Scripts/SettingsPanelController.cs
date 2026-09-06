using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SettingsPanelController : MonoBehaviour {

    [Header("Quality Carousel")]
    [SerializeField] private TMP_Text qualityLabel;
    [SerializeField] private Button btnQualityPrev;
    [SerializeField] private Button btnQualityNext;

    [Header("V Sync")]
    [SerializeField] private Toggle vsyncToggle;

    [Header("Frame Rate")]
    [SerializeField] private Slider frameRateSlider;
    [SerializeField] private TMP_Text frameRateLabel;
    [SerializeField] private float autoSaveDebounceSeconds = 0.5f;

    [Header("POV")]
    [SerializeField] private Button btnFirstPerson;
    [SerializeField] private Button btnThirdPerson;

    [Header("POV Button Colors")]
    [SerializeField] private Color activeColor = new Color(0.20f, 0.60f, 1.00f);
    [SerializeField] private Color inactiveColor = new Color(0.30f, 0.30f, 0.30f);

    [Header("Name Tag")]
    [SerializeField] private Toggle nameTagToggle;

    [Header("Debug")]
    [SerializeField] private Toggle debugToggle;
    [SerializeField] private DebugSetting debug;

    [Header("Panel Actions")]
    [SerializeField] private Button backButton;
    [SerializeField] private PauseMenuController pauseMenu;

    // ── State ────────────────────────────────────────────────
    private string[] _qualityNames;
    private int _qualityIndex;
    private bool _isFirstPerson;
    private bool _showNameTags;
    private bool _showDebug;
    private bool _vsyncEnabled;
    private int _targetFrameRate;
    private float _maxRefreshRate;
    private Coroutine _frameRateDebounceRoutine;
    private bool _hasPendingFrameRateSave;

    // ── Init ────────────────────────────────────────────────
    void Awake() {
        _qualityNames = QualitySettings.names;
        _maxRefreshRate = DeviceFrameRate.GetMaxRefreshRate();

        btnQualityPrev.onClick.AddListener(() => StepQuality(-1));
        btnQualityNext.onClick.AddListener(() => StepQuality(+1));

        btnFirstPerson.onClick.AddListener(() => SetPOV(true));
        btnThirdPerson.onClick.AddListener(() => SetPOV(false));

        nameTagToggle.onValueChanged.AddListener(OnNameTagToggled);
        debugToggle.onValueChanged.AddListener(OnDebugToggled);

        if (vsyncToggle != null)
            vsyncToggle.onValueChanged.AddListener(SetVSync);

        if (frameRateSlider != null) {
            frameRateSlider.minValue = 30f;
            frameRateSlider.maxValue = Mathf.Max(60f, Mathf.Round(_maxRefreshRate));
            frameRateSlider.onValueChanged.AddListener((value) => SetFrameRate(Mathf.RoundToInt(value)));
        }

        backButton.onClick.AddListener(() => pauseMenu.OnBackFromSettings());
    }

    // Flush any debounced frame-rate save if the panel is hidden mid-drag —
    // otherwise the coroutine (living on this now-inactive GameObject) is
    // killed and the value never gets saved.
    void OnDisable() {
        if (!_hasPendingFrameRateSave) return;

        if (_frameRateDebounceRoutine != null) {
            StopCoroutine(_frameRateDebounceRoutine);
            _frameRateDebounceRoutine = null;
        }
        AutoSave(s => s.targetFrameRate = _targetFrameRate);
        _hasPendingFrameRateSave = false;
    }

    // ── Called by PauseMenuController when opening ───────────
    public void Open() {
        if (SettingsManager.Instance != null)
            Populate(SettingsManager.Instance.Current);
    }

    // ── Populate from loaded settings ────────────────────────
    private void Populate(GameSettings s) {
        _qualityIndex = Mathf.Clamp(s.qualityLevel, 0, _qualityNames.Length - 1);
        qualityLabel.text = _qualityNames[_qualityIndex];

        _isFirstPerson = s.isFirstPerson;
        SetButtonColor(btnFirstPerson, s.isFirstPerson);
        SetButtonColor(btnThirdPerson, !s.isFirstPerson);

        _showNameTags = s.showNameTags;
        nameTagToggle.SetIsOnWithoutNotify(_showNameTags);

        _showDebug = s.showDebugOverlay;
        debugToggle.SetIsOnWithoutNotify(_showDebug);

        _vsyncEnabled = s.vsyncEnabled;
        QualitySettings.vSyncCount = s.vsyncEnabled ? 1 : 0;
        if (vsyncToggle != null)
            vsyncToggle.SetIsOnWithoutNotify(s.vsyncEnabled);

        if (s.targetFrameRate > 0) {
            _targetFrameRate = frameRateSlider != null
                ? Mathf.Clamp(s.targetFrameRate, Mathf.RoundToInt(frameRateSlider.minValue), Mathf.RoundToInt(frameRateSlider.maxValue))
                : s.targetFrameRate;
        } else {
            _targetFrameRate = Mathf.RoundToInt(_maxRefreshRate);
        }

        Application.targetFrameRate = _targetFrameRate;
        if (frameRateSlider != null)
            frameRateSlider.SetValueWithoutNotify(_targetFrameRate);
        if (frameRateLabel != null)
            frameRateLabel.text = $"{_targetFrameRate} FPS";
    }

    // ── Quality carousel ─────────────────────────────────────
    private void StepQuality(int direction) {
        _qualityIndex = (_qualityIndex + direction + _qualityNames.Length) % _qualityNames.Length;
        qualityLabel.text = _qualityNames[_qualityIndex];
        AutoSave(s => s.qualityLevel = _qualityIndex);
    }

    // ── POV toggle ───────────────────────────────────────────
    private void SetPOV(bool firstPerson) {
        _isFirstPerson = firstPerson;
        SetButtonColor(btnFirstPerson, firstPerson);
        SetButtonColor(btnThirdPerson, !firstPerson);
        AutoSave(s => s.isFirstPerson = firstPerson);
    }

    private void SetButtonColor(Button btn, bool isActive) {
        var colors = btn.colors;
        colors.normalColor = isActive ? activeColor : inactiveColor;
        btn.colors = colors;
    }

    // ── Name tag toggle ──────────────────────────────────────
    private void OnNameTagToggled(bool value) {
        _showNameTags = value;
        PlayerNameDisplay.All.ForEach(p => p.SetNameTagVisible(value));
        AutoSave(s => s.showNameTags = value);
    }

    // ── Debug toggle ─────────────────────────────────────────
    private void OnDebugToggled(bool value) {
        _showDebug = value;
        AutoSave(s => s.showDebugOverlay = value);
        debug.RefreshDebugMode();
    }

    // ── VSync toggle ─────────────────────────────────────────
    private void SetVSync(bool isOn) {
        _vsyncEnabled = isOn;
        QualitySettings.vSyncCount = isOn ? 1 : 0;

        // VSync overrides targetFrameRate while enabled on desktop/editor;
        // re-apply the slider's value immediately when VSync turns off.
        if (!isOn)
            Application.targetFrameRate = _targetFrameRate;

        AutoSave(s => s.vsyncEnabled = isOn);
    }

    // ── Frame rate slider ────────────────────────────────────
    private void SetFrameRate(int fps) {
        _targetFrameRate = fps;
        Application.targetFrameRate = fps;

        if (frameRateLabel != null)
            frameRateLabel.text = $"{fps} FPS";

        _hasPendingFrameRateSave = true;
        if (_frameRateDebounceRoutine != null)
            StopCoroutine(_frameRateDebounceRoutine);

        if (gameObject.activeInHierarchy)
            _frameRateDebounceRoutine = StartCoroutine(DebouncedFrameRateSave());
    }

    private System.Collections.IEnumerator DebouncedFrameRateSave() {
        yield return new WaitForSecondsRealtime(autoSaveDebounceSeconds);
        AutoSave(s => s.targetFrameRate = _targetFrameRate);
        _hasPendingFrameRateSave = false;
        _frameRateDebounceRoutine = null;
    }

    // ── Auto-save ────────────────────────────────────────────
    private void AutoSave(Action<GameSettings> mutate) {
        if (SettingsManager.Instance == null) return;
        SettingsManager.Instance.Save(mutate);
    }
}
