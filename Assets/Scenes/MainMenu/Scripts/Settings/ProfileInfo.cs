using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Minimal UI hookup for the Sign In button.
// Attach to a Canvas GameObject. Assign the button and label in the Inspector.
public class ProfileInfo : MonoBehaviour {
    [Tooltip("Stats")]
    [SerializeField] private GameObject guestWrapper;
    [SerializeField] private GameObject emailWrapper;
    [SerializeField] private TMP_Text email;
    [SerializeField] private TMP_Text displayName;
    [SerializeField] private TMP_Text coins;
    //[SerializeField] private TMP_Text role;
    [SerializeField] private TMP_Text xp;
    [SerializeField] private TMP_Text createdAt;

    [Header("Settings Panel")]
    [SerializeField] private Button signInBtn;
    [SerializeField] private Button logoutBtn;

    // ── Lifecycle ─────────────────────────────────────────────────────────

    void Start() {
        signInBtn.onClick.AddListener(OnAuthButtonClicked);
        logoutBtn.onClick.AddListener(OnAuthButtonClicked);

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
        bool signedIn = AuthManager.Instance.IsSignedIn;

        if (AuthManager.Instance.CurrentProfile != null) UpdateUserStatsUI(AuthManager.Instance.CurrentProfile);
        AuthManager.Instance.OnPlayerStatsLoaded += UpdateUserStatsUI;

        signInBtn.gameObject.SetActive(!signedIn);
        logoutBtn.gameObject.SetActive(signedIn);
    }

    private void UpdateUserStatsUI(PlayerProfile profile) {
        if (profile == null || emailWrapper == null || guestWrapper == null) return;

        emailWrapper.SetActive(AuthManager.Instance.IsSignedIn);
        guestWrapper.SetActive(!AuthManager.Instance.IsSignedIn);

        if (AuthManager.Instance.IsSignedIn) email.text = AuthManager.Instance.CurrentUser?.Email ?? "N/A";
        displayName.text = profile.DisplayName;
        //role.text = profile.Role.ToString();

        coins.text = profile.Coins.ToString();
        xp.text = profile.Xp.ToString();

        DateTime creationDate = profile.CreatedAt.ToDateTime();
        TimeSpan ageSpan = DateTime.UtcNow - creationDate;
        double ageInDays = (double)ageSpan.TotalDays;
        string dayText = ageInDays == 1 ? "day" : "days";
        string formattedCreationDate = creationDate.ToString("d"); // d for short date format, g for general date/time pattern (short time)

        createdAt.text = $"{formattedCreationDate} ({ageInDays:F2} {dayText})";
    }
}