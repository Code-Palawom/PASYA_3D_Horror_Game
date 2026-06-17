using UnityEngine;

// ScoreManager
public class ScoreManager : MonoBehaviour {
    public static ScoreManager Instance { get; private set; }

    public int TotalScore { get; private set; }

    void Awake() => Instance = this;

    public void AddScore(int amount) {
        TotalScore += amount;
        Debug.Log($"[ScoreManager] +{amount} pts → Total: {TotalScore}");
        // Hook into your score UI here
    }

    public void ResetScore() => TotalScore = 0;
}

// GameManager
public class GameManager : MonoBehaviour {
    public static GameManager Instance { get; private set; }

    void Awake() => Instance = this;

    // Freeze or restore player input while the quiz canvas is open.
    // Hook into your actual input system here (e.g. PlayerInput, CharacterController).
    public void SetPlayerInputEnabled(bool enabled) {
        // Example: find local player and toggle input
        var localPlayer = FindLocalPlayer();
        if (localPlayer == null) return;

        if (localPlayer.TryGetComponent<InteractionController>(out var ic))
            ic.enabled = enabled;

        // Add your movement controller disable here too
        // e.g. localPlayer.GetComponent<PlayerMovement>().enabled = enabled;
    }

    GameObject FindLocalPlayer() {
        // Replace with your actual local player reference strategy
        return GameObject.FindWithTag("LocalPlayer");
    }
}

// UIManager (stub — replace with your actual UI system)
public class UIManager : MonoBehaviour {
    public static UIManager Instance { get; private set; }

    void Awake() => Instance = this;

    public void ShowPrompt(string text) { /* show interact prompt UI */ }
    public void HidePrompt() { /* hide interact prompt UI */ }
}