using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drop this on a UI Button (e.g. in your HUD or settings panel) to give players
/// a mic mute toggle. Swaps sprite/color on mute state change, stays in sync if
/// mute is toggled elsewhere (e.g. a keybind). Speaking-fade opacity is delegated
/// to a VoiceActivityIndicator on the icon itself (same fade logic used for other
/// players' name tags) - wire its DisplayName to the local player automatically
/// once Vivox login completes.
/// </summary>
[RequireComponent(typeof(Button))]
public class MuteButtonUI : MonoBehaviour {
    [Header("Icon Swap")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Sprite micOnSprite;
    [SerializeField] private Sprite micMutedSprite;

    [Header("Optional Color Tint")]
    [SerializeField] private bool tintWhenMuted = true;
    [SerializeField] private Color mutedColor = Color.red;
    [SerializeField] private Color unmutedColor = Color.white;

    [Header("Local Speaking Fade")]
    [Tooltip("VoiceActivityIndicator on the mic icon (needs a CanvasGroup). DisplayName is wired automatically once Vivox login completes.")]
    [SerializeField] private VoiceActivityIndicator localSpeakingIndicator;

    private Button _button;

    private void Awake() {
        _button = GetComponent<Button>();
    }

    private void OnEnable() {
        _button.onClick.AddListener(HandleClick);

        if (VivoxManager.Instance != null) {
            VivoxManager.Instance.OnLocalMuteChanged += HandleMuteStateChanged;
            VivoxManager.Instance.OnVivoxLoggedIn += HandleVivoxLoggedIn;

            RefreshVisual(VivoxManager.Instance.IsLocallyMuted);
            WireLocalSpeakingIndicator();
        }
    }

    private void OnDisable() {
        _button.onClick.RemoveListener(HandleClick);

        if (VivoxManager.Instance != null) {
            VivoxManager.Instance.OnLocalMuteChanged -= HandleMuteStateChanged;
            VivoxManager.Instance.OnVivoxLoggedIn -= HandleVivoxLoggedIn;
        }
    }

    private void HandleClick() {
        if (VivoxManager.Instance == null) {
            Debug.LogWarning("[MuteButtonUI] VivoxManager not available.");
            return;
        }

        VivoxManager.Instance.ToggleLocalMute();
        // RefreshVisual is also called via the OnLocalMuteChanged event, so no
        // need to duplicate it here - this keeps a single source of truth.
    }

    private void HandleMuteStateChanged(bool isMuted) {
        RefreshVisual(isMuted);
    }

    private void HandleVivoxLoggedIn() {
        WireLocalSpeakingIndicator();
    }

    // LocalDisplayName is only known after login completes, so this is called
    // both on enable (in case login already happened) and on the login event
    // (in case this button was enabled before Vivox finished logging in).
    private void WireLocalSpeakingIndicator() {
        if (localSpeakingIndicator == null || VivoxManager.Instance == null)
            return;

        if (string.IsNullOrEmpty(VivoxManager.Instance.LocalDisplayName))
            return;

        localSpeakingIndicator.DisplayName = VivoxManager.Instance.LocalDisplayName;
    }

    private void RefreshVisual(bool isMuted) {
        if (iconImage == null)
            return;

        if (micOnSprite != null && micMutedSprite != null)
            iconImage.sprite = isMuted ? micMutedSprite : micOnSprite;

        if (tintWhenMuted)
            iconImage.color = isMuted ? mutedColor : unmutedColor;
    }
}