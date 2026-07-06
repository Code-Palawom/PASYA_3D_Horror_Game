using TMPro;
using Unity.Netcode;
using UnityEngine;

public class TimeController : NetworkBehaviour {
    [SerializeField] private TextMeshProUGUI timeDisplay;

    private TimeManager timeManager;

    public override void OnNetworkSpawn() {
        // Wait until the TimeManager exists (scene object is already spawned)
        timeManager = TimeManager.Instance;

        if (timeManager == null) Debug.LogError("TimeManager instance not found!");
    }

    private void Update() {
        if (timeManager != null) {
            timeDisplay.text = $"{timeManager.Days:D2}:{timeManager.Hours:D2}:{timeManager.Minutes:D2}";
        }
    }
}