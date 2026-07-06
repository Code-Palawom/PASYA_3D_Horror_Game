using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class TimeManager : NetworkBehaviour {
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

    private float SecondsPerGameMinute => (dayLengthInRealMinutes * 60f) / 1440f;
    private float TransitionRealSeconds => transitionLengthInGameMinutes * SecondsPerGameMinute;

    // ---- Server-authoritative time state ----
    private readonly NetworkVariable<int> netMinutes = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> netHours = new NetworkVariable<int>(
        8, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> netDays = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public int Minutes => netMinutes.Value;
    public int Hours => netHours.Value;
    public int Days => netDays.Value;

    private float tempSecond;
    private Coroutine skyboxRoutine;
    private Coroutine lightRoutine;

    public static TimeManager Instance { get; private set; }

    public override void OnNetworkSpawn() {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        netHours.OnValueChanged += OnHoursChanged;
        netMinutes.OnValueChanged += OnMinutesChanged;

        // Snap instantly to correct visuals — no tween for late joiners / host start
        ApplyInstantState();
    }

    public override void OnNetworkDespawn() {
        if (Instance == this)
            Instance = null;

        netHours.OnValueChanged -= OnHoursChanged;
        netMinutes.OnValueChanged -= OnMinutesChanged;
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
    private void OnMinutesChanged(int oldValue, int newValue) {
        UpdateLightRotation();
    }

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
}