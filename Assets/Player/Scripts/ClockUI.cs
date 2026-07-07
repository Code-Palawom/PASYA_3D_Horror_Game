using Unity.Netcode;
using UnityEngine;

public class ClockUI : NetworkBehaviour {
    [SerializeField] private TMPro.TMP_Text clockText;
    [SerializeField] private TMPro.TMP_Text dayText;

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
        if (clockText != null) clockText.text = TimeManager.Instance.GetFormattedTime();
        if (dayText != null) dayText.text = TimeManager.Instance.GetFormattedDay();
    }
}