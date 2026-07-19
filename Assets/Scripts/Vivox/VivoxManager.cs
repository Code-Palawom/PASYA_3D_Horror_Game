using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Vivox;
using UnityEngine;

// Singleton manager for Vivox voice chat. Handles init, login, positional channel
// join/leave, per-frame position updates for the local player, and mute/volume controls.
// Mirrors the FirebaseManager singleton pattern used elsewhere in the project.
public class VivoxManager : MonoBehaviour {
    public static VivoxManager Instance { get; private set; }

    [Header("Positional Channel Tuning")]
    [SerializeField] private int audibleDistance = 20;
    [SerializeField] private int conversationalDistance = 4;
    [SerializeField] private float audioFadeIntensityByDistance = 1.0f;
    [SerializeField] private AudioFadeModel audioFadeModel = AudioFadeModel.InverseByDistance;

    [Header("Position Update Rate")]
    [Tooltip("Seconds between Set3DPosition calls. 0 = every frame.")]
    [SerializeField] private float positionUpdateInterval = 0.1f;

    public bool IsInitialized { get; private set; }
    public bool IsLoggedIn { get; private set; }
    public string CurrentChannelName { get; private set; }
    public bool IsLocallyMuted { get; private set; }

    // The sanitized DisplayName actually sent to Vivox at login. Use this (not your
    // raw input string) when matching the local player against VoiceActivityIndicator,
    // since the sanitization step can alter the value.
    public string LocalDisplayName { get; private set; }

    public event Action OnVivoxLoggedIn;
    public event Action OnVivoxLoggedOut;
    public event Action<string> OnChannelJoined;
    public event Action<string> OnChannelLeft;
    public event Action<VivoxParticipant> OnParticipantJoinedChannel;
    public event Action<VivoxParticipant> OnParticipantLeftChannel;
    public event Action<bool> OnLocalMuteChanged;

    // Fired whenever a participant's speaking state or audio level changes.
    // Identifier is DisplayName (set to Firebase UID at login), NOT VivoxParticipant.PlayerId -
    // that internal ID is auto-derived from Unity Authentication and isn't something we control.
    // (firebaseUid, isSpeaking, audioEnergy 0-1). Fires for the local player too,
    // so you can drive a "you are speaking" indicator off the same event.
    public event Action<string, bool, double> OnParticipantSpeechChanged;

    private Transform _localPlayerTransform;
    private float _positionUpdateTimer;
    private readonly HashSet<string> _mutedParticipants = new HashSet<string>();
    private Task<bool> _loginTask;

    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update() {
        if (_localPlayerTransform == null || string.IsNullOrEmpty(CurrentChannelName))
            return;

        if (positionUpdateInterval <= 0f) {
            PushLocalPosition();
            return;
        }

        _positionUpdateTimer += Time.deltaTime;
        if (_positionUpdateTimer >= positionUpdateInterval) {
            _positionUpdateTimer = 0f;
            PushLocalPosition();
        }
    }

    private void PushLocalPosition() {
        try {
            VivoxService.Instance.Set3DPosition(
                _localPlayerTransform.gameObject,
                CurrentChannelName
            );
        } catch (Exception e) {
            Debug.LogWarning($"[VivoxManager] Failed to push position: {e.Message}");
        }
    }

    // Initializes UGS core services (if not already) and Vivox, then logs in anonymously.
    // Pass the player's Firebase UID as firebaseUid - it's stored as the Vivox DisplayName,
    // which is the identifier used elsewhere in this manager (mute lookups, speech events)
    // since Vivox's own internal PlayerId is derived from Unity Authentication, not Firebase.
    // Safe to call multiple times; no-ops if already logged in.
    public async Task<bool> InitializeAndLoginAsync(string username) {
        if (IsLoggedIn)
            return true;

        // If a login is already in flight (e.g. called from two places at once
        // on startup), share that result instead of firing a second LoginAsync -
        // Vivox throws "must be logged out" if you call it while mid-login.
        if (_loginTask != null && !_loginTask.IsCompleted)
            return await _loginTask;

        _loginTask = DoLoginAsync(username);
        return await _loginTask;
    }

    private async Task<bool> DoLoginAsync(string username) {
        try {
            if (!IsInitialized) {
                await VivoxService.Instance.InitializeAsync();
                IsInitialized = true;

                VivoxService.Instance.ParticipantAddedToChannel += HandleParticipantAdded;
                VivoxService.Instance.ParticipantRemovedFromChannel += HandleParticipantRemoved;
            }

            // Vivox's own session can already be logged in even if our cached
            // flag says otherwise - e.g. after an editor domain reload where the
            // native Vivox connection survives but this MonoBehaviour was
            // recreated. Calling LoginAsync again in that state throws.
            if (VivoxService.Instance.IsLoggedIn) {
                LocalDisplayName = SanitizeDisplayName(username);
                IsLoggedIn = true;
                Debug.Log("[VivoxManager] Logged in");
                OnVivoxLoggedIn?.Invoke();
                return true;
            }

            var loginOptions = new LoginOptions {
                DisplayName = SanitizeDisplayName(username),
                EnableTTS = false
            };

            await VivoxService.Instance.LoginAsync(loginOptions);
            LocalDisplayName = SanitizeDisplayName(username);
            IsLoggedIn = true;
            Debug.Log("[VivoxManager] Logged in");
            OnVivoxLoggedIn?.Invoke();
            return true;
        } catch (Exception e) {
            Debug.LogError($"[VivoxManager] Init/login failed: {e.Message}");
            return false;
        }
    }

    // Firebase UIDs are alphanumeric and safe as-is, but this strips anything Vivox
    // might choke on if you ever pass a different kind of name through here.
    private string SanitizeDisplayName(string name) {
        if (string.IsNullOrWhiteSpace(name))
            return "Player" + UnityEngine.Random.Range(1000, 9999);

        var chars = System.Text.RegularExpressions.Regex.Replace(name, @"[^a-zA-Z0-9_\-\.]", "");
        return chars.Length > 0 ? chars : "Player" + UnityEngine.Random.Range(1000, 9999);
    }

    // Joins a positional (3D) voice channel. Call this after NGO connection approval
    // succeeds, using a channel name derived from your session/lobby ID.
    public async Task<bool> JoinPositionalChannelAsync(string channelName) {
        if (!IsLoggedIn) {
            Debug.LogWarning("[VivoxManager] Cannot join channel before login.");
            return false;
        }

        if (CurrentChannelName == channelName)
            return true;

        if (!string.IsNullOrEmpty(CurrentChannelName))
            await LeaveCurrentChannelAsync();

        try {
            var channel3DProperties = new Channel3DProperties(
                audibleDistance,
                conversationalDistance,
                audioFadeIntensityByDistance,
                audioFadeModel
            );

            await VivoxService.Instance.JoinPositionalChannelAsync(
                channelName,
                ChatCapability.AudioOnly,
                channel3DProperties
            );

            CurrentChannelName = channelName;
            OnChannelJoined?.Invoke(channelName);
            return true;
        } catch (Exception e) {
            Debug.LogError($"[VivoxManager] Failed to join channel '{channelName}': {e.Message}");
            return false;
        }
    }

    // Joins a non-positional group channel (e.g. lobby-wide chat regardless of position).
    public async Task<bool> JoinGroupChannelAsync(string channelName) {
        if (!IsLoggedIn) {
            Debug.LogWarning("[VivoxManager] Cannot join channel before login.");
            return false;
        }

        if (CurrentChannelName == channelName)
            return true;

        if (!string.IsNullOrEmpty(CurrentChannelName))
            await LeaveCurrentChannelAsync();

        try {
            await VivoxService.Instance.JoinGroupChannelAsync(channelName, ChatCapability.AudioOnly);
            CurrentChannelName = channelName;
            OnChannelJoined?.Invoke(channelName);
            return true;
        } catch (Exception e) {
            Debug.LogError($"[VivoxManager] Failed to join group channel '{channelName}': {e.Message}");
            return false;
        }
    }

    public async Task LeaveCurrentChannelAsync() {
        if (string.IsNullOrEmpty(CurrentChannelName))
            return;

        var channelName = CurrentChannelName;

        try {
            await VivoxService.Instance.LeaveChannelAsync(channelName);
        } catch (Exception e) {
            Debug.LogWarning($"[VivoxManager] Error leaving channel '{channelName}': {e.Message}");
        } finally {
            CurrentChannelName = null;
            _localPlayerTransform = null;
            OnChannelLeft?.Invoke(channelName);
        }
    }

    public async Task LogoutAsync() {
        if (!IsLoggedIn)
            return;

        await LeaveCurrentChannelAsync();

        try {
            await VivoxService.Instance.LogoutAsync();
        } catch (Exception e) {
            Debug.LogWarning($"[VivoxManager] Error during logout: {e.Message}");
        } finally {
            IsLoggedIn = false;
            _loginTask = null;
            OnVivoxLoggedOut?.Invoke();
        }
    }

    // Registers the local player's transform to be fed into Vivox each frame for
    // positional audio. Call from the owning client's player OnNetworkSpawn.
    public void RegisterLocalPlayerTransform(Transform playerTransform) {
        _localPlayerTransform = playerTransform;
        _positionUpdateTimer = 0f;
    }

    public void UnregisterLocalPlayerTransform(Transform playerTransform) {
        if (_localPlayerTransform == playerTransform)
            _localPlayerTransform = null;
    }

    // --- Mute / Volume Controls ---

    public void SetLocalMute(bool muted) {
        try {
            if (muted)
                VivoxService.Instance.MuteInputDevice();
            else
                VivoxService.Instance.UnmuteInputDevice();

            IsLocallyMuted = muted;
            OnLocalMuteChanged?.Invoke(muted);
        } catch (Exception e) {
            Debug.LogWarning($"[VivoxManager] Failed to set local mute: {e.Message}");
        }
    }

    // Convenience toggle for a mute button UI. Returns the new mute state.
    public bool ToggleLocalMute() {
        SetLocalMute(!IsLocallyMuted);
        return IsLocallyMuted;
    }

    // Sets the local user's output (listening) volume. Range is -50 to 50, 0 = default.
    public void SetMasterVolume(int volume) {
        volume = Mathf.Clamp(volume, -50, 50);
        try {
            VivoxService.Instance.SetOutputDeviceVolume(volume);
        } catch (Exception e) {
            Debug.LogWarning($"[VivoxManager] Failed to set master volume: {e.Message}");
        }
    }

    public void SetParticipantMuted(string username, bool muted) {
        if (string.IsNullOrEmpty(CurrentChannelName))
            return;

        if (!VivoxService.Instance.ActiveChannels.TryGetValue(CurrentChannelName, out var participants))
            return;

        foreach (var participant in participants) {
            if (participant.DisplayName != username)
                continue;

            if (muted) {
                participant.MutePlayerLocally();
                _mutedParticipants.Add(username);
            } else {
                participant.UnmutePlayerLocally();
                _mutedParticipants.Remove(username);
            }
            break;
        }
    }

    public bool IsParticipantMuted(string username) => _mutedParticipants.Contains(username);

    // --- Event Handlers ---

    private void HandleParticipantAdded(VivoxParticipant participant) {
        participant.ParticipantSpeechDetected += () => HandleParticipantSpeechOrEnergyChanged(participant);
        participant.ParticipantAudioEnergyChanged += () => HandleParticipantSpeechOrEnergyChanged(participant);
        OnParticipantJoinedChannel?.Invoke(participant);
    }

    private void HandleParticipantRemoved(VivoxParticipant participant) {
        // Note: Vivox destroys the VivoxParticipant instance on removal, so these
        // handlers are released along with it - no explicit -= needed here, but we
        // still need to clean up our own tracking state.
        _mutedParticipants.Remove(participant.DisplayName);
        OnParticipantLeftChannel?.Invoke(participant);
    }

    // Fired by either ParticipantSpeechDetected or ParticipantAudioEnergyChanged;
    // both events are parameterless, so current state is read directly off the participant.
    private void HandleParticipantSpeechOrEnergyChanged(VivoxParticipant participant) {
        OnParticipantSpeechChanged?.Invoke(
            participant.DisplayName,
            participant.SpeechDetected,
            participant.AudioEnergy);
    }

    private async void OnApplicationQuit() {
        if (IsLoggedIn)
            await LogoutAsync();
    }
}