using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class PlayerNameDisplay : NetworkBehaviour {
    [Header("References")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Transform nameTagTransform; // the World Space Canvas
    [SerializeField] private RectTransform nameTagBackground; // the World Space Canvas
    [SerializeField] private VoiceActivityIndicator voiceIndicator; // mic icon, same name tag

    // Static registry of all non-owner PlayerNameDisplay instances.
    // Use this to toggle name tags without a scene scan.
    // Example: foreach (var p in PlayerNameDisplay.All) p.SetNameTagVisible(show);
    public static readonly List<PlayerNameDisplay> All = new();

    private Camera _mainCamera;
    private bool nameTagsEnabled = true;

    public override void OnNetworkSpawn() {
        if (IsOwner) {
            nameTagTransform.gameObject.SetActive(false);

            if (!GameModeManager.Instance.IsRelayMode) return;
            VivoxManager.Instance.OnLocalMuteChanged += HandleLocalMuteChanged;
            HandleLocalMuteChanged(VivoxManager.Instance.IsLocallyMuted, false);

            // Connection state - distinct from mute. IsMicOn reflects whether
            // this client has actually joined a Vivox channel and can transmit
            // at all, regardless of whether they've chosen to mute.
            VivoxManager.Instance.OnChannelJoined += HandleChannelJoined;
            VivoxManager.Instance.OnChannelLeft += HandleChannelLeft;
            HandleMicOnChanged(!string.IsNullOrEmpty(VivoxManager.Instance.CurrentChannelName));
            return;
        }

        // Register in static list
        All.Add(this);

        // Read setting once on spawn
        nameTagsEnabled = SettingsManager.Instance == null || SettingsManager.Instance.Current.showNameTags;

        nameTagTransform.gameObject.SetActive(nameTagsEnabled);

        // Look up name from session manager
        ApplyFromSession();

        // Fallback: in case this object spawns before the NetworkList is populated
        GameSessionManager.Instance.Players.OnListChanged += OnPlayersChanged;
    }

    public override void OnNetworkDespawn() {
        All.Remove(this);

        if (GameSessionManager.Instance != null)
            GameSessionManager.Instance.Players.OnListChanged -= OnPlayersChanged;

        if (VivoxManager.Instance != null && GameModeManager.Instance.IsRelayMode) {
            VivoxManager.Instance.OnLocalMuteChanged -= HandleLocalMuteChanged;
            VivoxManager.Instance.OnChannelJoined -= HandleChannelJoined;
            VivoxManager.Instance.OnChannelLeft -= HandleChannelLeft;
        }
    }

    private void HandleLocalMuteChanged(bool muted, bool forced) {
        if (GameSessionManager.Instance != null)
            GameSessionManager.Instance.SetMutedRpc(muted);
    }

    private void HandleChannelJoined(string channelName) => HandleMicOnChanged(true);
    private void HandleChannelLeft(string channelName) => HandleMicOnChanged(false);

    private void HandleMicOnChanged(bool micOn) {
        if (GameSessionManager.Instance != null)
            GameSessionManager.Instance.SetMicOnRpc(micOn);
    }

    // Call this from your settings save/apply logic to toggle all name tags.
    // Example: foreach (var p in PlayerNameDisplay.All) p.SetNameTagVisible(show);
    public void SetNameTagVisible(bool visible) {
        if (IsOwner) return;
        nameTagsEnabled = visible;
        nameTagTransform.gameObject.SetActive(visible);
    }

    private void ApplyFromSession() {
        if (GameSessionManager.Instance == null) {
            Debug.Log("[VoiceSync] ApplyFromSession: GameSessionManager.Instance is null");
            return;
        }

        foreach (var player in GameSessionManager.Instance.Players) {
            if (player.ClientId == OwnerClientId) {
                Debug.Log($"[VoiceSync] ApplyFromSession found player {player.ClientId}: IsMuted={player.IsMuted}, IsMicOn={player.IsMicOn}");
                ApplyPlayerInfo(player);
                return;
            }
        }

        Debug.Log($"[VoiceSync] ApplyFromSession: no matching player for OwnerClientId={OwnerClientId} in list of {GameSessionManager.Instance.Players.Count}");
    }

    private void OnPlayersChanged(NetworkListEvent<PlayerLobbyInfo> changeEvent) {
        Debug.Log($"[VoiceSync] OnPlayersChanged: type={changeEvent.Type}, valueClientId={changeEvent.Value.ClientId}, IsMuted={changeEvent.Value.IsMuted}, IsMicOn={changeEvent.Value.IsMicOn}, watching OwnerClientId={OwnerClientId}");
        if (changeEvent.Value.ClientId == OwnerClientId)
            ApplyPlayerInfo(changeEvent.Value);
    }

    private void ApplyPlayerInfo(PlayerLobbyInfo player) {
        string playerName = player.PlayerName.ToString();
        nameText.text = playerName;

        voiceIndicator.DisplayName = playerName; // matches Vivox DisplayName set at login
        voiceIndicator.IsMuted = player.IsMuted;
        voiceIndicator.IsMicOn = player.IsMicOn;

        LayoutRebuilder.ForceRebuildLayoutImmediate(nameTagBackground);
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