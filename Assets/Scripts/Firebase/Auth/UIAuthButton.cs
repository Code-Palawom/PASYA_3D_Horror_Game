using Firebase.Auth;
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

    [Tooltip("Optional: panels to show/hide based on auth state.")]
    [SerializeField] private GameObject signedInPanel;
    [SerializeField] private GameObject signedOutPanel;

    [Header("Settings Panel")]
    [SerializeField] private Button signInBtn;
    [SerializeField] private Button logoutBtn;

    // ── Lifecycle ─────────────────────────────────────────────────────────

    void Start() {
        authButton.onClick.AddListener(OnAuthButtonClicked);
        signInBtn.onClick.AddListener(OnAuthButtonClicked);
        logoutBtn.onClick.AddListener(OnAuthButtonClicked);

        AuthManager.Instance.OnAuthStateChanged += OnAuthStateChanged;

        // Reflect whatever state auth is already in (e.g. cached session)
        OnAuthStateChanged(AuthManager.Instance.CurrentUser);
    }

    void OnDestroy() {
        if (AuthManager.Instance != null)
            AuthManager.Instance.OnAuthStateChanged -= OnAuthStateChanged;
    }

    // ── Button click ──────────────────────────────────────────────────────

    private void OnAuthButtonClicked() {
        if (AuthManager.Instance.CurrentUser != null)
            AuthManager.Instance.SignOut();
        else
            AuthManager.Instance.SignIn();
    }

    // ── Auth state change ─────────────────────────────────────────────────

    private void OnAuthStateChanged(FirebaseUser user) {
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
        if (signedInPanel != null) signedInPanel.SetActive(signedIn);
        if (signedOutPanel != null) signedOutPanel.SetActive(!signedIn);

        signInBtn.gameObject.SetActive(!signedIn);
        logoutBtn.gameObject.SetActive(signedIn);

    }
}