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

    // ---- Event other scripts (e.g. player prefab UI) can subscribe to ----
    public event System.Action OnTimeUpdated;

    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public override void OnNetworkSpawn() {
        netHours.OnValueChanged += OnHoursChanged;
        netMinutes.OnValueChanged += (_, _) => { UpdateLightRotation(); UpdateClockUI(); };
        netDays.OnValueChanged += (_, _) => UpdateClockUI();

        ApplyInstantState();
        UpdateClockUI();
    }

    public override void OnNetworkDespawn() {
        if (Instance == this) Instance = null;
        netHours.OnValueChanged -= OnHoursChanged;
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

    // ---- Runs on every client (incl. host) whenever synced state changes ----
    private void OnHoursChanged(int oldValue, int newValue) {
        float t = TransitionRealSeconds;

        if (newValue == 6) {
            RestartRoutine(ref skyboxRoutine, LerpSkybox(skyboxNight, skyboxSunrise, t));
            RestartRoutine(ref lightRoutine, LerpLight(gradientNightToSunrise, t));
        } else if (newValue == 8) {
            RestartRoutine(ref skyboxRoutine, LerpSkybox(skyboxSunrise, skyboxDay, t));
            RestartRoutine(ref lightRoutine, LerpLight(gradientSunriseToDay, t));
        } else if (newValue == 18) {
            RestartRoutine(ref skyboxRoutine, LerpSkybox(skyboxDay, skyboxSunset, t));
            RestartRoutine(ref lightRoutine, LerpLight(gradientDayToSunset, t));
        } else if (newValue == 22) {
            RestartRoutine(ref skyboxRoutine, LerpSkybox(skyboxSunset, skyboxNight, t));
            RestartRoutine(ref lightRoutine, LerpLight(gradientSunsetToNight, t));
        }
    }

    private void RestartRoutine(ref Coroutine slot, IEnumerator routine) {
        if (slot != null) StopCoroutine(slot);
        slot = StartCoroutine(routine);
    }

    // Absolute sun angle from current synced time — safe for late joiners, no drift
    private void UpdateLightRotation() {
        float totalMinutes = netHours.Value * 60 + netMinutes.Value;
        float angle = (totalMinutes / 1440f) * 360f;
        globalLight.transform.rotation = Quaternion.Euler(angle, -90f, 0f);
    }

    // Snaps skybox + light to the correct phase instantly, no tween — used on spawn
    private void ApplyInstantState() {
        int h = netHours.Value;
        Texture2D tex;
        Gradient g;

        if (h >= 6 && h < 8) { tex = skyboxSunrise; g = gradientNightToSunrise; } else if (h >= 8 && h < 18) { tex = skyboxDay; g = gradientSunriseToDay; } else if (h >= 18 && h < 22) { tex = skyboxSunset; g = gradientDayToSunset; } else { tex = skyboxNight; g = gradientSunsetToNight; }

        RenderSettings.skybox.SetTexture("_Texture1", tex);
        RenderSettings.skybox.SetFloat("_Blend", 0);
        globalLight.color = g.Evaluate(1f);
        RenderSettings.fogColor = globalLight.color;
        UpdateLightRotation();
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

    private IEnumerator LerpLight(Gradient lightGradient, float time) {
        for (float i = 0; i < time; i += Time.deltaTime) {
            globalLight.color = lightGradient.Evaluate(i / time);
            float t = i / time;
            float easedT = t * t;
            globalLight.colorTemperature = Mathf.Lerp(1500f, 20000f, easedT);
            RenderSettings.fogColor = globalLight.color;
            yield return null;
        }
    }

    // ---- 12-hour formatting: "12:30AM", "9:00PM" ----
    public string GetFormattedTime() {
        int h = netHours.Value;
        int m = netMinutes.Value;

        string period = h >= 12 ? "PM" : "AM";
        int displayHour = h % 12;
        if (displayHour == 0) displayHour = 12;

        return $"{displayHour}:{m:00}{period}";
    }

    // ---- "Day 5" formatting ----
    public string GetFormattedDay() {
        return $"Day {DisplayDay}";
    }

    private void UpdateClockUI() {
        if (clockText != null) clockText.text = GetFormattedTime();
        OnTimeUpdated?.Invoke();
    }

    // ---- Admin: set absolute time ----
    [ServerRpc(RequireOwnership = false)]
    public void SetTimeServerRpc(int hour, int minute) {
        hour = Mathf.Clamp(hour, 0, 23);
        minute = Mathf.Clamp(minute, 0, 59);

        netMinutes.Value = minute;
        if (netHours.Value != hour) netHours.Value = hour;
        else OnHoursChanged(hour, hour); // force re-check even if hour didn't change
    }

    // ---- Admin: skip forward by a duration ----
    [ServerRpc(RequireOwnership = false)]
    public void SkipTimeServerRpc(int hoursToSkip, int minutesToSkip) {
        int totalMinutes = netHours.Value * 60 + netMinutes.Value + hoursToSkip * 60 + minutesToSkip;
        int daysToAdd = totalMinutes / 1440;
        totalMinutes %= 1440;
        if (totalMinutes < 0) { totalMinutes += 1440; daysToAdd -= 1; }

        int newHour = totalMinutes / 60;
        int newMinute = totalMinutes % 60;

        if (daysToAdd != 0) netDays.Value += daysToAdd;
        netMinutes.Value = newMinute;
        netHours.Value = newHour;

        ApplyInstantStateClientRpc(); // ensures visuals always match, even on no-op hour changes (e.g. exact 24h skip)
    }

    [ClientRpc]
    private void ApplyInstantStateClientRpc() {
        ApplyInstantState();
    }
}