using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class TimeManager : NetworkBehaviour {
    public static TimeManager Instance { get; private set; }

    [SerializeField] private Texture2D skyboxNight;
    [SerializeField] private Texture2D skyboxSunrise;
    [SerializeField] private Texture2D skyboxDay;
    [SerializeField] private Texture2D skyboxSunset;

    [SerializeField] private Gradient gradientNightToSunrise;
    [SerializeField] private Gradient gradientSunriseToDay;
    [SerializeField] private Gradient gradientDayToSunset;
    [SerializeField] private Gradient gradientSunsetToNight;

    [SerializeField] private Light globalLight;

    [Header("Time Speed")]
    [Tooltip("How many real-world minutes a full 24-hour in-game day takes.")]
    [SerializeField] private float dayLengthInRealMinutes = 24f;

    [Tooltip("How many in-game minutes each skybox/light transition takes (e.g. 120 = 2 in-game hours).")]
    [SerializeField] private float transitionLengthInGameMinutes = 120f;

    [Header("UI (optional scene-based clock)")]
    [SerializeField] private TMPro.TMP_Text clockText;

    private float SecondsPerGameMinute => (dayLengthInRealMinutes * 60f) / 1440f;
    private float TransitionRealSeconds => transitionLengthInGameMinutes * SecondsPerGameMinute;

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
    private Coroutine skyboxRoutine;
    private Coroutine lightRoutine;

    private float absDayLengthInRealMinutes;

    // ---- Event other scripts (e.g. player prefab UI) can subscribe to ----
    public event System.Action OnTimeUpdated;

    //private void Awake() {
    //    if (Instance != null && Instance != this) {
    //        Destroy(gameObject);
    //        return;
    //    }
    //    Instance = this;
    //}

    public override void OnNetworkSpawn() {
        Instance = this;

        absDayLengthInRealMinutes = dayLengthInRealMinutes;

        netHours.OnValueChanged += OnHoursChanged;
        netMinutes.OnValueChanged += OnMinutesChanged;
        netDays.OnValueChanged += (_, _) => UpdateClockUI();

        // Snap instantly to correct visuals — no tween for late joiners / host start
        ApplyInstantState();
        UpdateClockUI();
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

    // Stops any in-progress tween and snaps every client straight to the target hour.
    [Rpc(SendTo.ClientsAndHost)]
    private void SnapAllClientsRpc(int hour, int minute) {
        if (skyboxRoutine != null) { StopCoroutine(skyboxRoutine); skyboxRoutine = null; }
        if (lightRoutine != null) { StopCoroutine(lightRoutine); lightRoutine = null; }

        ApplyInstantStateForHour(hour);
        UpdateLightRotation();
    }

    // ---- Runs on every client (incl. host) whenever synced state changes ----
    private void OnMinutesChanged(int oldValue, int newValue) {
        UpdateLightRotation();
        UpdateClockUI();
    }

    private void OnHoursChanged(int oldValue, int newValue) {
        float t = TransitionRealSeconds;

        if (newValue == 6) {
            RestartRoutine(ref skyboxRoutine, LerpSkybox(skyboxNight, skyboxSunrise, t));
            RestartRoutine(ref lightRoutine, LerpLight(gradientNightToSunrise, 20000f, 1500f, t));
        } else if (newValue == 8) {
            RestartRoutine(ref skyboxRoutine, LerpSkybox(skyboxSunrise, skyboxDay, t));
            RestartRoutine(ref lightRoutine, LerpLight(gradientSunriseToDay, 1500f, 6500f, t));
        } else if (newValue == 18) {
            RestartRoutine(ref skyboxRoutine, LerpSkybox(skyboxDay, skyboxSunset, t));
            RestartRoutine(ref lightRoutine, LerpLight(gradientDayToSunset, 6500f, 1500f, t));
        } else if (newValue == 22) {
            RestartRoutine(ref skyboxRoutine, LerpSkybox(skyboxSunset, skyboxNight, t));
            RestartRoutine(ref lightRoutine, LerpLight(gradientSunsetToNight, 1500f, 20000f, t));
        }

        UpdateClockUI();
    }

    private void RestartRoutine(ref Coroutine slot, IEnumerator routine) {
        if (slot != null) StopCoroutine(slot);
        slot = StartCoroutine(routine);
    }

    // Absolute sun angle from current synced time — safe for late joiners, no drift
    private void UpdateLightRotation() {
        float totalMinutes = netHours.Value * 60 + netMinutes.Value;
        float angle = (totalMinutes / 1440f) * 180f;
        globalLight.transform.rotation = Quaternion.Euler(angle, -90f, 0f);
    }

    // Snaps skybox + light to the correct phase instantly for a given hour, no tween.
    private void ApplyInstantStateForHour(int h) {
        Texture2D tex;
        Gradient g;
        float temp;

        if (h >= 6 && h < 8) { tex = skyboxSunrise; g = gradientNightToSunrise; temp = 1500f; } else if (h >= 8 && h < 18) { tex = skyboxDay; g = gradientSunriseToDay; temp = 6500f; } else if (h >= 18 && h < 22) { tex = skyboxSunset; g = gradientDayToSunset; temp = 1500f; } else { tex = skyboxNight; g = gradientSunsetToNight; temp = 20000f; }

        RenderSettings.skybox.SetTexture("_Texture1", tex);
        RenderSettings.skybox.SetFloat("_Blend", 0);
        globalLight.color = g.Evaluate(1f);
        globalLight.colorTemperature = temp;
        RenderSettings.fogColor = globalLight.color;
    }

    // Snaps skybox + light to the correct phase instantly, no tween — used on spawn
    private void ApplyInstantState() {
        ApplyInstantStateForHour(netHours.Value);
        UpdateLightRotation();
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

    private IEnumerator LerpSkybox(Texture2D a, Texture2D b, float time) {
        RenderSettings.skybox.SetTexture("_Texture1", a);
        RenderSettings.skybox.SetTexture("_Texture2", b);
        RenderSettings.skybox.SetFloat("_Blend", 0);
        for (float i = 0; i < time; i += Time.deltaTime) {
            RenderSettings.skybox.SetFloat("_Blend", i / time);
            yield return null;
        }
        RenderSettings.skybox.SetTexture("_Texture1", b);
    }

    // startTemp/endTemp let each phase transition use its own Kelvin range,
    // while keeping the same t*t easing curve used everywhere else.
    private IEnumerator LerpLight(Gradient lightGradient, float startTemp, float endTemp, float time) {
        for (float i = 0; i < time; i += Time.deltaTime) {
            float t = i / time;
            float easedT = t * t;
            globalLight.color = lightGradient.Evaluate(t);
            globalLight.colorTemperature = Mathf.Lerp(startTemp, endTemp, easedT);
            RenderSettings.fogColor = globalLight.color;
            yield return null;
        }
        globalLight.colorTemperature = endTemp;
    }
}