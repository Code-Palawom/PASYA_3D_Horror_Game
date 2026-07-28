using Firebase.Auth;
using Org.BouncyCastle.Cms;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Minimal UI hookup for the Sign In button.
// Attach to a Canvas GameObject. Assign the button and label in the Inspector.
public class UIAuthButton : MonoBehaviour {
    [Header("References")]
    [Tooltip("The Sign In button.")]
    [SerializeField] private Button authButton;

    [SerializeField] private GameObject logoImage;

    [Tooltip("Label on the button (TextMeshPro).")]
    [SerializeField] private TMP_Text buttonLabel;

    [Tooltip("Optional: a separate label showing the signed-in user's name/email.")]
    [SerializeField] private TMP_Text userInfoLabel;

    [Tooltip("Panels to show/hide based on auth state.")]
    [SerializeField] private GameObject signedInPanel;

    [Tooltip("Stats")]
    [SerializeField] private TMP_Text email;
    [SerializeField] private TMP_Text displayName;
    [SerializeField] private TMP_Text role;
    [SerializeField] private TMP_Text xp;
    [SerializeField] private TMP_Text gamesPlayed;
    [SerializeField] private TMP_Text correctAnswers;
    [SerializeField] private TMP_Text incorrectAnswers;
    [SerializeField] private TMP_Text createdAt;

    [Header("Multiplayer Button")]
    [SerializeField] private GameObject hostModeWrapper;

    [Header("Settings Panel")]
    [SerializeField] private Button signInBtn;
    [SerializeField] private Button logoutBtn;

    // ── Lifecycle ─────────────────────────────────────────────────────────

    void Start() {
        authButton.onClick.AddListener(OnAuthButtonClicked);
        signInBtn.onClick.AddListener(OnAuthButtonClicked);
        logoutBtn.onClick.AddListener(OnAuthButtonClicked);

        signedInPanel.SetActive(false);

        AuthManager.Instance.OnPlayerStatsLoaded += OnPlayerStatsLoaded;

        // Reflect whatever state auth is already in (e.g. cached session)
        OnPlayerStatsLoaded(AuthManager.Instance.CurrentProfile);
    }

    void OnDestroy() {
        if (AuthManager.Instance != null)
            AuthManager.Instance.OnPlayerStatsLoaded -= OnPlayerStatsLoaded;
    }

    // ── Button click ──────────────────────────────────────────────────────

    private void OnAuthButtonClicked() {
        if (AuthManager.Instance.CurrentUser != null)
            AuthManager.Instance.SignOut();
        else
            AuthManager.Instance.SignIn();
    }

    // ── Auth state change ─────────────────────────────────────────────────

    private void OnPlayerStatsLoaded(PlayerProfile user) {
        bool signedIn = user != null;

        // Update button label
        if (buttonLabel != null) {
            buttonLabel.text = signedIn ? "Sign Out" : "Sign in with Google";
            logoImage.SetActive(signedIn);
            authButton.gameObject.SetActive(!signedIn);
        }

        // Update user info label
        if (userInfoLabel != null)
            userInfoLabel.text = signedIn
                //? $"{user.DisplayName}\n{user.Email}"
                ? user.DisplayName
                : string.Empty;

        // Toggle panels
        signedInPanel.SetActive(signedIn);
        if (signedIn) {
            if (AuthManager.Instance.CurrentProfile != null) UpdateUserStatsUI(AuthManager.Instance.CurrentProfile);
            AuthManager.Instance.OnPlayerStatsLoaded += UpdateUserStatsUI;
        }

        hostModeWrapper.SetActive(signedIn);

        signInBtn.gameObject.SetActive(!signedIn);
        logoutBtn.gameObject.SetActive(signedIn);
    }

    private void UpdateUserStatsUI(PlayerProfile profile) {
        if (profile == null) return;

        email.text = AuthManager.Instance.CurrentUser?.Email ?? "N/A";
        displayName.text = profile.DisplayName;
        role.text = profile.Role.ToString();
        xp.text = profile.Xp.ToString();
        gamesPlayed.text = profile.GamesPlayed.ToString();
        correctAnswers.text = profile.CorrectAnswers.ToString();
        incorrectAnswers.text = profile.IncorrectAnswers.ToString();

        DateTime creationDate = profile.CreatedAt.ToDateTime();
        TimeSpan ageSpan = DateTime.UtcNow - creationDate;
        double ageInDays = (double)ageSpan.TotalDays;
        string dayText = ageInDays == 1 ? "day" : "days";
        string formattedCreationDate = creationDate.ToString("d"); // d for short date format, g for general date/time pattern (short time)

        createdAt.text = $"{formattedCreationDate} ({ageInDays:F2} {dayText})";
    }
}