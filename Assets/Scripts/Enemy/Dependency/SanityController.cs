using Unity.Netcode;
using UnityEngine;

// Attach to the player prefab. Tracks a 0 (calm) - 1 (max dread) stress
// value, server-authoritative via NetworkVariable so it can't be spoofed
// client-side. StressManager computes the target each tick from nearby
// enemies (any state, not just Hunt) and calls ServerSetTargetStress; this
// component smooths toward that target and syncs the result.
//
// Effects are only ever applied on the owner's client (see OnStressChanged)
// — this replaces the old hard hunted/not-hunted binary with a continuous
// gate: vision darkening/desaturation ramp in gradually, audio muffling and
// hallucinations only kick in past their own thresholds. The hunted red
// vision + RGB split + motion blur spike (driven separately by
// EnemyController's targeted SetHuntedClientRpc) still layers on top of
// this for the sharper "actively being chased" moment.
public class SanityController : NetworkBehaviour {
    [Tooltip("Leave empty to auto-resolve via GetComponent in OnNetworkSpawn.")]
    [SerializeField] private VisionEffectController visionEffectController;
    [Tooltip("Leave empty to auto-resolve via GetComponent in OnNetworkSpawn.")]
    [SerializeField] private AudioDistortionController audioDistortionController;
    [SerializeField] private float smoothSpeed = 0.5f; // stress units per second toward target

    private readonly NetworkVariable<float> stressLevel = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private float targetStress;

    public float StressLevel => stressLevel.Value;

    public override void OnNetworkSpawn() {
        if (visionEffectController == null) visionEffectController = GetComponent<VisionEffectController>();
        if (audioDistortionController == null) audioDistortionController = GetComponent<AudioDistortionController>();

        if (IsServer && StressManager.Singleton != null)
            StressManager.Singleton.RegisterPlayer(this);

        if (IsOwner) {
            stressLevel.OnValueChanged += OnStressChanged;
            OnStressChanged(0f, stressLevel.Value); // apply current value immediately (covers late joiners)
        }
    }

    public override void OnNetworkDespawn() {
        if (IsServer && StressManager.Singleton != null)
            StressManager.Singleton.UnregisterPlayer(this);

        if (IsOwner)
            stressLevel.OnValueChanged -= OnStressChanged;
    }

    private void Update() {
        if (!IsServer) return;
        if (Mathf.Approximately(stressLevel.Value, targetStress)) return;

        stressLevel.Value = Mathf.MoveTowards(stressLevel.Value, targetStress, smoothSpeed * Time.deltaTime);
    }

    // Server-only. Called by StressManager with the latest computed target.
    public void ServerSetTargetStress(float value) {
        if (!IsServer) return;
        targetStress = Mathf.Clamp01(value);
    }

    private void OnStressChanged(float previous, float current) {
        if (visionEffectController != null) visionEffectController.SetStressLevel(current);
        if (audioDistortionController != null) audioDistortionController.SetStress(current);
    }
}