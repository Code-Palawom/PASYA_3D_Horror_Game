using UnityEngine;

// Freezes or restores player input while the quiz canvas is open.
// Hook into your actual input/movement system here.
public class GameManager : MonoBehaviour {
    public static GameManager Instance { get; private set; }

    void Awake() => Instance = this;

    public void SetPlayerInputEnabled(bool enabled) {
        var localPlayer = FindLocalPlayer();
        if (localPlayer == null) return;

        if (localPlayer.TryGetComponent<InteractionController>(out var ic))
            ic.enabled = enabled;

        // Add your movement controller disable here too
        // e.g. localPlayer.GetComponent<PlayerMovement>().enabled = enabled;
    }

    GameObject FindLocalPlayer() {
        return GameObject.FindWithTag("LocalPlayer");
    }
}