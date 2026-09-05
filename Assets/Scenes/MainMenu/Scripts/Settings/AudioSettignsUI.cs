using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AudioSettingsUI : MonoBehaviour {
    [Header("BGM")]
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private TMP_Text bgmLabel;

    [Header("SFX")]
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private TMP_Text sfxLabel;

    [SerializeField] private float autoSaveDebounceSeconds = 0.5f;

    private float _bgmVolume;
    private float _sfxVolume;

    private Coroutine _bgmDebounceRoutine;
    private Coroutine _sfxDebounceRoutine;
    private bool _hasPendingBgmSave;
    private bool _hasPendingSfxSave;

    void Start() {
        bgmSlider.minValue = 0f;
        bgmSlider.maxValue = 1f;
        bgmSlider.onValueChanged.AddListener(SetBgmVolume);

        sfxSlider.minValue = 0f;
        sfxSlider.maxValue = 1f;
        sfxSlider.onValueChanged.AddListener(SetSfxVolume);

        if (SettingsManager.Instance != null)
            Populate(SettingsManager.Instance.Current);
    }

    void OnEnable() {
        if (SettingsManager.Instance != null)
            Populate(SettingsManager.Instance.Current);
    }

    // Flush any debounced save that hasn't fired yet before the panel hides —
    // otherwise the coroutine (living on this now-hidden GameObject) dies and the value is lost.
    void OnDisable() {
        if (_hasPendingBgmSave) {
            if (_bgmDebounceRoutine != null) { StopCoroutine(_bgmDebounceRoutine); _bgmDebounceRoutine = null; }
            AutoSave(s => s.bgmVolume = _bgmVolume);
            _hasPendingBgmSave = false;
        }

        if (_hasPendingSfxSave) {
            if (_sfxDebounceRoutine != null) { StopCoroutine(_sfxDebounceRoutine); _sfxDebounceRoutine = null; }
            AutoSave(s => s.sfxVolume = _sfxVolume);
            _hasPendingSfxSave = false;
        }
    }

    // ── BGM slider ───────────────────────────────────────────
    private void SetBgmVolume(float value) {
        _bgmVolume = value;

        if (AudioSettingsManager.Instance != null)
            AudioSettingsManager.Instance.SetBgmVolume(value); // apply immediately, feels responsive while dragging

        if (bgmLabel != null)
            bgmLabel.text = $"{Mathf.RoundToInt(value * 100f)}%";

        _hasPendingBgmSave = true;
        if (_bgmDebounceRoutine != null)
            StopCoroutine(_bgmDebounceRoutine);

        if (gameObject.activeInHierarchy)
            _bgmDebounceRoutine = StartCoroutine(DebouncedSave(
                () => { AutoSave(s => s.bgmVolume = _bgmVolume); _hasPendingBgmSave = false; _bgmDebounceRoutine = null; }));
    }

    // ── SFX slider ───────────────────────────────────────────
    private void SetSfxVolume(float value) {
        _sfxVolume = value;

        if (AudioSettingsManager.Instance != null)
            AudioSettingsManager.Instance.SetSfxVolume(value);

        if (sfxLabel != null)
            sfxLabel.text = $"{Mathf.RoundToInt(value * 100f)}%";

        _hasPendingSfxSave = true;
        if (_sfxDebounceRoutine != null)
            StopCoroutine(_sfxDebounceRoutine);

        if (gameObject.activeInHierarchy)
            _sfxDebounceRoutine = StartCoroutine(DebouncedSave(
                () => { AutoSave(s => s.sfxVolume = _sfxVolume); _hasPendingSfxSave = false; _sfxDebounceRoutine = null; }));
    }

    private IEnumerator DebouncedSave(System.Action onSaved) {
        yield return new WaitForSecondsRealtime(autoSaveDebounceSeconds);
        onSaved();
    }

    // ── Auto-save ────────────────────────────────────────────
    private void AutoSave(System.Action<GameSettings> mutate) {
        if (SettingsManager.Instance == null) return;
        SettingsManager.Instance.Save(mutate);
    }

    // ── Populate from loaded settings ────────────────────────
    private void Populate(GameSettings s) {
        _bgmVolume = s.bgmVolume;
        bgmSlider.SetValueWithoutNotify(s.bgmVolume);
        if (bgmLabel != null)
            bgmLabel.text = $"{Mathf.RoundToInt(s.bgmVolume * 100f)}%";

        _sfxVolume = s.sfxVolume;
        sfxSlider.SetValueWithoutNotify(s.sfxVolume);
        if (sfxLabel != null)
            sfxLabel.text = $"{Mathf.RoundToInt(s.sfxVolume * 100f)}%";
    }
}