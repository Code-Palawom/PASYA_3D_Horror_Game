using System.Collections.Generic;
using Unity.Netcode;
using TMPro;
using UnityEngine;

public class PlayerNameDisplay : NetworkBehaviour {
    [Header("References")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Transform nameTagTransform; // the World Space Canvas

    /// Static registry of all non-owner PlayerNameDisplay instances.
    /// Use this to toggle name tags without a scene scan.
    /// Example: foreach (var p in PlayerNameDisplay.All) p.SetNameTagVisible(show);
    public static readonly List<PlayerNameDisplay> All = new();

    private Camera _mainCamera;
    private bool nameTagsEnabled = true;

    public override void OnNetworkSpawn() {
        if (IsOwner) {
            nameTagTransform.gameObject.SetActive(false);
            return;
        }

        // Register in static list
        All.Add(this);

        // Read setting once on spawn
        nameTagsEnabled = SettingsManager.Instance == null
            || SettingsManager.Instance.Current.showNameTags;

        nameTagTransform.gameObject.SetActive(nameTagsEnabled);

        // Look up name from session manager
        ApplyNameFromSession();

        // Fallback: in case this object spawns before the NetworkList is populated
        GameSessionManager.Instance.Players.OnListChanged += OnPlayersChanged;
    }

    public override void OnNetworkDespawn() {
        All.Remove(this);

        if (GameSessionManager.Instance != null)
            GameSessionManager.Instance.Players.OnListChanged -= OnPlayersChanged;
    }

    /// Call this from your settings save/apply logic to toggle all name tags.
    /// Example: foreach (var p in PlayerNameDisplay.All) p.SetNameTagVisible(show);
    public void SetNameTagVisible(bool visible) {
        if (IsOwner) return;
        nameTagsEnabled = visible;
        nameTagTransform.gameObject.SetActive(visible);
    }

    private void ApplyNameFromSession() {
        if (GameSessionManager.Instance == null) return;

        foreach (var player in GameSessionManager.Instance.Players) {
            if (player.ClientId == OwnerClientId) {
                nameText.text = player.PlayerName.ToString();
                return;
            }
        }
    }

    private void OnPlayersChanged(NetworkListEvent<PlayerLobbyInfo> changeEvent) {
        if (changeEvent.Value.ClientId == OwnerClientId)
            nameText.text = changeEvent.Value.PlayerName.ToString();
    }

    private void LateUpdate() {
        if (IsOwner) return;
        if (!nameTagsEnabled) return;

        if (nameTagTransform == null) return;

        // Lazy-resolve camera in case it wasn't ready on spawn
        if (_mainCamera == null)
            _mainCamera = Camera.main;

        if (_mainCamera == null) return;

        // Billboard — always face the camera
        nameTagTransform.rotation = _mainCamera.transform.rotation;
    }
}