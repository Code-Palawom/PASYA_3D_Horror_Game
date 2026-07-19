using UnityEngine;
using UnityEngine.UI;

// Attach to a player's name tag to show a discrete voice-intensity icon.
// Always visible (no fade) - shows one of 5 icons: idle/silent, 3 speaking
// intensity tiers, or muted (which overrides everything else). Set DisplayName
// at spawn time (matches the Vivox DisplayName set at login) and IsMuted
// whenever your synced mute flag (e.g. PlayerLobbyInfo.IsMuted) changes.
[RequireComponent(typeof(Image))]
public class VoiceActivityIndicator : MonoBehaviour {
    [Header("Icon (Image whose sprite is swapped)")]
    [SerializeField] private Image targetImage;

    [Header("Icons - index 0 doubles as idle/silent")]
    [Tooltip("4 sprites, low to high intensity. Index 0 is shown both when silent and at the lowest speaking tier.")]
    [SerializeField] private Sprite[] intensitySprites = new Sprite[4];
    [SerializeField] private Sprite mutedSprite;

    [Header("Intensity Thresholds (0-1 audio energy)")]
    [Tooltip("Below this, treated as silent/idle regardless of SpeechDetected.")]
    [SerializeField, Range(0f, 1f)] private float minEnergyThreshold = 0.05f;
    [Tooltip("Boundaries between intensity tiers 0/1, 1/2, and 2/3.")]
    [SerializeField, Range(0f, 1f)] private float tier1Threshold = 0.25f;
    [SerializeField, Range(0f, 1f)] private float tier2Threshold = 0.5f;
    [SerializeField, Range(0f, 1f)] private float tier3Threshold = 0.75f;

    [Header("Idle Opacity")]
    [Tooltip("Alpha while actively speaking, or muted (muted always shows at full opacity for visibility).")]
    [SerializeField, Range(0f, 1f)] private float speakingAlpha = 1f;
    [Tooltip("Alpha while silent/idle.")]
    [SerializeField, Range(0f, 1f)] private float idleAlpha = 0.4f;
    [Tooltip("Seconds for a full 0->1 (or 1->0) fade. 0 = instant.")]
    [SerializeField] private float fadeDuration = 0.15f;

    [Header("Mic Connection Tint")]
    [Tooltip("RGB tint while IsMicOn is false - i.e. this player hasn't joined a Vivox channel yet.")]
    [SerializeField] private Color micOffColor = Color.red;
    [Tooltip("RGB tint once IsMicOn is true. Does not affect alpha, which the idle/speaking fade controls separately.")]
    [SerializeField] private Color micOnColor = Color.white;

    private string displayName;
    private bool isMuted;
    private bool isMicOn;
    private bool _isSpeaking;
    private int _currentIndex = -1; // -1 forces first ApplyIcon to actually assign
    private float _targetAlpha;

    public string DisplayName {
        get => displayName;
        set => displayName = value;
    }

    // Set from your synced mute flag (e.g. PlayerLobbyInfo.IsMuted via NetworkList).
    public bool IsMuted {
        get => isMuted;
        set {
            if (isMuted == value)
                return;
            isMuted = value;
            ApplyIcon();
            RecomputeTargetAlpha();
        }
    }

    // Set from your synced connection flag (e.g. PlayerLobbyInfo.IsMicOn via NetworkList).
    // Distinct from IsMuted: this reflects whether the player has actually joined a
    // Vivox channel and can transmit at all, not whether they've chosen to mute.
    // Red until true, then white - independent of the idle/speaking alpha fade.
    public bool IsMicOn {
        get => isMicOn;
        set {
            if (isMicOn == value)
                return;
            isMicOn = value;
            ApplyTint();
        }
    }

    private void Awake() {
        if (!GameModeManager.Instance.IsRelayMode) {
            gameObject.SetActive(false);
        }
    }

    private void OnEnable() {
        if (VivoxManager.Instance != null)
            VivoxManager.Instance.OnParticipantSpeechChanged += HandleSpeechChanged;

        _currentIndex = -1;
        _isSpeaking = false;
        ApplyIcon(); // shows idle (index 0) or muted, whichever applies
        ApplyTint(); // red until IsMicOn is set true, white after

        _targetAlpha = isMuted ? speakingAlpha : idleAlpha;
        SetAlpha(_targetAlpha);
    }

    private void OnDisable() {
        if (VivoxManager.Instance != null)
            VivoxManager.Instance.OnParticipantSpeechChanged -= HandleSpeechChanged;
    }

    private void Update() {
        float currentAlpha = targetImage.color.a;
        if (Mathf.Approximately(currentAlpha, _targetAlpha))
            return;

        float maxDelta = fadeDuration > 0f
            ? Mathf.Abs(speakingAlpha - idleAlpha) / fadeDuration * Time.deltaTime
            : 1f; // instant if fadeDuration is 0

        SetAlpha(Mathf.MoveTowards(currentAlpha, _targetAlpha, maxDelta));
    }

    private void HandleSpeechChanged(string speakerDisplayName, bool isSpeaking, double audioEnergy) {
        if (string.IsNullOrEmpty(displayName) || speakerDisplayName != displayName)
            return;

        int index = ComputeIntensityIndex(isSpeaking, audioEnergy);
        _isSpeaking = isSpeaking && audioEnergy >= minEnergyThreshold;

        RecomputeTargetAlpha();

        if (index == _currentIndex)
            return;

        _currentIndex = index;
        ApplyIcon();
    }

    //  Actively speaking -> full opacity. Muted or silent/idle -> dimmed.
    private void RecomputeTargetAlpha() {
        _targetAlpha = (_isSpeaking) ? speakingAlpha : idleAlpha;
    }

    private void SetAlpha(float alpha) {
        if (targetImage == null)
            return;

        var c = targetImage.color;
        c.a = alpha;
        targetImage.color = c;
    }

    private void ApplyTint() {
        if (targetImage == null)
            return;

        var tint = isMicOn ? micOnColor : micOffColor;
        var c = targetImage.color;
        c.r = tint.r;
        c.g = tint.g;
        c.b = tint.b;
        // alpha untouched - the idle/speaking fade owns that channel
        targetImage.color = c;
    }

    private int ComputeIntensityIndex(bool isSpeaking, double audioEnergy) {
        if (!isSpeaking || audioEnergy < minEnergyThreshold)
            return 0; // idle/silent - reuses the lowest-tier sprite

        if (audioEnergy < tier1Threshold) return 0;
        if (audioEnergy < tier2Threshold) return 1;
        if (audioEnergy < tier3Threshold) return 2;
        return 3;
    }

    private void ApplyIcon() {
        if (isMuted) {
            targetImage.sprite = mutedSprite;
            return;
        }

        int index = Mathf.Clamp(_currentIndex < 0 ? 0 : _currentIndex, 0, intensitySprites.Length - 1);
        if (intensitySprites.Length > index && intensitySprites[index] != null)
            targetImage.sprite = intensitySprites[index];
    }
}