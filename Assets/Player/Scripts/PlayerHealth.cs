using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHealth : NetworkBehaviour {
    [Header("Refs")]
    [SerializeField] private JumpscareUI jumpscareUI;
    [SerializeField] private Player player; // existing component, exposes TeleportClientRpc(pos, rot)

    [Header("Visibility while jumpscared")]
    [SerializeField] private Collider[] collidersToHide;

    [Header("Hearts")]
    [SerializeField] private HeartsUI heartsUI;
    [SerializeField] private int maxHearts = 3;

    [Header("Spectator UI")]
    [SerializeField] private GameObject spectatorUIRoot; // panel with the buttons + name text, shown only once eliminated
    [SerializeField] private Button nextPlayerButton;
    [SerializeField] private Button previousPlayerButton;
    [SerializeField] private TMP_Text spectateTargetNameText;
    [SerializeField] private GameObject[] objectsToHideOnSpectate; // HUD, inventory panel, etc — hidden once eliminated
    // Player.SetCamera uses 0/10 for its own POV toggle — these stay well
    // outside that range so a spectated vcam can never tie with a normal one.
    private const int SpectateActivePriority = 100;
    private const int SpectateInactivePriority = -10;

    public int currentHearts;
    public bool IsEliminated { get; private set; }

    private bool isInSequence;
    private SkinnedMeshRenderer[] renderersToHide;

    // All spawned PlayerHealth instances on this client — used so an
    // eliminated local player can cycle through everyone else's vcams.
    private static readonly List<PlayerHealth> AllPlayers = new();
    private int _spectateIndex = -1;
    private PlayerHealth _currentSpectateTarget;

    private void Awake() {
        renderersToHide = GetComponentsInChildren<SkinnedMeshRenderer>();
        currentHearts = maxHearts;
    }

    public override void OnNetworkSpawn() {
        AllPlayers.Add(this);

        if (IsOwner) {
            if (nextPlayerButton != null) nextPlayerButton.onClick.AddListener(() => CycleSpectateTarget(1));
            if (previousPlayerButton != null) previousPlayerButton.onClick.AddListener(() => CycleSpectateTarget(-1));
        }
    }

    public override void OnNetworkDespawn() {
        AllPlayers.Remove(this);

        if (IsOwner) {
            if (nextPlayerButton != null) nextPlayerButton.onClick.RemoveAllListeners();
            if (previousPlayerButton != null) previousPlayerButton.onClick.RemoveAllListeners();
        }
    }

    private void Update() {
        if (!IsOwner || !IsEliminated) return;

        // Target died too while we were watching them — move on automatically.
        if (_currentSpectateTarget != null && _currentSpectateTarget.IsEliminated)
            CycleSpectateTarget(1);
    }

    public void ApplyJumpscareHit(string enemyType) {
        if (isInSequence || IsEliminated) return; // already dying, or already out — ignore re-entrant hits

        StartCoroutine(JumpscareSequence(enemyType));
    }

    private IEnumerator JumpscareSequence(string enemyType) {
        isInSequence = true;

        // Phase 1: jumpscare + death message (owner only), player vanishes
        // for everyone the moment the hit lands.
        jumpscareUI.TriggerJumpscareRpc(enemyType);
        SetVisibilityClientRpc(false);
        yield return new WaitForSeconds(3f);

        bool willBeEliminated = currentHearts - 1 <= 0;

        if (willBeEliminated) {
            // Same pacing as the respawn-location beat, but no respawn —
            // the player goes straight to spectating instead.
            yield return new WaitForSeconds(3f);
            Eliminate();
            yield break; // isInSequence intentionally left true — no re-entry via ApplyJumpscareHit
        }

        // Phase 2: teleport via the existing client-authoritative Player
        // RPC, then reveal — player becomes visible again at the new spot
        // for everyone right as the respawn-location countdown starts.
        Vector3 respawnPos = RespawnManager.Instance.GetRandomRespawnPoint(out Quaternion respawnRot);
        jumpscareUI.ShowRespawnLocationRpc(respawnPos, respawnRot);

        yield return new WaitForSeconds(3f);
        heartsUI.heartsChanged(currentHearts, currentHearts - 1);
        currentHearts--;
        isInSequence = false;
    }

    private async void Eliminate() {
        heartsUI.heartsChanged(currentHearts, 0);
        currentHearts = 0;
        IsEliminated = true;

        player.SetSpectator(true); // TODO: point this at whatever disables movement/input on your Player script

        if (player.FirstPersonPOV != null) player.FirstPersonPOV.Priority = SpectateInactivePriority;
        if (player.ThirdPersonPOV != null) player.ThirdPersonPOV.Priority = SpectateInactivePriority;
        _spectateIndex = -1;
        if (spectatorUIRoot != null) spectatorUIRoot.SetActive(true);
        foreach (var obj in objectsToHideOnSpectate)
            if (obj != null) obj.SetActive(false);
        CycleSpectateTarget(1); // picks the first alive player

        RecordEliminationRpc(); // let the server flag it too, for other players / results screen
        await AuthManager.Instance.RecordEliminationAsync();
    }

    // Cycles this (eliminated, local) player's spectate target among
    // currently-alive players by swapping Cinemachine vcam priorities.
    // Mirrors whichever of the target's two vcams (first/third person) is
    // currently their active one, and keeps following it if they toggle
    // mid-spectate. direction: +1 = next, -1 = previous.
    public void CycleSpectateTarget(int direction) {
        if (!IsOwner || !IsEliminated) return;

        var alive = AllPlayers.Where(p => p != this && !p.IsEliminated && p.player != null
            && (p.player.FirstPersonPOV != null || p.player.ThirdPersonPOV != null)).ToList();
        if (alive.Count == 0) return;

        if (_currentSpectateTarget != null) StopSpectating(_currentSpectateTarget);

        _spectateIndex = ((_spectateIndex + direction) % alive.Count + alive.Count) % alive.Count;
        _currentSpectateTarget = alive[_spectateIndex];
        StartSpectating(_currentSpectateTarget);

        if (spectateTargetNameText != null) spectateTargetNameText.text = GetDisplayName(_currentSpectateTarget);
    }

    // Looks up the target's display name via GameSessionManager.Players
    // (matched by OwnerClientId) rather than duplicating name storage here.
    private static string GetDisplayName(PlayerHealth target) {
        if (GameSessionManager.Instance == null) return "Player";

        foreach (var info in GameSessionManager.Instance.Players) {
            if (info.ClientId == target.OwnerClientId) return info.PlayerName.ToString();
        }
        return "Player";
    }

    private void StartSpectating(PlayerHealth target) {
        ApplySpectateCamera(target, target.player.IsFirstPersonActive.Value);
        target.player.IsFirstPersonActive.OnValueChanged += OnSpectateTargetViewChanged;
    }

    private void StopSpectating(PlayerHealth target) {
        target.player.IsFirstPersonActive.OnValueChanged -= OnSpectateTargetViewChanged;
        if (target.player.FirstPersonPOV != null) target.player.FirstPersonPOV.Priority = SpectateInactivePriority;
        if (target.player.ThirdPersonPOV != null) target.player.ThirdPersonPOV.Priority = SpectateInactivePriority;
    }

    // Target switched their own first/third view mid-spectate — follow it.
    private void OnSpectateTargetViewChanged(bool previous, bool current) {
        if (_currentSpectateTarget != null) ApplySpectateCamera(_currentSpectateTarget, current);
    }

    private void ApplySpectateCamera(PlayerHealth target, bool firstPerson) {
        var active = firstPerson ? target.player.FirstPersonPOV : target.player.ThirdPersonPOV;
        var inactive = firstPerson ? target.player.ThirdPersonPOV : target.player.FirstPersonPOV;
        if (active != null) active.Priority = SpectateActivePriority;
        if (inactive != null) inactive.Priority = SpectateInactivePriority;
    }

    [Rpc(SendTo.Server)]
    private void RecordEliminationRpc(RpcParams rpcParams = default) {
        GameSessionManager.Instance.RecordElimination(rpcParams.Receive.SenderClientId);
    }

    public void RestoreVisibility() {
        SetVisibilityClientRpc(true);
    }

    // Broadcast to everyone — during the jumpscare the victim should be
    // invisible and non-solid for all players, not just hidden on their
    // own screen.
    [ClientRpc]
    private void SetVisibilityClientRpc(bool visible) {
        foreach (var r in renderersToHide)
            if (r != null) r.enabled = visible;

        foreach (var c in collidersToHide)
            if (c != null) c.enabled = visible;
    }
}