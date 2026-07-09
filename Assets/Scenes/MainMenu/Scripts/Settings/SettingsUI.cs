using Firebase.Firestore;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SettingsUI : MonoBehaviour {
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

    // ── Init ────────────────────────────────────────────────
    void Awake() {
        _qualityNames = QualitySettings.names;
    }

    void Start() {
        // Quality carousel
        btnQualityPrev.onClick.AddListener(() => StepQuality(-1));
        btnQualityNext.onClick.AddListener(() => StepQuality(+1));

        // POV toggle
        btnFirstPerson.onClick.AddListener(() => SetPOV(true));
        btnThirdPerson.onClick.AddListener(() => SetPOV(false));

        nameTagToggle.onValueChanged.AddListener((isOn) => SetNameTagVisibility(isOn));
        nameSaveButton.onClick.AddListener(() => ChangeName());

        AuthManager.Instance.OnPlayerStatsLoaded += RefreshName;
        AuthManager.Instance.OnAuthStateChanged += (user) => {
            if (user == null) {
                nameField.text = "";
                nameChangeStatus.text = "Sign in to change your name.";
                nameChangeStatus.color = cannotChangeNameColor;
                nameSaveButton.interactable = false;
                nameField.interactable = false;
            }
        };

        if (SettingsManager.Instance != null)
            Populate(SettingsManager.Instance.Current);
    }

    // ── Called by your panel manager ─────────────────────────
    public void Show() {
        gameObject.SetActive(true);
        Refresh();
    }

    public void Hide() => gameObject.SetActive(false);

    public void Refresh() {
        if (SettingsManager.Instance != null)
            Populate(SettingsManager.Instance.Current);
    }

    // ── Quality carousel ─────────────────────────────────────
    private void StepQuality(int direction) {
        // Infinite wrap
        _qualityIndex = (_qualityIndex + direction + _qualityNames.Length) % _qualityNames.Length;
        qualityLabel.text = _qualityNames[_qualityIndex];
        AutoSave();
    }

    // ── POV toggle ───────────────────────────────────────────
    private void SetPOV(bool firstPerson) {
        _isFirstPerson = firstPerson;
        SetButtonColor(btnFirstPerson, firstPerson);
        SetButtonColor(btnThirdPerson, !firstPerson);
        AutoSave();
    }

    private void SetNameTagVisibility(bool show) {
        _showNameTags = show;
        AutoSave();
    }

    // ── Auto-save ────────────────────────────────────────────
    private void AutoSave() {
        if (SettingsManager.Instance == null) return;

        SettingsManager.Instance.Save(new GameSettings {
            playerName = nameField.text.Trim(),
            isFirstPerson = _isFirstPerson,
            qualityLevel = _qualityIndex,
            showNameTags = _showNameTags
        });
    }

    // ── Populate from loaded settings ────────────────────────
    private void Populate(GameSettings s) {
        if(AuthManager.Instance.CurrentProfile != null) {
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
        }

        _qualityIndex = Mathf.Clamp(s.qualityLevel, 0, _qualityNames.Length - 1);
        qualityLabel.text = _qualityNames[_qualityIndex];

        // Apply POV without triggering AutoSave during populate
        _isFirstPerson = s.isFirstPerson;
        SetButtonColor(btnFirstPerson, s.isFirstPerson);
        SetButtonColor(btnThirdPerson, !s.isFirstPerson);

        // Name tag toggle
        nameTagToggle.SetIsOnWithoutNotify(s.showNameTags);
    }

    private void RefreshName(PlayerProfile profile) {
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
    }

    private async void ChangeName() {
        string newName = nameField.text.Trim();

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

    // ── Helpers ──────────────────────────────────────────────
    private void SetButtonColor(Button btn, bool isActive) {
        var colors = btn.colors;
        colors.normalColor = isActive ? activeColor : inactiveColor;
        btn.colors = colors;
    }
}