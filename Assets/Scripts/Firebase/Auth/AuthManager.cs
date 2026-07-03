using System;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using Google;
using UnityEngine;

// Central auth controller. Handles:
//   - Android: Google Sign-In via native plugin
//   - Desktop/Editor: Google Sign-In via PKCE browser redirect (DesktopGoogleAuth)
// Add to your Bootstrap/persistent GameObject alongside DesktopGoogleAuth
// and UnityMainThreadDispatcher.
public class AuthManager : MonoBehaviour {
    public static AuthManager Instance { get; private set; }

    /// <summary>The currently signed-in Firebase user, or null.</summary>
    public FirebaseUser CurrentUser => _auth?.CurrentUser;

    /// <summary>Fires on main thread whenever auth state changes (sign-in or sign-out).</summary>
    public event Action<FirebaseUser> OnAuthStateChanged;

    [Tooltip("Assign the DesktopGoogleAuth component on this same GameObject.")]
    [SerializeField] private DesktopGoogleAuth desktopGoogleAuth;

    private FirebaseAuth _auth;
    private bool _firebaseReady;

    // ── Lifecycle ─────────────────────────────────────────────────────────

    void Awake() {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    async void Start() {
        var status = await FirebaseApp.CheckAndFixDependenciesAsync();
        if (status == DependencyStatus.Available) {
            _auth = FirebaseAuth.DefaultInstance;
            _firebaseReady = true;
            Debug.Log("[AuthManager] Firebase ready.");

            // Restore cached session if available
            if (_auth.CurrentUser != null) {
                Debug.Log($"[AuthManager] Restoring session for {_auth.CurrentUser.DisplayName}");
                OnSignInSuccess(_auth.CurrentUser);
            }
        } else {
            Debug.LogError($"[AuthManager] Firebase dependency error: {status}");
        }
    }

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>
    /// Starts the sign-in flow. Uses Google (Android) or PKCE browser redirect (Desktop/Editor).
    /// </summary>
    public void SignIn() {
        if (!_firebaseReady) {
            Debug.LogWarning("[AuthManager] Firebase not ready yet.");
            return;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        SignInWithGoogleAndroid();
#else
        SignInWithGoogleDesktop();
#endif
    }

    /// <summary>Signs out of both Firebase and Google (Android only).</summary>
    public void SignOut() {
        if (_auth == null) return;
        _auth.SignOut();

#if UNITY_ANDROID && !UNITY_EDITOR
        GoogleSignIn.DefaultInstance.SignOut();
#endif

        Debug.Log("[AuthManager] Signed out.");
        OnAuthStateChanged?.Invoke(null);
    }

    // ── Android: native Google Sign-In ────────────────────────────────────

#if UNITY_ANDROID && !UNITY_EDITOR
    private void SignInWithGoogleAndroid()
    {
        GoogleSignIn.Configuration = new GoogleSignInConfiguration
        {
            WebClientId    = SecretStore.GoogleClientId,
            RequestIdToken = true,
            RequestEmail   = true
        };

        GoogleSignIn.DefaultInstance.SignIn().ContinueWith(task =>
        {
            UnityMainThreadDispatcher.Instance.Enqueue(() =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogError("[AuthManager] Android Google Sign-In failed: " + task.Exception);
                    return;
                }

                var credential = GoogleAuthProvider.GetCredential(task.Result.IdToken, null);
                _auth.SignInWithCredentialAsync(credential).ContinueWith(HandleFirebaseResult);
            });
        });
    }
#endif

    // ── Desktop/Editor: PKCE browser redirect ────────────────────────────

    private async void SignInWithGoogleDesktop() {
        try {
            var user = await desktopGoogleAuth.SignInAsync();
            OnSignInSuccess(user);
        } catch (OperationCanceledException) {
            Debug.LogWarning("[AuthManager] Desktop sign-in cancelled.");
        } catch (Exception e) {
            Debug.LogError($"[AuthManager] Desktop sign-in error: {e.Message}");
        }
    }

    // ── Shared handlers ───────────────────────────────────────────────────

    private void HandleFirebaseResult(Task<FirebaseUser> task) {
        MainThreadDispatcher.Instance.Enqueue(() => {
            if (task.IsFaulted || task.IsCanceled) {
                Debug.LogError("[AuthManager] Firebase sign-in failed: " + task.Exception);
                return;
            }
            OnSignInSuccess(task.Result);
        });
    }

    private void OnSignInSuccess(FirebaseUser user) {
        Debug.Log($"[AuthManager] Signed in → {user.DisplayName} | UID: {user.UserId}");
        OnAuthStateChanged?.Invoke(user);
    }
}