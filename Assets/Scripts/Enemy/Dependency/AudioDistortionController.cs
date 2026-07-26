using UnityEngine;
using UnityEngine.Audio;

// Plain MonoBehaviour, not networked — driven entirely by SanityController
// calling SetStress locally, which only ever happens on the owning client
// (see SanityController.OnStressChanged). Two independently-thresholded
// layers so distortion escalates in stages rather than all at once:
//   1. Muffling: a low-pass filter on the master mix, ramping in once
//      stress crosses muffleStartThreshold.
//   2. Hallucinated sounds: random one-shot stingers, only once stress
//      crosses the higher hallucinationStartThreshold, with frequency
//      scaling further as stress climbs past that point.
public class AudioDistortionController : MonoBehaviour {
    [Header("Muffling")]
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private string lowPassParam = "MasterLowpassCutoff";
    [SerializeField] private float clearCutoffHz = 22000f;
    [SerializeField] private float muffledCutoffHz = 800f;
    [SerializeField] private float muffleStartThreshold = 0.4f; // stress below this = no muffling

    [Header("Hallucinated Sounds")]
    [Tooltip("A local, non-spatial (or lightly panned) source — this is only ever heard by the local player, never networked.")]
    [SerializeField] private AudioSource hallucinationSource;
    [SerializeField] private AudioClip[] hallucinationClips;
    [SerializeField] private float hallucinationStartThreshold = 0.6f; // stress below this = no hallucinations
    [SerializeField] private float maxHallucinationsPerMinute = 6f; // rate once fully past threshold
    [SerializeField] private float minGapBetweenHallucinations = 4f; // hard floor regardless of stress
    private float hallucinationCooldown;

    private float currentStress;

    private void Update() {
        hallucinationCooldown -= Time.deltaTime;

        if (currentStress < hallucinationStartThreshold) return;
        if (hallucinationCooldown > 0f) return;

        // Scale chance-per-second by how far past the threshold stress is,
        // so hallucinations get MORE frequent the more stressed you are,
        // not just on/off at the threshold.
        float t = Mathf.InverseLerp(hallucinationStartThreshold, 1f, currentStress);
        float chancePerSecond = (maxHallucinationsPerMinute / 60f) * t;

        if (Random.value < chancePerSecond * Time.deltaTime) {
            PlayHallucination();
            hallucinationCooldown = minGapBetweenHallucinations;
        }
    }

    // 0 = calm, 1 = max dread. Call from SanityController.
    public void SetStress(float value) {
        currentStress = Mathf.Clamp01(value);
        ApplyMuffling(currentStress);
    }

    private void ApplyMuffling(float stress) {
        if (audioMixer == null) return;

        float muffleAmount = stress < muffleStartThreshold
            ? 0f
            : Mathf.InverseLerp(muffleStartThreshold, 1f, stress);

        float cutoff = Mathf.Lerp(clearCutoffHz, muffledCutoffHz, muffleAmount);
        audioMixer.SetFloat(lowPassParam, cutoff);
    }

    private void PlayHallucination() {
        if (hallucinationSource == null || hallucinationClips == null || hallucinationClips.Length == 0) return;
        var clip = hallucinationClips[Random.Range(0, hallucinationClips.Length)];
        hallucinationSource.PlayOneShot(clip);
    }
}