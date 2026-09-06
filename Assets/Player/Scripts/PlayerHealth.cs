using System;
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
    // Must beat both SpectateActivePriority and the jumpscare cam's own
    // self-set 99 (from JumpscareUI.TriggerJumpscareRpc) so the spectator
    // sees the jumpscare framing regardless of what the target's own
    // client already did to that vcam's priority.
    private const int SpectateJumpscarePriority = 110;

    public int currentHearts;
    public bool IsEliminated { get; private set; }

    // Fired when this player's own jumpscare sequence starts/ends, so an
    // eliminated spectator watching this player can react (skip away to
    // another target, or show the jumpscare cam angle if no one else is
    // left to spectate).
    public event Action<PlayerHealth> JumpscareStarted;
    public event Action<PlayerHealth> JumpscareEnded;

    // Exposed so spectators can show the same jumpscare cam angle this
    // player's own client is seeing, without duplicating a reference.
    public CinemachineCamera JumpscareCam => jumpscareUI != null ? jumpscareUI.JumpscareCam : null;

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
        JumpscareStarted?.Invoke(this);

        // If this player had a quiz open, it's an instant wrong answer —
        // no side effects (handled separately from the jumpscare's own
        // consequences), just the scoring/stat miss.
        if (IsOwner) QuizManager.Instance?.ForceCloseAsWrong(gameObject);

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
        JumpscareEnded?.Invoke(this);
    }

    private void Eliminate() {
        heartsUI.heartsChanged(currentHearts, 0);
        currentHearts = 0;
        IsEliminated = true;

        // Clean up this player's own jumpscare visuals (overlay + cam)
        // without restoring gameplay UI/input/visibility, since Eliminate()
        // below immediately puts this player into spectator state instead.
        jumpscareUI.EndJumpscareForElimination();

        player.SetSpectator(true); // TODO: point this at whatever disables movement/input on your Player script

        if (player.FirstPersonPOV != null) player.FirstPersonPOV.Priority = SpectateInactivePriority;
        if (player.ThirdPersonPOV != null) player.ThirdPersonPOV.Priority = SpectateInactivePriority;
        _spectateIndex = -1;
        if (spectatorUIRoot != null) spectatorUIRoot.SetActive(true);
        foreach (var obj in objectsToHideOnSpectate)
            if (obj != null) obj.SetActive(false);
        CycleSpectateTarget(1); // picks the first alive player

        RecordEliminationRpc(); // server flags it + sends back this player's stats for the one Firestore write
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
        target.JumpscareStarted += OnTargetJumpscareStarted;
        target.JumpscareEnded += OnTargetJumpscareEnded;
    }

    private void StopSpectating(PlayerHealth target) {
        target.player.IsFirstPersonActive.OnValueChanged -= OnSpectateTargetViewChanged;
        target.JumpscareStarted -= OnTargetJumpscareStarted;
        target.JumpscareEnded -= OnTargetJumpscareEnded;

        // Remote vcams are disabled by default (NetworkSetup) — restore
        // that invariant when we stop spectating this target, not just
        // deprioritize, since a disabled vcam never competes for the brain
        // anyway but an enabled one left at a low priority is still a
        // dangling exception to how every other remote vcam behaves.
        if (target.player.FirstPersonPOV != null) {
            target.player.FirstPersonPOV.Priority = SpectateInactivePriority;
            target.player.FirstPersonPOV.enabled = false;
        }
        if (target.player.ThirdPersonPOV != null) {
            target.player.ThirdPersonPOV.Priority = SpectateInactivePriority;
            target.player.ThirdPersonPOV.enabled = false;
        }
        HideJumpscareCam(target); // in case we stopped spectating mid jumpscare-cam view
    }

    // Target switched their own first/third view mid-spectate — follow it.
    private void OnSpectateTargetViewChanged(bool previous, bool current) {
        if (_currentSpectateTarget != null) ApplySpectateCamera(_currentSpectateTarget, current);
    }

    // Target we're currently watching just got jumpscared. If there's
    // another eligible player to spectate instead, hop to them so we don't
    // sit through someone else's jumpscare; if this is the only player
    // left, show their jumpscare cam angle instead of a frozen/blank view.
    private void OnTargetJumpscareStarted(PlayerHealth target) {
        if (target != _currentSpectateTarget) return;

        bool hasOtherTarget = AllPlayers.Any(p => p != this && p != target && !p.IsEliminated
            && p.player != null && (p.player.FirstPersonPOV != null || p.player.ThirdPersonPOV != null));

        if (hasOtherTarget) CycleSpectateTarget(1);
        else ShowJumpscareCam(target);
    }

    // Target's jumpscare finished (they survived it) — if we're still
    // watching them (i.e. we showed their jumpscare cam rather than
    // cycling away), drop back to their normal POV vcam.
    private void OnTargetJumpscareEnded(PlayerHealth target) {
        if (target != _currentSpectateTarget) return;
        HideJumpscareCam(target);
        ApplySpectateCamera(target, target.player.IsFirstPersonActive.Value);
    }

    private void ShowJumpscareCam(PlayerHealth target) {
        var cam = target.JumpscareCam;
        if (cam == null) return;

        if (target.player.FirstPersonPOV != null) target.player.FirstPersonPOV.enabled = false;
        if (target.player.ThirdPersonPOV != null) target.player.ThirdPersonPOV.enabled = false;

        cam.enabled = true;
        cam.Priority = SpectateJumpscarePriority;
    }

    private void HideJumpscareCam(PlayerHealth target) {
        var cam = target.JumpscareCam;
        if (cam == null) return;

        cam.Priority = SpectateInactivePriority;
        cam.enabled = false;
    }

    private void ApplySpectateCamera(PlayerHealth target, bool firstPerson) {
        var active = firstPerson ? target.player.FirstPersonPOV : target.player.ThirdPersonPOV;
        var inactive = firstPerson ? target.player.ThirdPersonPOV : target.player.FirstPersonPOV;

        // Remote vcams start disabled (see NetworkSetup) — a disabled
        // CinemachineCamera never registers with the brain at all, so it
        // must be re-enabled here before raising its priority, and the
        // one we're switching away from disabled again to restore the
        // normal remote-player invariant.
        if (active != null) {
            active.enabled = true;
            active.Priority = SpectateActivePriority;
        }
        if (inactive != null) {
            inactive.Priority = SpectateInactivePriority;
            inactive.enabled = false;
        }
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