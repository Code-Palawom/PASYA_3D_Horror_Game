using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One row in the Join tab's room list table.
// Click to select — populates the detail panel on the right (RoomDetailPanelUI).
public class LobbyListItemUI : MonoBehaviour {
    [SerializeField] TMP_Text roomNameLabel;
    [SerializeField] TMP_Text levelNameLabel;
    [SerializeField] TMP_Text playerCountLabel;
    [SerializeField] PingDisplayUI pingDisplay;      // bars + ms label
    [SerializeField] Button selectButton;
    [SerializeField] Image background;
    [SerializeField] Color normalColor = Color.white;
    [SerializeField] Color selectedColor = new Color(0.3f, 0.6f, 1f);

    private DiscoveredHost _host;

    public void Setup(DiscoveredHost host, string levelDisplayName, Action<DiscoveredHost> onSelected) {
        _host = host;
        roomNameLabel.text = host.HostName;
        levelNameLabel.text = levelDisplayName;
        playerCountLabel.text = host.PlayerCount.ToString();
        pingDisplay?.SetPing(host.PingMs);

        if (background != null) background.color = normalColor;

        selectButton.onClick.RemoveAllListeners();
        selectButton.onClick.AddListener(() => onSelected?.Invoke(_host));
    }

    public void SetSelected(bool selected) {
        if (background != null)
            background.color = selected ? selectedColor : normalColor;
    }
}