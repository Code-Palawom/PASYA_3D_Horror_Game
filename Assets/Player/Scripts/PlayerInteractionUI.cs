using TMPro;
using UnityEngine;

/// <summary>
/// Lives inside the Player prefab on the InteractionPrompt GameObject.
/// Only the local player sees this — it's part of their own PlayerCanvas.
/// </summary>
public class PlayerInteractionUI : MonoBehaviour {
    [SerializeField] GameObject promptPanel;
    [SerializeField] TMP_Text promptText;
    [SerializeField] TMP_Text keyHintText;    // optional e.g. "[E]"

    void Awake() => Hide();

    public void Show(string message, string keyHint = "E") {
        promptPanel.SetActive(true);
        promptText.text = message;

        if (keyHintText != null)
            keyHintText.text = $"[{keyHint}]";
    }

    public void Hide() => promptPanel.SetActive(false);
}