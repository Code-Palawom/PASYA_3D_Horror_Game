using PrimeTween;
using System.Collections;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;

public class JumpscareUI : MonoBehaviour {
    [Header("Refs")]
    [SerializeField] private CanvasGroup overlayGroup;
    [SerializeField] private TMP_Text deathMessageText;
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private Player player; // existing component, exposes TeleportClientRpc(pos, rot)
    [SerializeField] private PlayerHealth playerHealth;

    [Header("Cinemachine")]
    [SerializeField] private CinemachineCamera firstPersonCam;
    [SerializeField] private CinemachineCamera thirdPersonCam;
    [SerializeField] private CinemachineCamera jumpscareCam;

    // Exposed so PlayerHealth's spectator system can show the same
    // jumpscare cam angle this player's own client is seeing, without
    // duplicating a second inspector reference.
    public CinemachineCamera JumpscareCam => jumpscareCam;

    [Header("Cinemachine Look")]
    [SerializeField] private CinemachinePanTilt panTilt;
    [SerializeField] private Transform fpYawTarget;

    [Header("Input lock while jumpscare plays")]
    [SerializeField] private MonoBehaviour[] inputScriptsToDisable; // FPS controller, look script, etc.
    [SerializeField] private GameObject gameplayUI; // action buttons / on-screen controller root

    [Header("Timing (must match PlayerHealth's WaitForSeconds calls)")]
    [SerializeField] private int respawnCountdownSeconds = 3;

    [Header("Jumpscare locations")]
    [SerializeField] private JumpscareLocationSet jumpscareLocations;


    public void TriggerJumpscareRpc(string enemyType) {
        SetInputLocked(true);
        player.IsJumpscared(true);
        if (gameplayUI != null) gameplayUI.SetActive(false);

        var entry = jumpscareLocations != null ? jumpscareLocations.GetEntry(enemyType) : null;
        if (entry == null) {
            Debug.LogWarning($"JumpscareUI: no JumpscareLocationSet entry for enemyType '{enemyType}'.");
            return;
        }

        Quaternion targetRot = Quaternion.Euler(entry.playerEulerRotation);

        // Server-authoritative teleport of the whole player object — see the
        // comment on RequestTeleportRpc if you already have a dedicated
        // teleport path (e.g. the one respawn uses) to route through instead.
        player.TeleportJumpscareClientRpc(entry.playerPosition, targetRot, true);

        jumpscareCam.Priority = 99;

        var propAnimator = JumpscarePropRegistry.Instance != null
            ? JumpscarePropRegistry.Instance.GetAnimator(entry.propId)
            : null;
        if (propAnimator != null) propAnimator.SetTrigger(entry.animationTrigger);

        deathMessageText.text = "";
        countdownText.text = "";
        overlayGroup.gameObject.SetActive(true);
        Tween.Alpha(overlayGroup, 1f, 0.25f, Ease.OutQuad);
    }

    public void ShowRespawnLocationRpc(Vector3 respawnPosition, Quaternion respawnRotation) {
        player.TeleportJumpscareClientRpc(respawnPosition, respawnRotation, false);

        if (panTilt != null)
            panTilt.TiltAxis.Value = 0f;

        deathMessageText.text = "";
        StartCoroutine(CountdownRoutine(respawnCountdownSeconds));
    }

    private IEnumerator CountdownRoutine(int seconds) {
        deathMessageText.text = "Respawning in...";
        for (int i = seconds; i > 0; i--) {
            countdownText.text = i.ToString();
            yield return new WaitForSeconds(1f);
        }
        deathMessageText.text = "";
        countdownText.text = "";

        EndJumpscare(restorePlayerState: true);
    }

    // Called when the player survives the jumpscare and respawns — fully
    // restores gameplay UI, input, visibility, and player state.
    // restorePlayerState is false when called from the elimination path
    // (see EndJumpscareForElimination), since PlayerHealth.Eliminate()
    // immediately takes over with its own spectator-mode UI/input/visibility
    // handling right after this returns — restoring gameplay state here
    // would just get overwritten a moment later.
    private void EndJumpscare(bool restorePlayerState) {
        Tween.Alpha(overlayGroup, 0f, 0.25f, Ease.OutQuad)
            .OnComplete(() => overlayGroup.gameObject.SetActive(false));
        jumpscareCam.Priority = 0;

        if (gameplayUI != null) gameplayUI.SetActive(true);
        if (restorePlayerState) {
            SetInputLocked(false);
            player.IsJumpscared(false);
            playerHealth.RestoreVisibility();
        }
    }

    // Public entry point for PlayerHealth.Eliminate() — cleans up the
    // jumpscare overlay/cam without restoring gameplay UI, input, or
    // visibility, all of which Eliminate() intentionally keeps hidden.
    public void EndJumpscareForElimination() {
        EndJumpscare(restorePlayerState: false);
    }

    private void SetInputLocked(bool locked) {
        foreach (var script in inputScriptsToDisable) {
            if (script != null) script.enabled = !locked;
        }
    }
}