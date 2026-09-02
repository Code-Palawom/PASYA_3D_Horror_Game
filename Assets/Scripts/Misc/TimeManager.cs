using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class TimeManager : NetworkBehaviour {
    public static TimeManager Instance { get; private set; }

    [SerializeField] private Light globalLight;

    [Header("Day")]
    [SerializeField] private float dayIntensity = 1f;
    [SerializeField] private Color dayLightColor = Color.white;

    [Header("Night")]
    [SerializeField] private float nightIntensity = 0.05f;
    [Tooltip("Cool blue moonlight tint applied to globalLight at night.")]
    [SerializeField] private Color moonLightColor = new Color(0.65f, 0.08f, 0.08f);

    [Header("Skybox (4-texture blend)")]
    [Tooltip("Skybox material's shader must expose _Texture1, _Texture2 and _Blend (a standard two-texture blended skybox shader). Assign that material in the Lighting settings so RenderSettings.skybox already points to it.")]
    [SerializeField] private Texture2D skyboxNight;
    [SerializeField] private Texture2D skyboxSunrise;
    [SerializeField] private Texture2D skyboxDay;
    [SerializeField] private Texture2D skyboxSunset;
    [Tooltip("In-game hour each skybox phase begins.")]
    [SerializeField] private int skyboxSunriseHour = 6;
    [SerializeField] private int skyboxDayHour = 8;
    [SerializeField] private int skyboxDuskHour = 18;
    [SerializeField] private int skyboxNightHour = 22;
    [Tooltip("How many in-game minutes the texture blend takes once a new phase starts. Pure function of time, so it's always correct instantly for late joiners / admin time-skips — no coroutines.")]
    [SerializeField] private float skyboxTransitionInGameMinutes = 120f;

    [Header("Ambient (Environment Lighting — Gradient)")]
    [Tooltip("Sets RenderSettings.ambientMode = Trilight so ambient light no longer just inherits skybox brightness.")]
    [SerializeField] private Color ambientSkyDay = new Color(0.5f, 0.7f, 1f);
    [SerializeField] private Color ambientEquatorDay = new Color(0.7f, 0.75f, 0.75f);
    [SerializeField] private Color ambientGroundDay = new Color(0.4f, 0.4f, 0.35f);
    [SerializeField] private Color ambientSkyNight = new Color(0.05f, 0.02f, 0.05f);
    [SerializeField] private Color ambientEquatorNight = new Color(0.05f, 0.02f, 0.03f);
    [SerializeField] private Color ambientGroundNight = new Color(0.02f, 0.01f, 0.01f);
    [Tooltip("Scales ambient specular from skybox/reflection probes. Left at 1 this keeps reflecting full-strength sky onto surfaces at night, reading as metallic even on non-metal materials.")]
    [SerializeField] private float reflectionIntensityDay = 1f;
    [SerializeField] private float reflectionIntensityNight = 0.25f;

    [Header("Fog")]
    [SerializeField] private bool controlFog = true;
    [SerializeField] private Color fogColorDay = new Color(0.75f, 0.85f, 0.95f);
    [SerializeField] private Color fogColorNight = new Color(0.05f, 0.02f, 0.04f);
    [SerializeField] private float fogDensityDay = 0.005f;
    [SerializeField] private float fogDensityNight = 0.02f;

    [Header("Light Shadows / Indirect")]
    [SerializeField] private float dayShadowStrength = 1f;
    [SerializeField] private float nightShadowStrength = 0.85f;
    [SerializeField] private float dayIndirectMultiplier = 1f;
    [SerializeField] private float nightIndirectMultiplier = 0.2f;

    [Header("Post-Processing (URP Global Volume)")]
    [Tooltip("Assign the scene's global Volume. Needs Color Adjustments, Vignette, and Bloom overrides on its profile for these to take effect.")]
    [SerializeField] private Volume globalVolume;
    [SerializeField] private float dayPostExposure = 0f;
    [SerializeField] private float nightPostExposure = -1f;
    [SerializeField] private float dayVignetteIntensity = 0.15f;
    [SerializeField] private float nightVignetteIntensity = 0.35f;
    [SerializeField] private float dayBloomThreshold = 1.1f;
    [SerializeField] private float nightBloomThreshold = 0.6f;

    [Header("Time-of-day schedule")]
    [SerializeField] private int nightStartHour = 18;
    [SerializeField] private int nightEndHour = 6;

    [Tooltip("Hour brightening begins (t starts easing from 1 toward 0).")]
    [SerializeField] private int dawnStartHour = 5;
    [Tooltip("Hour full daylight is reached (t=0). Holds flat until duskStartHour.")]
    [SerializeField] private int dayMaxHour = 8;
    [Tooltip("Hour darkening begins (t starts easing from 0 toward 1).")]
    [SerializeField] private int duskStartHour = 17;
    [Tooltip("Hour full darkness is reached (t=1). Holds flat until dawnStartHour (wraps past midnight).")]
    [SerializeField] private int nightMaxHour = 19;

    private ColorAdjustments colorAdjustments;
    private Vignette vignette;
    private Bloom bloom;
    private DepthOfField dof;
    private ChromaticAberration chromaticAberration;

    [Header("Time Speed")]
    [Tooltip("How many real-world minutes a full 24-hour in-game day takes.")]
    [SerializeField] private float dayLengthInRealMinutes = 24f;

    [Header("UI (optional scene-based clock)")]
    [SerializeField] private TMPro.TMP_Text clockText;

    private bool isNight;

    private float SecondsPerGameMinute => (dayLengthInRealMinutes * 60f) / 1440f;

    // ---- Server-authoritative time state ----
    // Fully collapsed hour+minute+day into ONE NetworkVariable that just
    // counts total elapsed game-minutes since start and never wraps.
    // Previously this was netHours/netMinutes/netDays (three separate
    // NetworkVariables), then netTotalMinutes/netDays (two). Even with two,
    // a client could theoretically apply the snapshot deltas out of
    // declaration order and read a stale netDays for one callback (harmless
    // here since Days doesn't drive visuals, but still a real race). With a
    // single value there is nothing left to be out of sync with — Hours,
    // Minutes, and Days are all just derived math on the one synced number.
    private readonly NetworkVariable<long> netTotalGameMinutes = new NetworkVariable<long>(
        18 * 60, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public int Minutes => (int)(netTotalGameMinutes.Value % 60);
    public int Hours => (int)((netTotalGameMinutes.Value / 60) % 24);
    public int Days => (int)(netTotalGameMinutes.Value / 1440);
    public int DisplayDay => Days + 1; // "Day 1" on day zero

    private float tempSecond;
    private float absDayLengthInRealMinutes;

    // ---- Event other scripts (e.g. player prefab UI) can subscribe to ----
    public event System.Action OnTimeUpdated;

    // Static, scene-independent: fires whenever a TimeManager becomes available
    // (e.g. on Level scene load, after the Lobby had none). Anything spawned
    // before this scene — like a player object carried over via DontDestroyOnLoad —
    // won't have OnNetworkSpawn() run again, so it needs this instead to know
    // a TimeManager now exists.
    public static event System.Action<TimeManager> OnAnyTimeManagerReady;

    public override void OnNetworkSpawn() {
        Instance = this;

        absDayLengthInRealMinutes = dayLengthInRealMinutes;

        netTotalGameMinutes.OnValueChanged += OnTotalGameMinutesChanged;

        // Ambient light: switch off skybox-driven ambient so it stops just
        // inheriting the skybox's brightness, and drive it from our own
        // day/night gradient instead.
        RenderSettings.ambientMode = AmbientMode.Trilight;

        // Fog setup (one-time mode/type; color & density are lerped per-tick).
        RenderSettings.fog = controlFog;
        if (controlFog) RenderSettings.fogMode = FogMode.ExponentialSquared;

        // Cache Volume override components once so we're not doing TryGet
        // every frame/update.
        if (globalVolume != null && globalVolume.profile != null) {
            globalVolume.profile.TryGet(out colorAdjustments);
            globalVolume.profile.TryGet(out vignette);
            globalVolume.profile.TryGet(out bloom);
            globalVolume.profile.TryGet(out dof);
            globalVolume.profile.TryGet(out chromaticAberration);
        }

        // Snap instantly to correct visuals — no tween for late joiners / host start
        ApplyVisualsForTime(Hours, Minutes);
        UpdateClockUI();

        OnAnyTimeManagerReady?.Invoke(this);
    }

    public override void OnNetworkDespawn() {
        netTotalGameMinutes.OnValueChanged -= OnTotalGameMinutesChanged;

        if (Instance == this) Instance = null;
    }

    private void Update() {
        if (!IsServer) return;

        tempSecond += Time.deltaTime;
        if (tempSecond >= SecondsPerGameMinute) {
            tempSecond = 0f;
            AdvanceMinute();
        }
    }

    // ---- Server-only progression ----
    private void AdvanceMinute() {
        // One value, one write — day/hour rollover is just arithmetic on
        // read (see Hours/Minutes/Days above), so there's no multi-variable
        // ordering to get right here anymore.
        netTotalGameMinutes.Value++;
    }

    // ---- Admin-only entry points ----
    // Called server-side, only after ChatCommandProcessor has verified the
    // caller's role. These bypass tweened transitions and snap instantly.

    // Sets the clock to an exact hour/minute. Does not change the day counter.
    public void AdminSetTime(int hour, int minute) {
        if (!IsServer) return;

        hour = Mathf.Clamp(hour, 0, 23);
        minute = Mathf.Clamp(minute, 0, 59);

        // Keep the current day, just replace the hour/minute-of-day part.
        long dayStart = (long)Days * 1440;
        netTotalGameMinutes.Value = dayStart + hour * 60 + minute;

        SnapAllClientsRpc(hour, minute);
    }

    // Advances the clock by a number of hours, rolling over days as needed.
    public void AdminSkipHours(int hoursToSkip) {
        if (!IsServer) return;

        long newTotal = netTotalGameMinutes.Value + (long)hoursToSkip * 60;
        if (newTotal < 0) newTotal = 0; // guard against negative skips going below start

        netTotalGameMinutes.Value = newTotal;

        SnapAllClientsRpc((int)((newTotal / 60) % 24), (int)(newTotal % 60));
    }

    public void AdminSetTimeSpeed(float timeSpeed) {
        if (!IsServer) return;

        dayLengthInRealMinutes = absDayLengthInRealMinutes / timeSpeed;
    }

    // Snaps every client straight to the target hour/minute's visuals instantly.
    [Rpc(SendTo.ClientsAndHost)]
    private void SnapAllClientsRpc(int hour, int minute) {
        ApplyVisualsForTime(hour, minute);
    }

    // ---- Runs on every client (incl. host) whenever synced state changes ----
    // Single callback now covers hour/minute AND day — there's only one
    // NetworkVariable to react to, so visuals and the "Day N" text always
    // update together from the same value, same frame.
    private void OnTotalGameMinutesChanged(long oldValue, long newValue) {
        ApplyVisualsForTime(Hours, Minutes);
        UpdateClockUI();
    }

    // Four explicit control points instead of a single continuous curve:
    // dawnStartHour -> dayMaxHour   : brightening ramp   (t: 1 -> 0)
    // dayMaxHour    -> duskStartHour: full-day plateau    (t = 0)
    // duskStartHour -> nightMaxHour : darkening ramp      (t: 0 -> 1)
    // nightMaxHour  -> dawnStartHour: full-night plateau  (t = 1, wraps past midnight)
    // Assumes dawnStartHour < dayMaxHour <= duskStartHour < nightMaxHour within the
    // same day; only the final night plateau wraps across midnight.
    private void ApplyVisualsForTime(int hour, int minute) {
        float totalMinutes = hour * 60 + minute;

        float dawnStartMin = dawnStartHour * 60;
        float dayMaxMin = dayMaxHour * 60;
        float duskStartMin = duskStartHour * 60;
        float nightMaxMin = nightMaxHour * 60;

        float t;
        if (totalMinutes >= dawnStartMin && totalMinutes < dayMaxMin) {
            float span = dayMaxMin - dawnStartMin;
            float frac = span > 0f ? (totalMinutes - dawnStartMin) / span : 1f;
            t = 0.5f * (1f + Mathf.Cos(frac * Mathf.PI)); // 1 -> 0
        } else if (totalMinutes >= dayMaxMin && totalMinutes < duskStartMin) {
            t = 0f; // full-day plateau
        } else if (totalMinutes >= duskStartMin && totalMinutes < nightMaxMin) {
            float span = nightMaxMin - duskStartMin;
            float frac = span > 0f ? (totalMinutes - duskStartMin) / span : 1f;
            t = 0.5f * (1f - Mathf.Cos(frac * Mathf.PI)); // 0 -> 1
        } else {
            t = 1f; // full-night plateau: nightMaxHour..24:00 and 00:00..dawnStartHour
        }

        // Single continuous rotation per day (like a real sun), but flipped
        // to its antipodal position whenever it would dip below the horizon.
        // This keeps sun/moon rising and setting at opposite horizon points
        // — the moon rises where the sun just set, arcs overhead by
        // midnight, and sets on the other side by dawn — instead of either
        // going below the horizon (kills shadows) or bouncing back to the
        // same point it rose from (previous fix's bug).
        float dayFraction = totalMinutes / 1440f; // 0 = midnight, 0.5 = noon
        float thetaDeg = dayFraction * 360f - 90f;
        if (Mathf.Sin(thetaDeg * Mathf.Deg2Rad) < 0f) {
            thetaDeg -= 180f; // swap to the antipodal position, now above horizon
        }
        globalLight.transform.rotation = Quaternion.Euler(thetaDeg, -90f, 0f);
        globalLight.intensity = Mathf.Lerp(dayIntensity, nightIntensity, t);
        globalLight.color = Color.Lerp(dayLightColor, moonLightColor, t);
        globalLight.shadowStrength = Mathf.Lerp(dayShadowStrength, nightShadowStrength, t);
        globalLight.bounceIntensity = Mathf.Lerp(dayIndirectMultiplier, nightIndirectMultiplier, t);

        ApplySkyboxBlend(hour, minute);
        PushCelestialGlobals(hour);

        // Ambient (Environment Lighting) — independent of skybox brightness now.
        RenderSettings.ambientSkyColor = Color.Lerp(ambientSkyDay, ambientSkyNight, t);
        RenderSettings.ambientEquatorColor = Color.Lerp(ambientEquatorDay, ambientEquatorNight, t);
        RenderSettings.ambientGroundColor = Color.Lerp(ambientGroundDay, ambientGroundNight, t);
        RenderSettings.reflectionIntensity = Mathf.Lerp(reflectionIntensityDay, reflectionIntensityNight, t);

        // Fog
        if (controlFog) {
            RenderSettings.fogColor = Color.Lerp(fogColorDay, fogColorNight, t);
            RenderSettings.fogDensity = Mathf.Lerp(fogDensityDay, fogDensityNight, t);
        }

        // Post-processing (Volume overrides), if assigned/present on the profile.
        if (colorAdjustments != null) colorAdjustments.postExposure.value = Mathf.Lerp(dayPostExposure, nightPostExposure, t);
        if (vignette != null) vignette.intensity.value = Mathf.Lerp(dayVignetteIntensity, nightVignetteIntensity, t);
        if (bloom != null) bloom.threshold.value = Mathf.Lerp(dayBloomThreshold, nightBloomThreshold, t);

        bool shouldBeNight = IsNightHour(hour);

        if (shouldBeNight == isNight) return;

        isNight = shouldBeNight;

        if (isNight) {
            if (dof != null) dof.active = true;
            if (chromaticAberration != null) chromaticAberration.active = true;
        } else {
            if (dof != null) dof.active = false;
            if (chromaticAberration != null) chromaticAberration.active = true;
        }
    }

    // Pure function of the current hour/minute — always correct instantly for
    // late joiners and admin time-skips, no coroutines needed. Determines
    // which phase we're in (night/sunrise/day/sunset), then blends from the
    // previous phase's texture into the current one over the first
    // `skyboxTransitionInGameMinutes` minutes of the phase, holding steady
    // after that until the next boundary.
    private void ApplySkyboxBlend(int hour, int minute) {
        if (RenderSettings.skybox == null) return;

        int totalMinutes = hour * 60 + minute;

        int sunriseMin = skyboxSunriseHour * 60;
        int dayMin = skyboxDayHour * 60;
        int duskMin = skyboxDuskHour * 60;
        int nightMin = skyboxNightHour * 60;

        Texture2D fromTex, toTex;
        int phaseStart;

        if (totalMinutes >= sunriseMin && totalMinutes < dayMin) {
            fromTex = skyboxNight; toTex = skyboxSunrise; phaseStart = sunriseMin;
        } else if (totalMinutes >= dayMin && totalMinutes < duskMin) {
            fromTex = skyboxSunrise; toTex = skyboxDay; phaseStart = dayMin;
        } else if (totalMinutes >= duskMin && totalMinutes < nightMin) {
            fromTex = skyboxDay; toTex = skyboxSunset; phaseStart = duskMin;
        } else {
            // Night phase — wraps past midnight, so phaseStart may be "later"
            // in raw minutes than totalMinutes; elapsed is corrected below.
            fromTex = skyboxSunset; toTex = skyboxNight; phaseStart = nightMin;
        }

        float elapsed = totalMinutes - phaseStart;
        if (elapsed < 0f) elapsed += 1440f;

        float blend = skyboxTransitionInGameMinutes > 0f
            ? Mathf.Clamp01(elapsed / skyboxTransitionInGameMinutes)
            : 1f;

        RenderSettings.skybox.SetTexture("_Texture1", fromTex);
        RenderSettings.skybox.SetTexture("_Texture2", toTex);
        RenderSettings.skybox.SetFloat("_Blend", blend);
    }

    // Same night window used for the DOF/chromatic-aberration toggle below —
    // shared here so the moon disc snaps on/off at the exact same hour.
    private bool IsNightHour(int hour) {
        return nightStartHour <= nightEndHour
            ? hour >= nightStartHour && hour < nightEndHour          // e.g. 8 -> 18, same-day window
            : hour >= nightStartHour || hour < nightEndHour;         // e.g. 22 -> 6, wraps past midnight
    }

    // Drives the skybox shader's sun/moon disc from the same directional light
    // that's already being rotated for real lighting — no separate sun/moon
    // transform needed. _MoonAmount is a hard snap (1 = moon, 0 = sun) at
    // nightStartHour/nightEndHour, not the continuous day/night t — the disc
    // swap is instant, everything else (light color, ambient, fog) keeps
    // fading smoothly on its own curve.
    private void PushCelestialGlobals(int hour) {
        if (RenderSettings.skybox == null) return;

        Vector3 towardLight = -globalLight.transform.forward;
        RenderSettings.skybox.SetVector("_CelestialDir", new Vector4(towardLight.x, towardLight.y, towardLight.z, 0f));
        RenderSettings.skybox.SetFloat("_MoonAmount", IsNightHour(hour) ? 1f : 0f);
    }

    // ---- 12-hour formatting: "12:30AM", "9:00PM" ----
    public string GetFormattedTime() {
        int h = Hours;
        int m = Minutes;

        string period = h >= 12 ? "PM" : "AM";
        int displayHour = h % 12;
        if (displayHour == 0) displayHour = 12;

        return $"{displayHour}:{m:00} {period}";
    }

    // ---- "Day 5" formatting ----
    public string GetFormattedDay() {
        return $"Day {DisplayDay}";
    }

    private void UpdateClockUI() {
        if (clockText != null) clockText.text = GetFormattedTime();
        OnTimeUpdated?.Invoke();
    }
}