using System;
using UnityEngine;

public class TimerController : MonoBehaviour {
    public event Action OnTimeUp;

    public float TimeRemaining { get; private set; }
    public float TotalDuration { get; private set; }

    private bool _running;

    public void StartTimer(float duration) {
        TotalDuration = duration;
        TimeRemaining = duration;
        _running = true;
    }

    public void StopTimer() => _running = false;

    void Update() {
        if (!_running) return;

        TimeRemaining -= Time.deltaTime;

        if (TimeRemaining <= 0f) {
            TimeRemaining = 0f;
            _running = false;
            OnTimeUp?.Invoke();
        }
    }
}