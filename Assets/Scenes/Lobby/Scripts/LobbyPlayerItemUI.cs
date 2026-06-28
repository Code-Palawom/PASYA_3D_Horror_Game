using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One row in the Lobby player list — used in both
// LobbyWorldCanvasController (world-space) and LobbyUI (old screen-space list).
// Shows player name and a ready indicator (checkmark / cross or color change).
public class LobbyPlayerItemUI : MonoBehaviour {
    [SerializeField] TMP_Text nameLabel;
    [SerializeField] Image readyIndicator;     // optional dot/icon
    [SerializeField] TMP_Text readyLabel;          // optional "✓" / "✗" text
    [SerializeField] Color readyColor = Color.green;
    [SerializeField] Color notReadyColor = Color.grey;

    public void Setup(string playerName, bool isReady = false) {
        nameLabel.text = playerName;
        SetReady(isReady);
    }

    public void SetReady(bool isReady) {
        if (readyIndicator != null)
            readyIndicator.color = isReady ? readyColor : notReadyColor;

        if (readyLabel != null) {
            readyLabel.text = isReady ? "Ready" : "-";
            readyLabel.color = isReady ? readyColor : notReadyColor;
        }
    }
}