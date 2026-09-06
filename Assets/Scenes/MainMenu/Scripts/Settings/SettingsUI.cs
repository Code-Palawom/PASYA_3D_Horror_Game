using Firebase.Firestore;
using PrimeTween;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SettingsUI : MonoBehaviour {
    [SerializeField] private MainMenuUI mainMenuUI;

    [Header("Name")]
    [SerializeField] private CanvasGroup nameChangePanel;
    [SerializeField] private Button openNameChangePanel;
    [SerializeField] private Button closeNameChangePanel;
    [SerializeField] private TMP_InputField nameField;
    [SerializeField] private TMP_Text nameChangeStatus;
    [SerializeField] private Color canCangeNameColor;
    [SerializeField] private Color cannotChangeNameColor;
    [SerializeField] private Button nameSaveButton;

    [SerializeField] private Button playTutorial;
    [SerializeField] private Button customizeControls;
    [SerializeField] private GameObject customizeControlsPanel;

    private RectTransform _nameChangePanelRect;
    private Tween _scaleTween;
    private Tween _fadeTween;

    void Start() {
        customizeControls.onClick.AddListener(() => customizeControlsPanel.SetActive(true));
        playTutorial.onClick.AddListener(() => mainMenuUI.StartTutorial());
        nameSaveButton.onClick.AddListener(() => ChangeName());
        openNameChangePanel.onClick.AddListener(() => AnimateNameChangePanel(true));
        closeNameChangePanel.onClick.AddListener(() => AnimateNameChangePanel(false));

        _nameChangePanelRect = nameChangePanel.GetComponent<RectTransform>();
        _nameChangePanelRect.localScale = Vector3.zero;

        AuthManager.Instance.OnPlayerStatsLoaded += RefreshName;
        AuthManager.Instance.OnAuthStateChanged += (user) => {
            if (user == null) {
                nameField.text = SettingsManager.Instance.Current.playerName;
                nameChangeStatus.text = "";
                nameChangeStatus.color = canCangeNameColor;
                nameSaveButton.interactable = true;
                openNameChangePanel.interactable = true;
                nameField.interactable = true;
            }
        };

        if (AuthManager.Instance.CurrentProfile != null)
            RefreshName(AuthManager.Instance.CurrentProfile);

        if (SettingsManager.Instance != null)
            Populate(SettingsManager.Instance.Current);
    }

    void OnEnable() {
        if (SettingsManager.Instance != null)
            Populate(SettingsManager.Instance.Current);
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
                    openNameChangePanel.interactable = true;
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
                openNameChangePanel.interactable = true;
                nameField.interactable = true;
            }
        } else {
            nameField.text = SettingsManager.Instance.Current.playerName;
            nameChangeStatus.text = "";
            nameChangeStatus.color = canCangeNameColor;
            nameSaveButton.interactable = true;
            openNameChangePanel.interactable = true;
            nameField.interactable = true;
        }
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
                    openNameChangePanel.interactable = true;
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
                openNameChangePanel.interactable = true;
                nameField.interactable = true;
            }
        } else {
            nameField.text = SettingsManager.Instance.Current.playerName;
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

        AnimateNameChangePanel(false);
        nameSaveButton.interactable = false; // prevent double-taps while the request is in flight
        openNameChangePanel.interactable = false;
        nameField.interactable = false;
        nameChangeStatus.text = "Changing name...";

        NameChangeResult result = await AuthManager.Instance.RequestDisplayNameChangeAsync(newName);

        nameField.interactable = true;
        nameSaveButton.interactable = true;
        openNameChangePanel.interactable = true;
        switch (result) {
            case NameChangeResult.Success:
                nameChangeStatus.text = "Name changed!";

                AuthManager.Instance.CurrentProfile.DisplayName = newName;
                if (AuthManager.Instance.IsSignedIn) {
                    nameField.interactable = false;
                    nameSaveButton.interactable = false;
                    openNameChangePanel.interactable = false;
                } else {
                    SettingsManager.Instance.Save(s => s.playerName = newName);
                }
                break;

            case NameChangeResult.NameTaken:
                nameChangeStatus.text = "That name is already taken.";
                break;
            case NameChangeResult.OnCooldown:
                nameField.interactable = false;
                nameSaveButton.interactable = false;
                openNameChangePanel.interactable = false;
                nameChangeStatus.text = "You can only change your name once every 14 days.";
                break;

            case NameChangeResult.NotSignedIn:
                nameChangeStatus.text = "You're not signed in.";
                break;

            case NameChangeResult.Error:
            default:
                nameChangeStatus.text = "Something went wrong. Try again.";
                break;

            case NameChangeResult.Offline:
                nameChangeStatus.text = "You're offline. Try again when you have an internet connection.";
                break;
        }
    }

    private void AnimateNameChangePanel(bool open) {
        if (_scaleTween.isAlive) _scaleTween.Stop();
        if (_fadeTween.isAlive) _fadeTween.Stop();

        // Block interaction immediately either way — closing shouldn't be
        // clickable mid-shrink, and opening only becomes interactable once
        // the pop settles (below).
        nameChangePanel.interactable = false;

        if (open) {
            nameChangePanel.blocksRaycasts = true; // let it start catching input right away as it grows in
            _scaleTween = Tween.Scale(_nameChangePanelRect, endValue: Vector3.one, duration: 0.5f, ease: Ease.OutBack)
                .OnComplete(() => nameChangePanel.interactable = true);
            _fadeTween = Tween.Alpha(nameChangePanel, endValue: 1f, duration: 0.5f * 0.6f);
        } else {
            _scaleTween = Tween.Scale(_nameChangePanelRect, endValue: Vector3.zero, duration: 0.5f, ease: Ease.OutBack)
                .OnComplete(() => nameChangePanel.blocksRaycasts = false);
            _fadeTween = Tween.Alpha(nameChangePanel, endValue: 0f, duration: 0.5f * 0.8f);
        }
    }
}