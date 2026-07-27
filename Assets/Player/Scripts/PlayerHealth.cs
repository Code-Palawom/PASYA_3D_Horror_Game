using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerHealth : NetworkBehaviour {
    [Header("Refs")]
    [SerializeField] private JumpscareUI jumpscareUI;
    [SerializeField] private Player player; // existing component, exposes TeleportClientRpc(pos, rot)

    [Header("Visibility while jumpscared")]
    [SerializeField] private Collider[] collidersToHide;

    [Header("Hearts")]
    [SerializeField] private int maxHearts = 3;

    public NetworkVariable<int> CurrentHearts = new(
        3, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private bool isInSequence;
    private SkinnedMeshRenderer[] renderersToHide;

    private void Awake() {
        renderersToHide = GetComponentsInChildren<SkinnedMeshRenderer>();
    }

    public override void OnNetworkSpawn() {
        if (IsServer) CurrentHearts.Value = maxHearts;
    }

    // Server-only entry point. Called by EnemyController.TickAttack() when
    // this player is the attack target. Removes one heart, runs the
    // jumpscare -> death message -> teleport -> respawn-reveal sequence,
    // then unlocks the player.
    public void ApplyJumpscareHit(string enemyType) {
        if (!IsServer) return;
        if (isInSequence) return; // already dying, ignore re-entrant hits
        if (CurrentHearts.Value <= 0) return;

        StartCoroutine(JumpscareSequence(enemyType));
    }

    private IEnumerator JumpscareSequence(string enemyType) {
        isInSequence = true;

        // Phase 1: jumpscare + death message (owner only), player vanishes
        // for everyone the moment the hit lands.
        jumpscareUI.TriggerJumpscareRpc(enemyType);
        SetVisibilityClientRpc(false);
        yield return new WaitForSeconds(3f);

        // Phase 2: teleport via the existing client-authoritative Player
        // RPC, then reveal — player becomes visible again at the new spot
        // for everyone right as the respawn-location countdown starts.
        Vector3 respawnPos = RespawnManager.Instance.GetRandomRespawnPoint(out Quaternion respawnRot);
        jumpscareUI.ShowRespawnLocationRpc(respawnPos, respawnRot);

        yield return new WaitForSeconds(3f);

        jumpscareUI.EndJumpscareRpc();
        SetVisibilityClientRpc(true);
        CurrentHearts.Value = Mathf.Max(0, CurrentHearts.Value - 1);
        isInSequence = false;

        if (CurrentHearts.Value <= 0) {
            // Out of hearts — hook up game-over/spectate/elimination flow here
            // once that system exists. Left intentionally unimplemented.
            Debug.Log($"[PlayerHealth] {OwnerClientId} is out of hearts.");
        }
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