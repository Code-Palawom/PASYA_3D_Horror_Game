using Firebase.Firestore;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class VideoSettings : MonoBehaviour {
    [Header("Quality Carousel")]
    [SerializeField] private Button btnQualityPrev;
    [SerializeField] private Button btnQualityNext;
    [SerializeField] private TMP_Text qualityLabel;

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

    [Header("Name Tag Toggle")]
    [SerializeField] private Toggle nameTagToggle;

    // ── State ────────────────────────────────────────────────
    private bool _isFirstPerson;
    private int _qualityIndex;
    private string[] _qualityNames;
    private bool _showNameTags;
    private bool _vsyncEnabled;
    private int _targetFrameRate;
    private float _maxRefreshRate;
    private Coroutine _frameRateDebounceRoutine;
    private bool _hasPendingFrameRateSave;

    // ── Init ────────────────────────────────────────────────
    void Awake() {
        _qualityNames = QualitySettings.names;
        _maxRefreshRate = DeviceFrameRate.GetMaxRefreshRate();
    }

    void Start() {
        // Quality carousel
        btnQualityPrev.onClick.AddListener(() => StepQuality(-1));
        btnQualityNext.onClick.AddListener(() => StepQuality(+1));

        // POV toggle
        btnFirstPerson.onClick.AddListener(() => SetPOV(true));
        btnThirdPerson.onClick.AddListener(() => SetPOV(false));

        nameTagToggle.onValueChanged.AddListener((isOn) => SetNameTagVisibility(isOn));

        // VSync toggle
        if (vsyncToggle != null)
            vsyncToggle.onValueChanged.AddListener((isOn) => SetVSync(isOn));

        // Frame rate slider
        if (frameRateSlider != null) {
            frameRateSlider.minValue = 30f;
            frameRateSlider.maxValue = Mathf.Max(60f, Mathf.Round(_maxRefreshRate));
            frameRateSlider.value = Mathf.Max(60f, Mathf.Round(_maxRefreshRate));
            frameRateSlider.onValueChanged.AddListener((value) => SetFrameRate(Mathf.RoundToInt(value)));
        }

        if (SettingsManager.Instance != null)
            Populate(SettingsManager.Instance.Current);
    }

    void OnEnable() {
        if (SettingsManager.Instance != null)
            Populate(SettingsManager.Instance.Current);
    }

    // Called right before the panel is hidden. Flushes any debounced save
    // that hasn't fired yet — otherwise navigating away mid-drag kills
    // the coroutine (which lives on this now-hidden GameObject) and the
    // value is lost.
    void OnDisable() {
        if (!_hasPendingFrameRateSave) return;

        if (_frameRateDebounceRoutine != null) {
            StopCoroutine(_frameRateDebounceRoutine);
            _frameRateDebounceRoutine = null;
        }
        AutoSave(s => s.targetFrameRate = _targetFrameRate);
        _hasPendingFrameRateSave = false;
    }

    // ── Quality carousel ─────────────────────────────────────
    private void StepQuality(int direction) {
        // Infinite wrap
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

    private void SetNameTagVisibility(bool show) {
        _showNameTags = show;
        AutoSave(s => s.showNameTags = show);
    }

    // ── VSync toggle ─────────────────────────────────────────
    private void SetVSync(bool isOn) {
        _vsyncEnabled = isOn;
        QualitySettings.vSyncCount = isOn ? 1 : 0;

        // On desktop/editor, VSync overrides targetFrameRate while enabled.
        // Re-apply the slider's frame rate immediately if VSync gets turned off
        // so the cap takes effect without needing another slider interaction.
        if (!isOn)
            Application.targetFrameRate = _targetFrameRate;

        AutoSave(s => s.vsyncEnabled = isOn);
    }

    // ── Frame rate slider ────────────────────────────────────
    private void SetFrameRate(int fps) {
        _targetFrameRate = fps;
        Application.targetFrameRate = fps; // apply immediately, feels responsive while dragging

        if (frameRateLabel != null)
            frameRateLabel.text = $"{fps} FPS";

        // Debounce the save, not the apply — restart the timer on every slider move
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

    // ── Populate from loaded settings ────────────────────────
    private void Populate(GameSettings s) {
        _qualityIndex = Mathf.Clamp(s.qualityLevel, 0, _qualityNames.Length - 1);
        qualityLabel.text = _qualityNames[_qualityIndex];

        // Apply POV without triggering AutoSave during populate
        _isFirstPerson = s.isFirstPerson;
        SetButtonColor(btnFirstPerson, s.isFirstPerson);
        SetButtonColor(btnThirdPerson, !s.isFirstPerson);

        // Name tag toggle
        nameTagToggle.SetIsOnWithoutNotify(s.showNameTags);

        // VSync toggle — apply without triggering AutoSave during populate
        _vsyncEnabled = s.vsyncEnabled;
        QualitySettings.vSyncCount = s.vsyncEnabled ? 1 : 0;
        if (vsyncToggle != null)
            vsyncToggle.SetIsOnWithoutNotify(s.vsyncEnabled);

        // Frame rate slider — default to max refresh rate if no saved value yet.
        // Clamp against current slider bounds in case the saved value came from
        // a different device/refresh rate than the one currently running.
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

    // ── Helpers ──────────────────────────────────────────────
    private void SetButtonColor(Button btn, bool isActive) {
        var colors = btn.colors;
        colors.normalColor = isActive ? activeColor : inactiveColor;
        btn.colors = colors;
    }
}