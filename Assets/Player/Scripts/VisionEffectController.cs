using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// NetworkBehaviour so it can receive the targeted hunted-effect RPC directly.
// EnemyController calls SetHuntedClientRpc on THIS component, targeted at
// the hunted player's OwnerClientId via ClientRpcParams. Ambient stress
// effects (vignette darkening, desaturation) are driven locally/by whatever
// gameplay system tracks stress — call SetStressLevel directly, or route it
// through SetStressLevelClientRpc if a server system needs to drive it.
public class VisionEffectController : NetworkBehaviour {
    public Volume volume; // assign the Global Volume (or a local one)
    private DepthOfField dof;
    private Vignette vignette;
    private ChromaticAberration chromaticAberration;
    private MotionBlur motionBlur;
    private ColorAdjustments colorAdjustments;

    [Header("Ambient Stress (0-1, drives vignette darkening + desaturation)")]
    [SerializeField] private Color stressVignetteColor = Color.black;
    [SerializeField] private float maxStressVignetteIntensity = 0.25f;
    [SerializeField] private float maxDesaturation = -100f; // ColorAdjustments saturation range is -100..100
    private float stressLevel; // 0 = calm, 1 = max ambient dread

    [Header("Hunted Effect (red vignette + RGB split + motion blur)")]
    [SerializeField] private Color huntedVignetteColor = Color.red;
    [SerializeField] private float maxVignetteIntensity = 0.45f;
    [SerializeField] private float maxRgbSplitIntensity = 0.6f;
    [SerializeField] private float maxHuntedMotionBlurIntensity = 0.5f;
    [SerializeField] private float fadeSpeed = 4f;
    [SerializeField] private bool pulse = true;
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float pulseAmount = 0.15f;
    private bool isHunted;
    private float currentHuntedIntensity;

    [Header("Flashlight Flicker")]
    [Tooltip("Assign the flashlight's Light component. Flicker only runs while it's enabled.")]
    [SerializeField] private Light flashlightLight;
    [SerializeField] private float flickerChancePerSecond = 0.5f; // avg flicker events per second
    [SerializeField] private float flickerDuration = 0.08f;
    [SerializeField] private float flickerMinIntensityMultiplier = 0.35f;
    private float baseFlashlightIntensity;
    private bool isFlickering;
    private float flickerTimer;

    void Start() {
        volume.profile.TryGet(out dof);
        volume.profile.TryGet(out vignette);
        volume.profile.TryGet(out chromaticAberration);
        volume.profile.TryGet(out motionBlur);
        volume.profile.TryGet(out colorAdjustments);

        if (flashlightLight != null) baseFlashlightIntensity = flashlightLight.intensity;
    }

    public override void OnNetworkSpawn() {
        // Only the local player's own instance should ever touch the
        // (usually scene-shared) Volume — remote copies of other players'
        // VisionEffectController would otherwise run Update() too and
        // stomp your active effect back to 0 every other frame.
        if (!IsOwner) {
            enabled = false;
        }
    }

    private void Update() {
        UpdateHuntedIntensity();
        UpdateVignette();
        SetBlur(currentHuntedIntensity);
        SetRGBSplit();
        SetMotionBlur(currentHuntedIntensity);
        SetDesaturation(stressLevel);
        HandleFlashlightFlicker();
    }

    private void UpdateHuntedIntensity() {
        float target = isHunted ? 1f : 0f;
        if (isHunted && pulse) {
            target += Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
            target = Mathf.Clamp01(target);
        }
        currentHuntedIntensity = Mathf.MoveTowards(currentHuntedIntensity, target, fadeSpeed * Time.deltaTime);
    }

    // Vignette is shared between the ambient stress look (calm darkening)
    // and the hunted look (red) — whichever is stronger wins on intensity,
    // and color leans red as soon as hunted intensity is nonzero.
    private void UpdateVignette() {
        if (vignette == null) return;

        float stressIntensity = Mathf.Lerp(0f, maxStressVignetteIntensity, stressLevel);
        float huntedIntensity = Mathf.Lerp(0f, maxVignetteIntensity, currentHuntedIntensity);
        float finalIntensity = Mathf.Max(stressIntensity, huntedIntensity);

        Color finalColor = currentHuntedIntensity > 0.01f
            ? Color.Lerp(stressVignetteColor, huntedVignetteColor, currentHuntedIntensity)
            : stressVignetteColor;

        vignette.active = finalIntensity > 0f;
        vignette.intensity.value = finalIntensity;
        vignette.color.value = finalColor;
    }

    public void SetBlur(float intensity) // 0 = clear, 1 = max blur
    {
        if (dof != null) {
            dof.active = intensity > 0f;
            dof.gaussianStart.value = Mathf.Lerp(50f, 0f, intensity);
            dof.gaussianEnd.value = Mathf.Lerp(60f, 5f, intensity);
        }
    }

    // 0 = no channel split, 1 = max chromatic aberration / RGB fringing.
    // Requires a Chromatic Aberration override on the assigned Volume profile.
    public void SetRGBSplit() {
        float intensity = isHunted ? 1f : 0f;

        if (chromaticAberration == null) return;
        intensity = Mathf.Clamp01(intensity);
        chromaticAberration.active = intensity > 0f;
        chromaticAberration.intensity.value = Mathf.Lerp(0f, maxRgbSplitIntensity, intensity);
    }

    // 0 = no blur, 1 = max motion blur. Driven by hunted intensity for a
    // chase-feeling smear; requires a Motion Blur override on the Volume.
    public void SetMotionBlur(float intensity) {
        if (motionBlur == null) return;
        intensity = Mathf.Clamp01(intensity);
        motionBlur.active = intensity > 0f;
        motionBlur.intensity.value = Mathf.Lerp(0f, maxHuntedMotionBlurIntensity, intensity);
    }

    // 0 = normal color, 1 = fully desaturated. Requires a Color Adjustments
    // override on the Volume. Driven by ambient stressLevel, not hunted state.
    public void SetDesaturation(float intensity) {
        if (colorAdjustments == null) return;
        intensity = Mathf.Clamp01(intensity);
        colorAdjustments.saturation.value = Mathf.Lerp(0f, maxDesaturation, intensity);
    }

    // Ambient dread level, independent of being actively hunted — feeds the
    // baseline vignette darkening and desaturation. Drive this from whatever
    // tracks tension (enemy proximity while patrolling, time-in-dark, etc).
    public void SetStressLevel(float value) {
        stressLevel = Mathf.Clamp01(value);
    }

    [ClientRpc]
    public void SetStressLevelClientRpc(float value, ClientRpcParams rpcParams = default) {
        SetStressLevel(value);
    }

    private void HandleFlashlightFlicker() {
        if (flashlightLight == null || !flashlightLight.enabled) return;

        if (!isFlickering) {
            if (Random.value < flickerChancePerSecond * Time.deltaTime) {
                isFlickering = true;
                flickerTimer = flickerDuration;
            } else {
                flashlightLight.intensity = baseFlashlightIntensity;
            }
        } else {
            flashlightLight.intensity = baseFlashlightIntensity * Random.Range(flickerMinIntensityMultiplier, 1f);
            flickerTimer -= Time.deltaTime;
            if (flickerTimer <= 0f) {
                isFlickering = false;
                flashlightLight.intensity = baseFlashlightIntensity;
            }
        }
    }

    // rpcParams is populated by the caller (EnemyController) with a single
    // TargetClientIds entry — this player's own OwnerClientId — so it never
    // reaches anyone else's screen.
    [ClientRpc]
    public void SetHuntedClientRpc(bool hunted, ClientRpcParams rpcParams = default) {
        isHunted = hunted;
    }
}