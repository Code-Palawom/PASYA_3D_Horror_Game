using Unity.Netcode;
using UnityEngine;

public class ClockUI : NetworkBehaviour {
    [SerializeField] private TMPro.TMP_Text timeText;

    private TimeManager _subscribedTo;

    public override void OnNetworkSpawn() {
        if (!IsOwner) {
            enabled = false; // only the local player's own HUD needs to show this
            return;
        }

        // Covers the case where this object already existed (e.g. carried over
        // from the Lobby scene via DontDestroyOnLoad) and a TimeManager only
        // shows up later, in a scene this object won't re-spawn in.
        TimeManager.OnAnyTimeManagerReady += HandleTimeManagerReady;

        // Also handle the normal case: a TimeManager already exists right now.
        if (TimeManager.Instance != null) HandleTimeManagerReady(TimeManager.Instance);
    }

    public override void OnNetworkDespawn() {
        TimeManager.OnAnyTimeManagerReady -= HandleTimeManagerReady;
        Unsubscribe();
    }

    private void HandleTimeManagerReady(TimeManager tm) {
        if (_subscribedTo == tm) return; // already bound to this one

        Unsubscribe();
        _subscribedTo = tm;
        tm.OnTimeUpdated += Refresh;
        Refresh(); // show correct time/day immediately, don't wait for next tick
    }

    private void Unsubscribe() {
        if (_subscribedTo != null) _subscribedTo.OnTimeUpdated -= Refresh;
        _subscribedTo = null;
    }

    private void Refresh() {
        timeText.text = $"{TimeManager.Instance.GetFormattedDay()} | {TimeManager.Instance.GetFormattedTime()}";
    }
}