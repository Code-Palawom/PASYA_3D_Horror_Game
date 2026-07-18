using UnityEngine;

// Attach to a player's name tag / portrait / HUD element to fade a mic icon
// in/out whenever they're speaking. Set displayName at spawn time (matches
// the Vivox DisplayName set at login), then this reacts to VivoxManager's
// speech detection events automatically.
[RequireComponent(typeof(CanvasGroup))]
public class VoiceActivityIndicator : MonoBehaviour {
    [Header("Visual")]
    [Tooltip("CanvasGroup on the mic icon whose alpha is faded. Auto-assigned from this GameObject if left empty.")]
    [SerializeField] private CanvasGroup micIconCanvasGroup;
    [SerializeField, Range(0f, 1f)] private float speakingAlpha = 1f;
    [SerializeField, Range(0f, 1f)] private float idleAlpha = 0f;
    [Tooltip("Seconds for a full 0->1 (or 1->0) fade. Partial fades scale proportionally.")]
    [SerializeField] private float fadeDuration = 0.15f;

    [Header("Audio Energy Threshold")]
    [Tooltip("Ignore tiny energy fluctuations below this so the icon doesn't flicker.")]
    [SerializeField, Range(0f, 1f)] private float minEnergyThreshold = 0.05f;

    private string displayName;
    private bool _isSpeaking;
    private float _targetAlpha;

    public string DisplayName {
        get => displayName;
        set => displayName = value;
    }

    private void Awake() {
        if (micIconCanvasGroup == null)
            micIconCanvasGroup = GetComponent<CanvasGroup>();
    }

    private void OnEnable() {
        if (VivoxManager.Instance != null)
            VivoxManager.Instance.OnParticipantSpeechChanged += HandleSpeechChanged;

        _targetAlpha = idleAlpha;
        if (micIconCanvasGroup != null)
            micIconCanvasGroup.alpha = idleAlpha;
    }

    private void OnDisable() {
        if (VivoxManager.Instance != null)
            VivoxManager.Instance.OnParticipantSpeechChanged -= HandleSpeechChanged;
    }

    private void HandleSpeechChanged(string speakerDisplayName, bool isSpeaking, double audioEnergy) {
        if (string.IsNullOrEmpty(displayName) || speakerDisplayName != displayName)
            return;

        bool effectiveSpeaking = isSpeaking && audioEnergy >= minEnergyThreshold;
        if (effectiveSpeaking == _isSpeaking)
            return;

        _isSpeaking = effectiveSpeaking;
        _targetAlpha = _isSpeaking ? speakingAlpha : idleAlpha;
    }

    private void Update() {
        if (micIconCanvasGroup == null)
            return;

        if (Mathf.Approximately(micIconCanvasGroup.alpha, _targetAlpha))
            return;

        float maxDelta = fadeDuration > 0f
            ? Mathf.Abs(speakingAlpha - idleAlpha) / fadeDuration * Time.deltaTime
            : 1f; // instant if fadeDuration is 0

        micIconCanvasGroup.alpha = Mathf.MoveTowards(micIconCanvasGroup.alpha, _targetAlpha, maxDelta);
    }
}