using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// A single control that both displays the local player's live mic intensity
// (via a non-interactive Slider fill) and toggles mute on click. Also swaps
// a mic icon between on/muted states and plays a mute/unmute SFX.
[RequireComponent(typeof(Slider))]
public class MuteButtonUI : MonoBehaviour, IPointerClickHandler {
    [Header("Intensity Slider (fill = live mic energy)")]
    [Tooltip("Auto-assigned from this GameObject if left empty. Kept non-interactive - click toggles mute, drag does nothing.")]
    [SerializeField] private Slider intensitySlider;
    [SerializeField] private Image fillImage;
    [SerializeField] private Color unmutedFillColor = Color.green;
    [SerializeField] private Color mutedFillColor = Color.red;
    [Tooltip("Higher = faster the fill catches up to actual mic energy. 0 = snap instantly.")]
    [SerializeField] private float fillSmoothing = 12f;

    [Header("Icon (mute state)")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Sprite micOnSprite;
    [SerializeField] private Sprite micMutedSprite;

    [Header("Icon Idle Opacity")]
    [Tooltip("Icon alpha while actively speaking, or muted (muted always shows at full opacity).")]
    [SerializeField, Range(0f, 1f)] private float iconSpeakingAlpha = 1f;
    [Tooltip("Icon alpha while silent/idle.")]
    [SerializeField, Range(0f, 1f)] private float iconIdleAlpha = 0.4f;
    [Tooltip("Seconds for a full 0->1 (or 1->0) icon fade. 0 = instant.")]
    [SerializeField] private float iconFadeDuration = 0.15f;

    [Header("Mute/Unmute SFX")]
    [Tooltip("AudioSource used to play the mute/unmute clips. Auto-added on this GameObject if left empty.")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip muteSound;
    [SerializeField] private AudioClip unmuteSound;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

    [Header("Intensity Gate")]
    [Tooltip("Ignore tiny energy fluctuations below this so the fill doesn't flicker.")]
    [SerializeField, Range(0f, 1f)] private float minEnergyThreshold = 0.05f;

    [SerializeField] private InputAction action;

    private float _targetFill;
    private float _targetIconAlpha;

    private bool isActive;

    private void Awake() {
        if (!GameModeManager.Instance.IsRelayMode) {
            gameObject.SetActive(false);
        }
    }

    private void OnEnable() {
        iconImage.sprite = micMutedSprite;
        iconImage.color = Color.red;

        _targetFill = 0f;
        _targetIconAlpha = iconIdleAlpha;

        if (VivoxManager.Instance != null) {
            if (string.IsNullOrEmpty(VivoxManager.Instance.CurrentChannelName)) {
                fillImage.color = unmutedFillColor;
                isActive = true;
                iconImage.color = Color.white;
                action.performed += _ => OnActionPerformed();
                action.Enable();
            } else {
                VivoxManager.Instance.OnChannelJoined += (_) => {
                    fillImage.color = unmutedFillColor;
                    isActive = true;
                    iconImage.color = Color.white;
                    action.performed += _ => OnActionPerformed();
                    action.Enable();
                };
            }

            VivoxManager.Instance.OnLocalMuteChanged += HandleMuteStateChanged;
            VivoxManager.Instance.OnParticipantSpeechChanged += HandleSpeechChanged;

            RefreshVisual(VivoxManager.Instance.IsLocallyMuted);
            _targetFill = VivoxManager.Instance.IsLocallyMuted ? 0f : intensitySlider.value;
            _targetIconAlpha = VivoxManager.Instance.IsLocallyMuted ? iconSpeakingAlpha : iconIdleAlpha;
            SetIconAlpha(_targetIconAlpha);
        }
    }

    private void OnDisable() {
        action.Disable();
        action.Dispose();

        if (VivoxManager.Instance != null) {
            VivoxManager.Instance.OnLocalMuteChanged -= HandleMuteStateChanged;
            VivoxManager.Instance.OnParticipantSpeechChanged -= HandleSpeechChanged;
        }
    }

    void OnActionPerformed() {
        VivoxManager.Instance.ToggleLocalMute();
    }

    private void Update() {
        if (intensitySlider != null) {
            if (fillSmoothing <= 0f)
                intensitySlider.value = _targetFill;
            else
                intensitySlider.value = Mathf.Lerp(intensitySlider.value, _targetFill, Time.deltaTime * fillSmoothing);
        }

        if (iconImage != null) {
            float currentAlpha = iconImage.color.a;
            if (!Mathf.Approximately(currentAlpha, _targetIconAlpha)) {
                float maxDelta = iconFadeDuration > 0f
                    ? Mathf.Abs(iconSpeakingAlpha - iconIdleAlpha) / iconFadeDuration * Time.deltaTime
                    : 1f;
                SetIconAlpha(Mathf.MoveTowards(currentAlpha, _targetIconAlpha, maxDelta));
            }
        }
    }

    // Slider is non-interactive so it never intercepts drag, but Image raycasts
    // still register clicks - this is the only way mute gets toggled now.
    public void OnPointerClick(PointerEventData eventData) {
        if (!isActive) return;
        if (VivoxManager.Instance == null) {
            Debug.LogWarning("[MuteButtonUI] VivoxManager not available.");
            return;
        }

        VivoxManager.Instance.ToggleLocalMute();
        // RefreshVisual + SFX both fire via OnLocalMuteChanged, so no need to
        // duplicate them here.
    }

    private void HandleMuteStateChanged(bool isMuted) {
        RefreshVisual(isMuted);
        PlayMuteSfx(isMuted);

        if (isMuted) {
            _targetFill = 0f; // muted input transmits nothing, so force the meter to empty
            _targetIconAlpha = iconIdleAlpha; // muted always shown at full opacity
        }
    }

    private void HandleSpeechChanged(string speakerDisplayName, bool isSpeaking, double audioEnergy) {
        if (VivoxManager.Instance == null)
            return;

        if (string.IsNullOrEmpty(VivoxManager.Instance.LocalDisplayName) ||
            speakerDisplayName != VivoxManager.Instance.LocalDisplayName)
            return;

        if (VivoxManager.Instance.IsLocallyMuted) {
            _targetFill = 0f;
            _targetIconAlpha = iconIdleAlpha;
            return;
        }

        bool effectiveSpeaking = isSpeaking && audioEnergy >= minEnergyThreshold;
        _targetFill = effectiveSpeaking ? (float)audioEnergy : 0f;
        _targetIconAlpha = effectiveSpeaking ? iconSpeakingAlpha : iconIdleAlpha;
    }

    private void SetIconAlpha(float alpha) {
        var c = iconImage.color;
        c.a = alpha;
        iconImage.color = c;
    }

    private void PlayMuteSfx(bool isMuted) {
        var clip = isMuted ? muteSound : unmuteSound;
        if (clip != null)
            sfxSource.PlayOneShot(clip, sfxVolume);
    }

    private void RefreshVisual(bool isMuted) {
        iconImage.sprite = isMuted ? micMutedSprite : micOnSprite;
        fillImage.color = isMuted ? mutedFillColor : unmutedFillColor;
    }
}