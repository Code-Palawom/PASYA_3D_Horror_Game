using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class TimeManager : NetworkBehaviour {
    public static TimeManager Instance { get; private set; }

    [SerializeField] private Light globalLight;
    [SerializeField] private Material skyboxMat;

    [Header("Day")]
    [SerializeField] private Color day = new Color(0.53f, 0.81f, 0.98f);
    [SerializeField] private Color dayHorizon = new Color(0.73f, 0.89f, 1f);
    [SerializeField] private float dayIntensity = 1f;
    [SerializeField] private Color dayLightColor = Color.white;

    [Header("Night")]
    [SerializeField] private Color night = new Color(0.08f, 0.0f, 0.0f);
    [SerializeField] private Color nightHorizon = new Color(0.28f, 0.02f, 0.02f);
    [SerializeField] private float nightIntensity = 0.05f;
    [Tooltip("Cool blue moonlight tint applied to globalLight at night.")]
    [SerializeField] private Color moonLightColor = new Color(0.65f, 0.08f, 0.08f);

    [Header("Sun / Moon (shader disc)")]
    [SerializeField] private float sunSize = 0.05f;
    [SerializeField] private float sunHaze = 0.1f;
    [SerializeField] private Texture2D moonTexture;
    [SerializeField] private float moonSize = 0.25f;
    [Tooltip("Night amount (t) at which the shader swaps the sun disc for the moon.")]
    [SerializeField, Range(0f, 1f)] private float nightThreshold = 0.5f;

    [Header("Clouds (day/night tint only — shape stays static in Inspector)")]
    [SerializeField] private Color cloudTintDay = Color.white;
    [SerializeField] private Color cloudTintNight = new Color(0.35f, 0.08f, 0.08f);

    [Header("Sky Exposure / Ground / Horizon Fog")]
    [Tooltip("The actual fix for a skybox that stays bright at night — a multiplier on top of the zenith/horizon colors.")]
    [SerializeField] private float skyExposureDay = 1f;
    [SerializeField] private float skyExposureNight = 0.2f;
    [SerializeField] private float skySaturationDay = 1f;
    [SerializeField] private float skySaturationNight = 0.5f;
    [SerializeField] private Color groundColorDay = Color.white;
    [SerializeField] private Color groundColorNight = new Color(0.03f, 0.01f, 0.01f);
    [SerializeField] private Color horizonFogColorDay = Color.white;
    [SerializeField] private Color horizonFogColorNight = new Color(0.1f, 0.03f, 0.03f);
    [SerializeField] private float horizonFogDensityDay = 0.25f;
    [SerializeField] private float horizonFogDensityNight = 0.4f;

    [Header("Stars")]
    [Tooltip("Intensity ramps with night depth instead of hard on/off — dimmer at dusk, fuller at true midnight.")]
    [SerializeField] private float starIntensityMin = 0.1f;
    [SerializeField] private float starIntensityMax = 0.4f;

    [Header("Ambient (Environment Lighting — Gradient)")]
    [Tooltip("Sets RenderSettings.ambientMode = Trilight so ambient light no longer just inherits skybox brightness.")]
    [SerializeField] private Color ambientSkyDay = new Color(0.5f, 0.7f, 1f);
    [SerializeField] private Color ambientEquatorDay = new Color(0.7f, 0.75f, 0.75f);
    [SerializeField] private Color ambientGroundDay = new Color(0.4f, 0.4f, 0.35f);
    [SerializeField] private Color ambientSkyNight = new Color(0.05f, 0.02f, 0.05f);
    [SerializeField] private Color ambientEquatorNight = new Color(0.05f, 0.02f, 0.03f);
    [SerializeField] private Color ambientGroundNight = new Color(0.02f, 0.01f, 0.01f);

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

    private ColorAdjustments colorAdjustments;
    private Vignette vignette;
    private Bloom bloom;

    [Header("Time Speed")]
    [Tooltip("How many real-world minutes a full 24-hour in-game day takes.")]
    [SerializeField] private float dayLengthInRealMinutes = 24f;

    [Header("UI (optional scene-based clock)")]
    [SerializeField] private TMPro.TMP_Text clockText;

    private float SecondsPerGameMinute => (dayLengthInRealMinutes * 60f) / 1440f;

    // ---- Server-authoritative time state ----
    private readonly NetworkVariable<int> netMinutes = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> netHours = new NetworkVariable<int>(
        8, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Days start at 1 (Day 1), stored internally as 0-indexed and exposed via DisplayDay
    private readonly NetworkVariable<int> netDays = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public int Minutes => netMinutes.Value;
    public int Hours => netHours.Value;
    public int Days => netDays.Value;
    public int DisplayDay => netDays.Value + 1; // "Day 1" on day zero

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

        netHours.OnValueChanged += OnHoursChanged;
        netMinutes.OnValueChanged += OnMinutesChanged;
        netDays.OnValueChanged += (_, _) => UpdateClockUI();

        // One-time shader setup — these don't change with time of day.
        if (skyboxMat != null) {
            skyboxMat.SetFloat("_SunSize", sunSize);
            skyboxMat.SetFloat("_SunHaze", sunHaze);
            skyboxMat.SetFloat("_MoonSize", moonSize);
            if (moonTexture != null) skyboxMat.SetTexture("_MoonTex", moonTexture);
            RenderSettings.skybox = skyboxMat;
        }

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
        }

        // Snap instantly to correct visuals — no tween for late joiners / host start
        ApplyVisualsForTime(netHours.Value, netMinutes.Value);
        UpdateClockUI();

        OnAnyTimeManagerReady?.Invoke(this);
    }

    public override void OnNetworkDespawn() {
        netHours.OnValueChanged -= OnHoursChanged;
        netMinutes.OnValueChanged -= OnMinutesChanged;

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
        int newMinutes = netMinutes.Value + 1;
        int newHours = netHours.Value;
        int newDays = netDays.Value;

        if (newMinutes >= 60) {
            newMinutes = 0;
            newHours++;
        }
        if (newHours >= 24) {
            newHours = 0;
            newDays++;
        }

        netMinutes.Value = newMinutes;
        if (newHours != netHours.Value) netHours.Value = newHours;
        if (newDays != netDays.Value) netDays.Value = newDays;
    }

    // ---- Admin-only entry points ----
    // Called server-side, only after ChatCommandProcessor has verified the
    // caller's role. These bypass tweened transitions and snap instantly.

    // Sets the clock to an exact hour/minute. Does not change the day counter.
    public void AdminSetTime(int hour, int minute) {
        if (!IsServer) return;

        hour = Mathf.Clamp(hour, 0, 23);
        minute = Mathf.Clamp(minute, 0, 59);

        netHours.Value = hour;
        netMinutes.Value = minute;

        SnapAllClientsRpc(hour, minute);
    }

    // Advances the clock by a number of hours, rolling over days as needed.
    public void AdminSkipHours(int hoursToSkip) {
        if (!IsServer) return;

        int total = netHours.Value * 60 + netMinutes.Value + hoursToSkip * 60;
        int dayDelta = Mathf.FloorToInt(total / 1440f);
        int rem = total - dayDelta * 1440;
        if (rem < 0) rem += 1440; // guard against negative skips

        int newHour = rem / 60;
        int newMinute = rem % 60;

        netDays.Value += dayDelta;
        netHours.Value = newHour;
        netMinutes.Value = newMinute;

        SnapAllClientsRpc(newHour, newMinute);
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
    private void OnMinutesChanged(int oldValue, int newValue) {
        ApplyVisualsForTime(netHours.Value, netMinutes.Value);
        UpdateClockUI();
    }

    private void OnHoursChanged(int oldValue, int newValue) {
        // Visuals are already refreshed by OnMinutesChanged (hour rollover
        // always coincides with a minute change); this just covers the UI.
        UpdateClockUI();
    }

    // Continuous, phase-aligned day/night: t = 0 at noon (full day),
    // t = 1 at midnight (full night), smoothly interpolating through
    // sunrise (~06:00) and sunset (~18:00).
    private void ApplyVisualsForTime(int hour, int minute) {
        float totalMinutes = hour * 60 + minute;
        float dayFraction = totalMinutes / 1440f; // 0 = midnight, 0.5 = noon

        float t = 0.5f * (1f + Mathf.Cos(dayFraction * 2f * Mathf.PI));

        // Elevation always stays >= 0 so the light never points below the
        // horizon (which would mean it's not hitting the ground at all —
        // the old formula did this at midnight and killed all shadows).
        // Sun arcs from horizon -> overhead -> horizon across the day half,
        // then the SAME light continues as the moon's arc across the night
        // half: horizon -> overhead (midnight) -> horizon. Horizon points
        // land exactly at 6am/6pm either way.
        float elevation = 90f * Mathf.Abs(Mathf.Cos(dayFraction * 2f * Mathf.PI));
        globalLight.transform.rotation = Quaternion.Euler(elevation, -90f, 0f);
        globalLight.intensity = Mathf.Lerp(dayIntensity, nightIntensity, t);
        globalLight.color = Color.Lerp(dayLightColor, moonLightColor, t);
        globalLight.shadowStrength = Mathf.Lerp(dayShadowStrength, nightShadowStrength, t);
        globalLight.bounceIntensity = Mathf.Lerp(dayIndirectMultiplier, nightIndirectMultiplier, t);

        if (skyboxMat != null) {
            skyboxMat.SetColor("_ZenithColor", Color.Lerp(day, night, t));
            skyboxMat.SetColor("_HorizonColor", Color.Lerp(dayHorizon, nightHorizon, t));
            skyboxMat.SetFloat("_AtmosphereThickness", Mathf.Lerp(0.5f, 1f, t));
            skyboxMat.SetFloat("_EnableStars", t);
            skyboxMat.SetFloat("_EnableMoon", t > nightThreshold ? 1f : 0f);
            skyboxMat.SetColor("_CloudTint", Color.Lerp(cloudTintDay, cloudTintNight, t));

            skyboxMat.SetFloat("_SkyExposure", Mathf.Lerp(skyExposureDay, skyExposureNight, t));
            skyboxMat.SetFloat("_SkySaturation", Mathf.Lerp(skySaturationDay, skySaturationNight, t));
            skyboxMat.SetColor("_GroundColor", Color.Lerp(groundColorDay, groundColorNight, t));
            skyboxMat.SetColor("_HorizonFogColor", Color.Lerp(horizonFogColorDay, horizonFogColorNight, t));
            skyboxMat.SetFloat("_HorizonFogDensity", Mathf.Lerp(horizonFogDensityDay, horizonFogDensityNight, t));

            // Stars ramp in past the same threshold used to swap the moon disc,
            // instead of a hard on/off — dim right after dusk, fuller at true midnight.
            float starRamp = Mathf.InverseLerp(nightThreshold, 1f, t);
            skyboxMat.SetFloat("_StarIntensity", Mathf.Lerp(starIntensityMin, starIntensityMax, starRamp));
        }

        // Ambient (Environment Lighting) — independent of skybox brightness now.
        RenderSettings.ambientSkyColor = Color.Lerp(ambientSkyDay, ambientSkyNight, t);
        RenderSettings.ambientEquatorColor = Color.Lerp(ambientEquatorDay, ambientEquatorNight, t);
        RenderSettings.ambientGroundColor = Color.Lerp(ambientGroundDay, ambientGroundNight, t);

        // Fog
        if (controlFog) {
            RenderSettings.fogColor = Color.Lerp(fogColorDay, fogColorNight, t);
            RenderSettings.fogDensity = Mathf.Lerp(fogDensityDay, fogDensityNight, t);
        }

        // Post-processing (Volume overrides), if assigned/present on the profile.
        if (colorAdjustments != null) colorAdjustments.postExposure.value = Mathf.Lerp(dayPostExposure, nightPostExposure, t);
        if (vignette != null) vignette.intensity.value = Mathf.Lerp(dayVignetteIntensity, nightVignetteIntensity, t);
        if (bloom != null) bloom.threshold.value = Mathf.Lerp(dayBloomThreshold, nightBloomThreshold, t);

        PushLightGlobals();
    }

    // The shader was written against Built-in RP globals (_WorldSpaceLightPos0,
    // _LightColor0), which URP does not populate automatically. Push them
    // manually every time the light changes so the shader's sun/moon disc
    // and lighting stay in sync with the actual networked time.
    private void PushLightGlobals() {
        Vector3 dir = globalLight.transform.forward;
        Shader.SetGlobalVector("_WorldSpaceLightPos0", new Vector4(-dir.x, -dir.y, -dir.z, 0f));
        Shader.SetGlobalColor("_LightColor0", globalLight.color * globalLight.intensity);
    }

    // ---- 12-hour formatting: "12:30AM", "9:00PM" ----
    public string GetFormattedTime() {
        int h = netHours.Value;
        int m = netMinutes.Value;

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