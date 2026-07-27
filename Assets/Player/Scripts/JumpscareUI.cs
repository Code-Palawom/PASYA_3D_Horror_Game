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
    [SerializeField] private Transform playerCamera; // actual Camera transform (has CinemachineBrain)

    [Header("Cinemachine")]
    [SerializeField] private CinemachineCamera jumpscareCam;       // dedicated vcam, no Aim/Body — driven manually
    [SerializeField] private CinemachineCamera firstPersonCam;
    [SerializeField] private CinemachineCamera thirdPersonCam;
    [SerializeField] private CinemachinePanTilt firstPersonPanTilt;      // FP aim
    [SerializeField] private CinemachineOrbitalFollow thirdPersonOrbitalFollow; // TP body (drives orbit position)
    // Note: thirdPersonCam's Aim is a CinemachineRotationComposer pointed at the
    // player's LookAt target — it has no axes of its own, so nothing to sync there.
    // Only OrbitalFollow's Horizontal/Vertical axes need to be re-seeded on handoff.

    [Header("Input lock while jumpscare plays")]
    [SerializeField] private MonoBehaviour[] inputScriptsToDisable; // FPS controller, look script, etc.
    [SerializeField] private GameObject gameplayUI; // action buttons / on-screen controller root

    [Header("Timing (must match PlayerHealth's WaitForSeconds calls)")]
    [SerializeField] private int respawnCountdownSeconds = 3;

    private const int JumpscarePriority = 100;
    private int gameplayPriorityBackup;

    public override void OnNetworkSpawn() {
        if (!IsOwner) { enabled = false; return; }
        overlayGroup.alpha = 0f;
        overlayGroup.gameObject.SetActive(false);
        jumpscareCam.Priority = -1; // stay out of the way until triggered
    }

    // Whichever vcam currently has the higher priority is "active" — call this
    // from wherever you already flip FP/TP priorities, or read it live below.
    private CinemachineCamera ActiveGameplayCam =>
        firstPersonCam.Priority.Value >= thirdPersonCam.Priority.Value ? firstPersonCam : thirdPersonCam;

    private bool IsFirstPersonActive => ActiveGameplayCam == firstPersonCam;

    // Phase 1 — jumpscare hit lands: cut camera to the jumpscare vcam facing
    // the enemy, show overlay + death message, hide gameplay buttons.
    [Rpc(SendTo.Owner)]
    public void TriggerJumpscareRpc(Vector3 enemyPos, Quaternion enemyRot) {
        SetInputLocked(true);
        if (gameplayUI != null) gameplayUI.SetActive(false);

        Vector3 lookDir = enemyPos - playerCamera.position;
        Quaternion snapRot = lookDir.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(lookDir.normalized)
            : playerCamera.rotation;

        jumpscareCam.transform.SetPositionAndRotation(playerCamera.position, snapRot);
        jumpscareCam.ForceCameraPosition(playerCamera.position, snapRot);

        gameplayPriorityBackup = ActiveGameplayCam.Priority.Value;
        jumpscareCam.Priority = JumpscarePriority; // brain cuts to it (set the blend between these to "Cut")

        deathMessageText.text = "You died.";
        countdownText.text = "";
        overlayGroup.gameObject.SetActive(true);
        Tween.Alpha(overlayGroup, 1f, 0.25f, Ease.OutQuad);
    }

    // Phase 2 — server has already teleported us. Clear the death message,
    // orient the jumpscare vcam to the new spawn's facing, start the countdown.
    [Rpc(SendTo.Owner)]
    public void ShowRespawnLocationRpc(Quaternion respawnRot) {
        deathMessageText.text = "";
        jumpscareCam.transform.rotation = respawnRot; // position already follows the (teleported) player root
        jumpscareCam.ForceCameraPosition(jumpscareCam.transform.position, respawnRot);
        StartCoroutine(CountdownRoutine(respawnCountdownSeconds));
    }

    private IEnumerator CountdownRoutine(int seconds) {
        for (int i = seconds; i > 0; i--) {
            countdownText.text = i.ToString();
            yield return new WaitForSeconds(1f);
        }
        countdownText.text = "";
    }

    // Phase 3 — sequence over: sync the active gameplay vcam's stored aim to
    // where the jumpscare cam ended up (avoids a pop), hand priority back, fade out.
    [Rpc(SendTo.Owner)]
    public void EndJumpscareRpc() {
        Vector3 euler = jumpscareCam.transform.eulerAngles;

        if (IsFirstPersonActive) {
            firstPersonPanTilt.PanAxis.Value = euler.y;
            firstPersonPanTilt.TiltAxis.Value = NormalizePitch(euler.x);
        } else {
            // OrbitalFollow's Horizontal/Vertical axes describe the camera's
            // orbit angle around the follow target, not a raw world rotation.
            // This maps the jumpscare cam's yaw/pitch onto those axes directly —
            // good enough when the rig's forward/up matches the player root,
            // but re-check the axis Range/Center in your OrbitalFollow settings
            // if the third-person camera pops on handoff.
            thirdPersonOrbitalFollow.HorizontalAxis.Value = euler.y;
            thirdPersonOrbitalFollow.VerticalAxis.Value = NormalizePitch(euler.x);
        }

        jumpscareCam.Priority = gameplayPriorityBackup - 1;

        Tween.Alpha(overlayGroup, 0f, 0.25f, Ease.OutQuad)
            .OnComplete(() => overlayGroup.gameObject.SetActive(false));
        if (gameplayUI != null) gameplayUI.SetActive(true);
        SetInputLocked(false);
    }

    // Unity's eulerAngles.x is 0-360, wrapping the moment you look up past
    // the horizon. PanTilt/OrbitalFollow pitch axes are centered at 0, so
    // convert to a signed -180..180 range before assigning.
    private static float NormalizePitch(float rawPitchEuler) {
        return rawPitchEuler > 180f ? rawPitchEuler - 360f : rawPitchEuler;
    }

    private void SetInputLocked(bool locked) {
        foreach (var script in inputScriptsToDisable)
            if (script != null) script.enabled = !locked;
    }
}