using Firebase;
using Firebase.Auth;
using Firebase.Firestore;
using Google;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// Central auth controller. Handles:
//   - Android: Google Sign-In via native plugin
//   - Desktop/Editor: Google Sign-In via PKCE browser redirect (DesktopGoogleAuth)
//   - Live player profile sync via Firestore snapshot listener (users/{uid})
// Add to your Bootstrap/persistent GameObject alongside DesktopGoogleAuth
// and UnityMainThreadDispatcher.
public class AuthManager : MonoBehaviour {
    public static AuthManager Instance { get; private set; }

    /// <summary>The currently signed-in Firebase user, or null.</summary>
    public FirebaseUser CurrentUser => _auth?.CurrentUser;

    /// <summary>Fires on main thread whenever auth state changes (sign-in or sign-out).</summary>
    public event Action<FirebaseUser> OnAuthStateChanged;

    /// <summary>Fires on main thread every time the player's Firestore profile document changes (initial load, level-up, answer counters, etc).</summary>
    public event Action<PlayerProfile> OnPlayerStatsLoaded;

    /// <summary>Latest known profile snapshot, cached for synchronous access (e.g. UI that inits after the event already fired).</summary>
    public PlayerProfile CurrentProfile { get; private set; }

    [Tooltip("Assign the DesktopGoogleAuth component on this same GameObject.")]
    [SerializeField] private DesktopGoogleAuth desktopGoogleAuth;

    private FirebaseAuth _auth;
    private bool _firebaseReady;

    private CancellationTokenSource cts;

    private ListenerRegistration _profileListener;
    private DocumentReference _profileDocRef;
    private bool _lastLoginBumpedThisSession;

    // ── Lifecycle ─────────────────────────────────────────────────────────

    void Awake() {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start() {
        if (ConfirmFirebaseServices.Instance.IsReady) InitAuth();
        else ConfirmFirebaseServices.Instance.OnFirebaseReady += InitAuth;
    }

    private void OnDestroy() {
        DetachPlayerProfileListener(); // safety net if the app quits/scene tears down without SignOut
    }

    private void InitAuth() {
        _auth = FirebaseAuth.DefaultInstance;
        _firebaseReady = true;
        if (_auth.CurrentUser != null) OnSignInSuccess(_auth.CurrentUser);
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

        DetachPlayerProfileListener();
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

        GoogleSignInLoading.Instance.Show("Signing in");

        GoogleSignIn.DefaultInstance.SignIn().ContinueWith(task =>
        {
            MainThreadDispatcher.Instance.Enqueue(() =>
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

        GoogleSignInLoading.Instance.Hide();
    }
#endif

    // ── Desktop/Editor: PKCE browser redirect ────────────────────────────

    private async void SignInWithGoogleDesktop() {
        cts = new CancellationTokenSource();

        try {
            GoogleSignInLoading.Instance.Show("Signing in", onCancel: () => cts.Cancel());

            var signInTask = desktopGoogleAuth.SignInAsync(cts.Token); // ← pass token
            var user = await signInTask;
            OnSignInSuccess(user);
        } catch (OperationCanceledException) {
            Debug.LogWarning("[AuthManager] Desktop sign-in cancelled.");
        } catch (Exception e) {
            Debug.LogError($"[AuthManager] Desktop sign-in error: {e.Message}");
        } finally {
            cts.Dispose();
            cts = null;
            GoogleSignInLoading.Instance.Hide();
        }
    }

    // Shared handlers

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
        AttachPlayerProfileListener(user);
    }

    // ── Player profile (Firestore: users/{uid}, live-listened) ──────────

    private void AttachPlayerProfileListener(FirebaseUser user) {
        DetachPlayerProfileListener(); // guard against double-attach on re-sign-in

        _profileDocRef = FirebaseFirestore.DefaultInstance.Collection("users").Document(user.UserId);
        _lastLoginBumpedThisSession = false;

        _profileListener = _profileDocRef.Listen(snapshot => {
            MainThreadDispatcher.Instance.Enqueue(() => HandleProfileSnapshot(snapshot, user));
        });
    }

    private async void HandleProfileSnapshot(DocumentSnapshot snapshot, FirebaseUser user) {
        // Skip optimistic local snapshots. Any field written with FieldValue.ServerTimestamp
        // comes back as null until the server resolves the actual value — ConvertTo<T>()
        // would throw trying to assign null into a non-nullable Timestamp field.
        if (snapshot.Metadata.HasPendingWrites) return;

        if (!snapshot.Exists) {
            // First-time sign-in: create the doc. The listener fires again automatically
            // once this write lands, so we don't invoke the event here.
            var defaultProfile = new PlayerProfile {
                DisplayName = user.DisplayName ?? "Player",
                Xp = 0,
                GamesPlayed = 0,
                HighScore = 0,
                CorrectAnswers = 0,
                IncorrectAnswers = 0,
                Role = PlayerRole.Player.ToString(),
                // Placeholders — immediately overwritten below with the authoritative
                // server clock, since Timestamp can't hold the ServerTimestamp sentinel directly.
                CreatedAt = Timestamp.GetCurrentTimestamp(),
                LastLoginAt = Timestamp.GetCurrentTimestamp()
            };

            try {
                await _profileDocRef.SetAsync(defaultProfile);

                // Overwrite with server-resolved timestamps so account age / login time
                // can't be spoofed via the device's local clock.
                await _profileDocRef.UpdateAsync(new Dictionary<string, object> {
                    { "CreatedAt", FieldValue.ServerTimestamp },
                    { "LastLoginAt", FieldValue.ServerTimestamp }
                });
            } catch (Exception e) {
                Debug.LogError($"[AuthManager] Failed to create default player profile: {e.Message}");
            }
            return;
        }

        var profile = snapshot.ConvertTo<PlayerProfile>();
        profile.Uid = user.UserId;
        CurrentProfile = profile;
        OnPlayerStatsLoaded?.Invoke(profile);

        // Returning user (doc already existed) — safe to bump LastLoginAt now.
        // Guarded to fire exactly once per session; without this, the write below
        // would re-trigger this same listener and loop forever.
        if (!_lastLoginBumpedThisSession) {
            _lastLoginBumpedThisSession = true;
            _ = _profileDocRef.UpdateAsync("LastLoginAt", FieldValue.ServerTimestamp)
                .ContinueWith(t => {
                    if (t.IsFaulted) Debug.LogWarning($"[AuthManager] Failed to update LastLoginAt: {t.Exception?.InnerException?.Message}");
                });
        }
    }

    private void DetachPlayerProfileListener() {
        _profileListener?.Stop();
        _profileListener = null;
        _profileDocRef = null;
        CurrentProfile = null;
        _lastLoginBumpedThisSession = false;
    }

    // Call when a question is answered correctly. Increments the counter server-side; the live listener will push the updated profile back via OnPlayerStatsLoaded.
    public Task RecordQuestionAnsweredCorrectlyAsync() => IncrementProfileFieldAsync("CorrectAnswers");

    // Call when a question is answered incorrectly. Increments the counter server-side; the live listener will push the updated profile back via OnPlayerStatsLoaded.
    public Task RecordQuestionAnsweredIncorrectlyAsync() => IncrementProfileFieldAsync("IncorrectAnswers");

    private async Task IncrementProfileFieldAsync(string fieldName) {
        if (_profileDocRef == null) {
            Debug.LogWarning($"[AuthManager] Tried to increment {fieldName} with no active profile doc (not signed in?).");
            return;
        }

        try {
            await _profileDocRef.UpdateAsync(new Dictionary<string, object> {
                { fieldName, FieldValue.Increment(1) }
            });
        } catch (Exception e) {
            Debug.LogError($"[AuthManager] Failed to increment {fieldName}: {e.Message}");
        }
    }
}