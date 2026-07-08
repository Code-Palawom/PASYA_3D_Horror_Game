using Unity.Netcode;
using UnityEngine;

public class ClockUI : NetworkBehaviour {
    [SerializeField] private TMPro.TMP_Text timeText;

    public override void OnNetworkSpawn() {
        if (!IsOwner) {
            enabled = false; // only the local player's own HUD needs to show this
            return;
        }

        if (TimeManager.Instance != null) {
            TimeManager.Instance.OnTimeUpdated += Refresh;
            Refresh(); // show correct time/day immediately, don't wait for next tick
        }
    }

    public override void OnNetworkDespawn() {
        if (TimeManager.Instance != null)
            TimeManager.Instance.OnTimeUpdated -= Refresh;
    }

    private void Refresh() {
        timeText.text = $"{TimeManager.Instance.GetFormattedDay()} | {TimeManager.Instance.GetFormattedTime()}";
    }
}