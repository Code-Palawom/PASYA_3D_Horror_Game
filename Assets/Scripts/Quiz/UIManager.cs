using UnityEngine;

// Stub — replace with your actual global UI system if you have one.
// Note: PlayerInteractionUI (per-player) handles the interact prompt now,
// this is only a placeholder for other global UI calls if needed.
public class UIManager : MonoBehaviour {
    public static UIManager Instance { get; private set; }

    void Awake() => Instance = this;

    public void ShowPrompt(string text) { /* show interact prompt UI */ }
    public void HidePrompt() { /* hide interact prompt UI */ }
}