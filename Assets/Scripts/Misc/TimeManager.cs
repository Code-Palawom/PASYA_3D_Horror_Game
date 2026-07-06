using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TimeManager : MonoBehaviour {
    [SerializeField] private Texture2D skyboxNight;
    [SerializeField] private Texture2D skyboxSunrise;
    [SerializeField] private Texture2D skyboxDay;
    [SerializeField] private Texture2D skyboxSunset;

    [SerializeField] private Gradient graddientNightToSunrise;
    [SerializeField] private Gradient graddientSunriseToDay;
    [SerializeField] private Gradient graddientDayToSunset;
    [SerializeField] private Gradient graddientSunsetToNight;

    [SerializeField] private Light globalLight;

    [Header("Time Speed")]
    [Tooltip("How many real-world minutes a full 24-hour in-game day takes.")]
    [SerializeField] private float dayLengthInRealMinutes = 24f;

    [Tooltip("How many in-game minutes each skybox/light transition takes (e.g. 120 = 2 in-game hours).")]
    [SerializeField] private float transitionLengthInGameMinutes = 120f;

    // Real seconds per in-game minute, derived from dayLengthInRealMinutes
    private float SecondsPerGameMinute => (dayLengthInRealMinutes * 60f) / 1440f;

    // Real seconds a transition should take, scaled to current speed
    private float TransitionRealSeconds => transitionLengthInGameMinutes * SecondsPerGameMinute;

    [SerializeField] private int minutes;

    public int Minutes {
        get { return minutes; }
        set { minutes = value; OnMinutesChange(value); }
    }

    [SerializeField] private int hours = 5;

    public int Hours {
        get { return hours; }
        set { hours = value; OnHoursChange(value); }
    }

    [SerializeField] private int days;

    public int Days {
        get { return days; }
        set { days = value; }
    }

    private float tempSecond;

    private void Start() {
        Hours = hours; // Trigger initial skybox/light setup
    }

    public void Update() {
        tempSecond += Time.deltaTime;

        if (tempSecond >= SecondsPerGameMinute) {
            Minutes += 1;
            tempSecond = 0;
        }
    }

    private void OnMinutesChange(int value) {
        globalLight.transform.Rotate(Vector3.forward, (1f / 1440f) * 360f, Space.World);

        if (value >= 60) {
            Hours++;
            minutes = 0;
        }
        if (Hours >= 24) {
            Hours = 0;
            Days++;
        }
    }

    private void OnHoursChange(int value) {
        float t = TransitionRealSeconds;

        if (value == 6) {
            StartCoroutine(LerpSkybox(skyboxNight, skyboxSunrise, t));
            StartCoroutine(LerpLight(graddientNightToSunrise, t));
        } else if (value == 8) {
            StartCoroutine(LerpSkybox(skyboxSunrise, skyboxDay, t));
            StartCoroutine(LerpLight(graddientSunriseToDay, t));
        } else if (value == 18) {
            StartCoroutine(LerpSkybox(skyboxDay, skyboxSunset, t));
            StartCoroutine(LerpLight(graddientDayToSunset, t));
        } else if (value == 22) {
            StartCoroutine(LerpSkybox(skyboxSunset, skyboxNight, t));
            StartCoroutine(LerpLight(graddientSunsetToNight, t));
        }
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