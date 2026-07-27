using System.Collections;
using Unity.Netcode;
using UnityEngine;
using TMPro;
using PrimeTween;
using Unity.Cinemachine;

public class JumpscareUI : NetworkBehaviour {
    [Header("Refs")]
    [SerializeField] private CanvasGroup overlayGroup;
    [SerializeField] private TMP_Text deathMessageText;
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private Player player; // existing component, exposes TeleportClientRpc(pos, rot)

    [Header("Cinemachine")]
    [SerializeField] private CinemachineCamera firstPersonCam;
    [SerializeField] private CinemachineCamera thirdPersonCam;

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

    // Priorities captured right before we force first-person, so we can put
    // the same two values back afterward without needing to know the
    // project's actual numeric scheme for "active"/"inactive".
    private int fpPriorityBackup;
    private int tpPriorityBackup;

    public override void OnNetworkSpawn() {
        if (!IsOwner) { enabled = false; return; }
        overlayGroup.alpha = 0f;
        overlayGroup.gameObject.SetActive(false);
    }

    // Forces the FP vcam active regardless of the current gameplay setting,
    // remembering both priorities so RestoreCameraModeFromSettings can put
    // them back in the right slot afterward.
    private void ForceFirstPersonCamera() {
        fpPriorityBackup = firstPersonCam.Priority.Value;
        tpPriorityBackup = thirdPersonCam.Priority.Value;

        int activeValue = Mathf.Max(fpPriorityBackup, tpPriorityBackup);
        int inactiveValue = Mathf.Min(fpPriorityBackup, tpPriorityBackup);

        firstPersonCam.Priority = activeValue;
        thirdPersonCam.Priority = inactiveValue;
    }

    // Re-reads GameSettings.IsFirstPerson (the player may have flipped it
    // while dead) and hands the "active" priority slot to whichever cam
    // that setting calls for.
    private void RestoreCameraModeFromSettings() {
        bool firstPerson = SettingsManager.Instance.Current.isFirstPerson;
        int activeValue = Mathf.Max(fpPriorityBackup, tpPriorityBackup);
        int inactiveValue = Mathf.Min(fpPriorityBackup, tpPriorityBackup);

        firstPersonCam.Priority = firstPerson ? activeValue : inactiveValue;
        thirdPersonCam.Priority = firstPerson ? inactiveValue : activeValue;
    }

    // Phase 1 — jumpscare hit lands: teleport the player's NetworkObject to
    // the fixed spot for this enemy type (facing the prop, since the
    // teleport sets rotation too), force first-person, trigger the prop's
    // animation, show overlay + death message, hide gameplay buttons.
    [Rpc(SendTo.Owner)]
    public void TriggerJumpscareRpc(string enemyType) {
        SetInputLocked(true);
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
        RequestTeleport(entry.playerPosition, targetRot);

        Quaternion yawRot = Quaternion.Euler(0f, entry.playerEulerRotation.y, 0f);
        RequestTeleport(entry.playerPosition, yawRot);
        OrientCameraToJumpscare(entry.playerEulerRotation);

        ForceFirstPersonCamera();

        var propAnimator = JumpscarePropRegistry.Instance != null
            ? JumpscarePropRegistry.Instance.GetAnimator(entry.propId)
            : null;
        if (propAnimator != null) propAnimator.SetTrigger(entry.animationTrigger);

        deathMessageText.text = "You died.";
        countdownText.text = "";
        overlayGroup.gameObject.SetActive(true);
        Tween.Alpha(overlayGroup, 1f, 0.25f, Ease.OutQuad);
    }

    // Host-authoritative: server moves the player's NetworkObject and the
    // NetworkTransform syncs it back down to owner + observers. If PlayerHealth
    // (or whatever already teleports players for respawn) exposes a server-side
    // teleport method, call that instead of setting transform here directly.
    private void RequestTeleport(Vector3 position, Quaternion rotation) {
        player.TeleportClientRpc(position, rotation);
    }

    // Phase 2 — respawn point chosen: teleport the player there and drop the
    // camera back into whatever mode GameSettings.IsFirstPerson calls for,
    // clear the death message, start the countdown.
    [Rpc(SendTo.Owner)]
    public void ShowRespawnLocationRpc(Vector3 respawnPosition, Quaternion respawnRotation) {
        RequestTeleport(respawnPosition, respawnRotation);
        RestoreCameraModeFromSettings();

        if (panTilt != null)
            panTilt.TiltAxis.Value = 0f;

        deathMessageText.text = "";
        StartCoroutine(CountdownRoutine(respawnCountdownSeconds));
    }

    private IEnumerator CountdownRoutine(int seconds) {
        for (int i = seconds; i > 0; i--) {
            countdownText.text = i.ToString();
            yield return new WaitForSeconds(1f);
        }
        countdownText.text = "";
    }

    // Phase 3 — sequence over: fade out the death overlay and hand control
    // back. Camera mode was already settled in Phase 2, so nothing to do here.
    [Rpc(SendTo.Owner)]
    public void EndJumpscareRpc() {
        Tween.Alpha(overlayGroup, 0f, 0.25f, Ease.OutQuad)
            .OnComplete(() => overlayGroup.gameObject.SetActive(false));
        if (gameplayUI != null) gameplayUI.SetActive(true);
        SetInputLocked(false);
    }

    private void SetInputLocked(bool locked) {
        foreach (var script in inputScriptsToDisable)
            if (script != null) script.enabled = !locked;
    }

    private void OrientCameraToJumpscare(Vector3 eulerRotation) {
        if (panTilt == null) return;

        float yaw = eulerRotation.y;
        float pitch = eulerRotation.x;

        panTilt.PanAxis.Value = yaw;
        panTilt.TiltAxis.Value = Mathf.Clamp(pitch, panTilt.TiltAxis.Range.x, panTilt.TiltAxis.Range.y);

        if (fpYawTarget != null)
            fpYawTarget.rotation = Quaternion.Euler(0f, yaw, 0f);
    }
}