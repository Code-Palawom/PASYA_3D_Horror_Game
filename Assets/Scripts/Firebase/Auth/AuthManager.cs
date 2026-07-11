using Firebase;
using Firebase.Auth;
using Firebase.Firestore;
using Google;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Sockets;
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

    // The currently signed-in Firebase user, or null.
    public FirebaseUser CurrentUser => _auth?.CurrentUser;

    // Fires on main thread whenever auth state changes (sign-in or sign-out).
    public event Action<FirebaseUser> OnAuthStateChanged;

    // Fires on main thread every time the player's Firestore profile document changes (initial load, level-up, answer counters, etc).
    public event Action<PlayerProfile> OnPlayerStatsLoaded;

    // Latest known profile snapshot, cached for synchronous access (e.g. UI that inits after the event already fired).
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

    // Starts the sign-in flow. Uses Google (Android) or PKCE browser redirect (Desktop/Editor).
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

    // Signs out of both Firebase and Google (Android only).
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
                // User dismissed the Google account picker
                if (task.IsCanceled)
                {
                    Debug.LogWarning("[AuthManager] Android Google Sign-In cancelled.");
                    GoogleSignInLoading.Instance.Hide();
                    return;
                }

                if (task.IsFaulted)
                {
                    Debug.LogError("[AuthManager] Android Google Sign-In failed: " + task.Exception);
                    GoogleSignInLoading.Instance.ShowError(GetAndroidErrorMessage(task.Exception));
                    return;
                }

                // Google sign-in succeeded — now authenticate with Firebase
                var credential = GoogleAuthProvider.GetCredential(task.Result.IdToken, null);
                _auth.SignInWithCredentialAsync(credential).ContinueWith(HandleFirebaseResult);
            });
        });
    }

    private static string GetAndroidErrorMessage(AggregateException ex) {
        string msg = ex?.InnerException?.Message ?? ex?.Message ?? "";

        // Google Play Services not available or outdated
        if (msg.Contains("ApiException") || msg.Contains("SIGN_IN_FAILED"))
            return "Google Sign-In failed.\nCheck Play Services.";

        // Network-related
        if (msg.Contains("network") || msg.Contains("connect") || msg.Contains("timeout")
            || ex?.InnerException is HttpRequestException
            || ex?.InnerException is SocketException)
            return "No internet connection.";

        return "Sign-in failed. Try again.";
    }
#endif

    // ── Desktop/Editor: PKCE browser redirect ────────────────────────────

    private async void SignInWithGoogleDesktop() {
        cts = new CancellationTokenSource();

        try {
            GoogleSignInLoading.Instance.Show("Signing in", onCancel: () => cts.Cancel());

            var user = await desktopGoogleAuth.SignInAsync(cts.Token);

            // Success — hide immediately before proceeding
            GoogleSignInLoading.Instance.Hide();
            OnSignInSuccess(user);

        } catch (OperationCanceledException) {
            Debug.LogWarning("[AuthManager] Desktop sign-in cancelled.");
            GoogleSignInLoading.Instance.Hide();   // immediate, no error message

        } catch (Exception e) {
            Debug.LogError($"[AuthManager] Desktop sign-in error: {e.Message}");
            GoogleSignInLoading.Instance.ShowError(GetDesktopErrorMessage(e)); // auto-hides after 3s

        } finally {
            cts?.Dispose();
            cts = null;
            // NOTE: Hide() is NOT called here — each branch handles its own dismiss
            // so ShowError() has time to display before hiding.
        }
    }

    private static string GetDesktopErrorMessage(Exception e) {
        bool isNetwork = e is HttpRequestException
                      || e.InnerException is SocketException
                      || e.Message.Contains("network")
                      || e.Message.Contains("connect")
                      || e.Message.Contains("timeout");

        return isNetwork ? "No internet connection." : "Sign-in failed. Try again.";
    }

    // ── Shared handlers ───────────────────────────────────────────────────

    // Called after Android Firebase credential sign-in completes.
    // Handles both the overlay dismiss and error display.
    private void HandleFirebaseResult(Task<FirebaseUser> task) {
        MainThreadDispatcher.Instance.Enqueue(() => {
            if (task.IsCanceled) {
                Debug.LogWarning("[AuthManager] Firebase sign-in cancelled.");
                GoogleSignInLoading.Instance.Hide();
                return;
            }

            if (task.IsFaulted) {
                Debug.LogError("[AuthManager] Firebase sign-in failed: " + task.Exception);

                string msg = task.Exception?.InnerException is HttpRequestException
                          || task.Exception?.InnerException is SocketException
                    ? "No internet connection."
                    : "Sign-in failed. Try again.";

                GoogleSignInLoading.Instance.ShowError(msg); // auto-hides
                return;
            }

            // All good — hide overlay then proceed
            GoogleSignInLoading.Instance.Hide();
            OnSignInSuccess(task.Result);
        });
    }

    private void OnSignInSuccess(FirebaseUser user) {
        Debug.Log($"[AuthManager] Signed in → {user.DisplayName} | UID: {user.UserId}");
        ToastNotification.Instance.ShowLocalToast($"Welcome {user.DisplayName}", ToastType.Info);
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
            // First-time sign-in: create the doc AND claim the username atomically,
            // so two devices racing to create the same account name can't collide,
            // and so this default name is properly reserved in usernames/{name}.
            string defaultName = user.DisplayName ?? "Player";
            string nameKey = defaultName.Trim().ToLowerInvariant();

            var db = FirebaseFirestore.DefaultInstance;
            DocumentReference usernameRef = db.Collection("usernames").Document(nameKey);

            // ── Step 1: claim a name (its own transaction, so it fully commits
            // before we create the profile — get() in rules can't see writes
            // from earlier in the SAME transaction). ─────────────────────────
            string finalName = defaultName;
            try {
                await db.RunTransactionAsync(async transaction => {
                    DocumentSnapshot existing = await transaction.GetSnapshotAsync(usernameRef);

                    if (existing.Exists && existing.GetValue<string>("uid") != user.UserId) {
                        // Collision — fall back to a name guaranteed unique, since
                        // it's derived from the uid (already the users/{uid} doc ID).
                        finalName = $"{defaultName}{user.UserId.Substring(0, 6)}";
                        string finalKey = finalName.ToLowerInvariant();
                        usernameRef = db.Collection("usernames").Document(finalKey);
                    }

                    transaction.Set(usernameRef, new Dictionary<string, object> { { "uid", user.UserId } });
                });
            } catch (Exception e) {
                Debug.LogError($"[AuthManager] Failed to claim initial display name: {e.Message}");
                return;
            }

            // ── Step 2: create the profile now that the claim is visible to rules. ──
            var defaultProfile = new PlayerProfile {
                DisplayName = finalName,
                Xp = 0,
                GamesPlayed = 0,
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

                await _profileDocRef.UpdateAsync(new Dictionary<string, object> {
                { "CreatedAt", FieldValue.ServerTimestamp },
                { "LastLoginAt", FieldValue.ServerTimestamp }
            });
            } catch (Exception e) {
                Debug.LogError($"[AuthManager] Failed to create default player profile: {e.Message}");
                // Compensating action: release the name claim so it isn't orphaned.
                try { await usernameRef.DeleteAsync(); } catch (Exception cleanupEx) { Debug.LogError($"[AuthManager] Failed to roll back username claim: {cleanupEx.Message}"); }
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

    // Call when the player wants to change their display name.
    // Enforces a 14-day cooldown client-side for immediate UX feedback — the real
    // enforcement lives in Firestore Security Rules, since this check alone could
    // be bypassed by a modified client.
    // Uniqueness is enforced via a usernames/{displayNameLower} doc, claimed
    // atomically in the same transaction as the rename so two players racing
    // for the same name can't both succeed.
    // <returns>Result indicating success or the specific failure reason.</returns>
    public async Task<NameChangeResult> RequestDisplayNameChangeAsync(string newDisplayName) {
        if (_profileDocRef == null || CurrentProfile == null) {
            Debug.LogWarning("[AuthManager] Tried to change display name with no active profile (not signed in?).");
            return NameChangeResult.NotSignedIn;
        }

        if (CurrentProfile.LastNameChange is Timestamp lastChange) {
            var elapsed = DateTime.UtcNow - lastChange.ToDateTime();
            if (elapsed.TotalDays < 14) {
                double daysLeft = 14 - elapsed.TotalDays;
                Debug.LogWarning($"[AuthManager] Display name change blocked — {daysLeft:F1} day(s) remaining on cooldown.");
                return NameChangeResult.OnCooldown;
            }
        }

        string uid = CurrentUser.UserId;
        string newNameKey = newDisplayName.Trim().ToLowerInvariant();
        string oldNameKey = CurrentProfile.DisplayName?.Trim().ToLowerInvariant();

        var db = FirebaseFirestore.DefaultInstance;
        DocumentReference newUsernameRef = db.Collection("usernames").Document(newNameKey);

        // ── Step 1: claim the new name (its own transaction, so it fully commits
        // before we touch the profile — required because rules' get() calls can't
        // see writes from earlier in the SAME transaction). ──────────────────
        try {
            await db.RunTransactionAsync(async transaction => {
                DocumentSnapshot existing = await transaction.GetSnapshotAsync(newUsernameRef);
                if (existing.Exists) {
                    string ownerUid = existing.GetValue<string>("uid");
                    if (ownerUid != uid) throw new NameTakenException();
                    return; // already ours (e.g. retry after a partial prior failure)
                }
                transaction.Set(newUsernameRef, new Dictionary<string, object> { { "uid", uid } });
            });
        } catch (NameTakenException) {
            Debug.LogWarning($"[AuthManager] Display name '{newDisplayName}' is already taken.");
            return NameChangeResult.NameTaken;
        } catch (Exception e) {
            Debug.LogError($"[AuthManager] Failed to claim new display name: {e.Message}");
            return NameChangeResult.Error;
        }

        // ── Step 2: update the profile now that the claim is visible to rules. ──
        try {
            await _profileDocRef.UpdateAsync(new Dictionary<string, object> {
            { "DisplayName", newDisplayName },
            { "LastNameChange", FieldValue.ServerTimestamp }
        });
        } catch (Exception e) {
            Debug.LogError($"[AuthManager] Failed to update profile after claiming name: {e.Message}");
            // Compensating action: release the name we just claimed so it doesn't
            // get orphaned (reserved forever with nobody actually using it).
            try { await newUsernameRef.DeleteAsync(); } catch (Exception cleanupEx) { Debug.LogError($"[AuthManager] Failed to roll back username claim: {cleanupEx.Message}"); }
            return NameChangeResult.Error;
        }

        // ── Step 3: release the old name, now that the rename succeeded. ────
        if (!string.IsNullOrEmpty(oldNameKey) && oldNameKey != newNameKey) {
            try {
                await db.Collection("usernames").Document(oldNameKey).DeleteAsync();
            } catch (Exception e) {
                // Non-fatal — the rename already succeeded. Worst case the old
                // name stays reserved and needs a cleanup pass later.
                Debug.LogWarning($"[AuthManager] Failed to release old display name '{oldNameKey}': {e.Message}");
            }
        }

        return NameChangeResult.Success;
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

public enum NameChangeResult {
    Success,
    NotSignedIn,
    OnCooldown,
    NameTaken,
    Error
}

// Thrown inside the transaction to signal a name collision without
// letting Firestore's SDK retry logic mistake it for a normal fault.
internal class NameTakenException : Exception { }