using Firebase.Firestore;
using System;
using System.Collections;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SettingsUI : MonoBehaviour {
    [SerializeField] private MainMenuUI mainMenuUI;
    
    [Header("Name")]
    [SerializeField] private TMP_InputField nameField;
    [SerializeField] private TMP_Text nameChangeStatus;
    [SerializeField] private Color canCangeNameColor;
    [SerializeField] private Color cannotChangeNameColor;
    [SerializeField] private Button nameSaveButton;

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

    [SerializeField] private Button playTutorial;

    [Header("Download Button")]
    [SerializeField] private Button downloadUpdate;

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
        playTutorial.onClick.AddListener(() => mainMenuUI.StartTutorial());

        // Quality carousel
        btnQualityPrev.onClick.AddListener(() => StepQuality(-1));
        btnQualityNext.onClick.AddListener(() => StepQuality(+1));

        // POV toggle
        btnFirstPerson.onClick.AddListener(() => SetPOV(true));
        btnThirdPerson.onClick.AddListener(() => SetPOV(false));

        nameTagToggle.onValueChanged.AddListener((isOn) => SetNameTagVisibility(isOn));
        nameSaveButton.onClick.AddListener(() => ChangeName());

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

        AuthManager.Instance.OnPlayerStatsLoaded += RefreshName;
        AuthManager.Instance.OnAuthStateChanged += (user) => {
            if (user == null) {
                nameField.text = SettingsManager.Instance.Current.playerName;
                nameChangeStatus.text = "";
                nameChangeStatus.color = canCangeNameColor;
                nameSaveButton.interactable = true;
                nameField.interactable = true;
            }
        };

        if (AuthManager.Instance.CurrentProfile != null)
            RefreshName(AuthManager.Instance.CurrentProfile);

        if (SettingsManager.Instance != null)
            Populate(SettingsManager.Instance.Current);

        if(VersionChecker.Instance.IsNotOnLastestVersion && VersionChecker.Instance.DownloadURL != "") {
            downloadUpdate.gameObject.SetActive(true);
        }
    }

    // ── Called by your panel manager ─────────────────────────
    public void Show() {
        gameObject.SetActive(true);
        Refresh();
    }

    public void Hide() {
        // Flush any debounced frame rate save that hasn't fired yet —
        // otherwise closing the panel mid-drag kills the coroutine and the value is lost.
        if (_hasPendingFrameRateSave) {
            if (_frameRateDebounceRoutine != null) {
                StopCoroutine(_frameRateDebounceRoutine);
                _frameRateDebounceRoutine = null;
            }
            AutoSave(s => s.targetFrameRate = _targetFrameRate);
            _hasPendingFrameRateSave = false;
        }

        gameObject.SetActive(false);
    }

    public void Refresh() {
        if (SettingsManager.Instance != null)
            Populate(SettingsManager.Instance.Current);
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
        if (AuthManager.Instance.CurrentProfile != null) {
            PlayerProfile player = AuthManager.Instance.CurrentProfile;
            nameField.text = player.DisplayName;

            if (player.LastNameChange is Timestamp lastChange) {
                var elapsed = DateTime.UtcNow - lastChange.ToDateTime();
                if (elapsed.TotalDays > 14) {
                    nameChangeStatus.color = canCangeNameColor;
                    nameChangeStatus.text = "Name change available";
                    nameSaveButton.interactable = true;
                    nameField.interactable = true;
                } else {
                    double daysLeft = 14 - elapsed.TotalDays;
                    nameChangeStatus.color = cannotChangeNameColor;
                    nameChangeStatus.text = $"Available in {daysLeft:F1} day(s).";
                }
            } else {
                nameChangeStatus.color = canCangeNameColor;
                nameChangeStatus.text = "Name change available";
                nameSaveButton.interactable = true;
                nameField.interactable = true;
            }
        } else {
            nameField.text = SettingsManager.Instance.Current.playerName;
            nameChangeStatus.text = "";
            nameChangeStatus.color = canCangeNameColor;
            nameSaveButton.interactable = true;
            nameField.interactable = true;
        }

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

    private void RefreshName(PlayerProfile profile) {
        if (profile != null) {
            nameField.text = profile.DisplayName;

            if (profile.LastNameChange is Timestamp lastChange) {
                var elapsed = DateTime.UtcNow - lastChange.ToDateTime();
                if (elapsed.TotalDays > 14) {
                    nameChangeStatus.color = canCangeNameColor;
                    nameChangeStatus.text = "Name change available";
                    nameSaveButton.interactable = true;
                    nameField.interactable = true;
                } else {
                    double daysLeft = 14 - elapsed.TotalDays;
                    nameChangeStatus.color = cannotChangeNameColor;
                    nameChangeStatus.text = $"Available in {daysLeft:F1} day(s).";
                }
            } else {
                nameChangeStatus.color = canCangeNameColor;
                nameChangeStatus.text = "Name change available";
                nameSaveButton.interactable = true;
                nameField.interactable = true;
            }
        } else {
            nameField.text = SettingsManager.Instance.Current.playerName;
        }
    }

    private async void ChangeName() {
        string newName = nameField.text.Trim();

        if (AuthManager.Instance.CurrentProfile == null) {
            if (AuthManager.Instance.CurrentProfile == null) {
                SettingsManager.Instance.Save(s => s.playerName = newName);
            }
        } else {
            if (string.IsNullOrEmpty(newName)) {
                nameChangeStatus.text = "Please enter a name.";
                return;
            } else if (AuthManager.Instance.CurrentProfile.DisplayName == newName) {
                nameChangeStatus.text = "That's already your name.";
                return;
            }

            nameSaveButton.interactable = false; // prevent double-taps while the request is in flight
            nameField.interactable = false;
            nameChangeStatus.text = "Changing name...";

            NameChangeResult result = await AuthManager.Instance.RequestDisplayNameChangeAsync(newName);

            nameField.interactable = true;
            nameSaveButton.interactable = true;
            switch (result) {
                case NameChangeResult.Success:
                nameChangeStatus.text = "Name changed!";
                    nameField.interactable = false;
                    nameSaveButton.interactable = false;
                AuthManager.Instance.CurrentProfile.DisplayName = newName;
                    break;

                case NameChangeResult.NameTaken:
                    nameChangeStatus.text = "That name is already taken.";
                    break;
                case NameChangeResult.OnCooldown:
                    nameChangeStatus.text = "You can only change your name once every 14 days.";
                    break;

                case NameChangeResult.NotSignedIn:
                    nameChangeStatus.text = "You're not signed in.";
                    break;

                case NameChangeResult.Error:
                default:
                    nameChangeStatus.text = "Something went wrong. Try again.";
                    break;
            }
        }
    }

    public void DownloadLatestVersion() {
        Application.OpenURL(VersionChecker.Instance.DownloadURL);
    }

    // ── Helpers ──────────────────────────────────────────────
    private void SetButtonColor(Button btn, bool isActive) {
        var colors = btn.colors;
        colors.normalColor = isActive ? activeColor : inactiveColor;
        btn.colors = colors;
    }
}