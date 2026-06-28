using TMPro;
using UnityEngine;

// One row in the Lobby player list — used in both
// LobbyWorldCanvasController (world-space) and LobbyUI (old screen-space list).
// Shows player name and a ready indicator (checkmark / cross or color change).
public class MenuPlayerItemUI : MonoBehaviour {
    [SerializeField] TMP_Text nameLabel;

    public void Setup(string playerName) {
        nameLabel.text = playerName;
    }
}