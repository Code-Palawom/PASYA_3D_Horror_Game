using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One row in the unified Join panel's session list.
// Handles both LAN (DiscoveredHost) and online (OnlineDiscoveredSession) entries.
// Use lanBadge / onlineBadge to visually distinguish session type in the prefab.
public class LobbyListItemUI : MonoBehaviour {
    [SerializeField] TMP_Text roomNameLabel;
    [SerializeField] TMP_Text levelNameLabel;
    [SerializeField] TMP_Text playerCountLabel;
    [SerializeField] PingDisplayUI pingDisplay;
    [SerializeField] Button selectButton;
    [SerializeField] Image background;
    [SerializeField] Color normalColor = Color.white;
    [SerializeField] Color selectedColor = new Color(0.3f, 0.6f, 1f);

    // ── Session type badges ───────────────────────────────────
    // Assign both in the prefab Inspector; Setup/SetupOnline toggle them automatically.
    [SerializeField] GameObject lanBadge;    // e.g. a wifi / network icon
    [SerializeField] GameObject onlineBadge; // e.g. a globe icon

    DiscoveredHost _host;
    OnlineDiscoveredSession _onlineSession;

    // ── LAN session ───────────────────────────────────────────
    public void Setup(DiscoveredHost host, string levelDisplayName, Action<DiscoveredHost> onSelected) {
        _host = host;
        _onlineSession = null;

        roomNameLabel.text = host.HostName;
        levelNameLabel.text = levelDisplayName;
        playerCountLabel.text = host.PlayerCount.ToString();

        pingDisplay.SetPing(host.PingMs);
        pingDisplay.gameObject.SetActive(true);

        lanBadge.SetActive(true);
        onlineBadge.SetActive(false);

        if (background != null) background.color = normalColor;

        selectButton.onClick.RemoveAllListeners();
        selectButton.onClick.AddListener(() => onSelected?.Invoke(_host));
    }

    // ── Online (Relay) session ────────────────────────────────
    public void SetupOnline(OnlineDiscoveredSession session, string levelDisplayName,
        Action<OnlineDiscoveredSession> onSelected) {
        _onlineSession = session;
        _host = null;

        roomNameLabel.text = session.HostName;
        levelNameLabel.text = levelDisplayName;
        playerCountLabel.text = $"{session.PlayerCount}/{session.MaxPlayers}";

        pingDisplay.SetPing(-1);

        lanBadge.SetActive(false);
        onlineBadge.SetActive(true);

        if (background != null) background.color = normalColor;

        selectButton.onClick.RemoveAllListeners();
        selectButton.onClick.AddListener(() => onSelected?.Invoke(_onlineSession));
    }

    public void SetSelected(bool selected) {
        if (background != null)
            background.color = selected ? selectedColor : normalColor;
    }
}