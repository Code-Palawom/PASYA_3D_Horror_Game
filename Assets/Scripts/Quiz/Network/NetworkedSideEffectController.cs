using Unity.Netcode;
using UnityEngine;

// Sits on the Player prefab.
// Use for non-gate side effects (environmental traps, items, etc.).
// Do NOT call from gate logic — use NetworkedQuizGate's broadcast path instead.
// Uses NGO 2.x unified [Rpc] attribute.
public class NetworkedSideEffectController : NetworkBehaviour {
    [SerializeField] SideEffectRegistry registry;

    /// Call on the local player to broadcast side effects to all clients.
    [Rpc(SendTo.Server)]
    public void TriggerSideEffectsRpc(int[] effectIndices) {
        Debug.Log("[RPC][Server] TriggerSideEffects");
        ApplySideEffectsRpc(effectIndices);
    }

    [Rpc(SendTo.Everyone)]
    void ApplySideEffectsRpc(int[] effectIndices) {
        Debug.Log("[RPC][Everyone] ApplySideEffects");
        bool isLocalPlayer = IsOwner;

        foreach (int idx in effectIndices) {
            QuizSideEffect effect = registry.GetByIndex(idx);
            if (effect != null)
                StartCoroutine(effect.ApplyWithDuration(gameObject, isLocalPlayer));
        }
    }
}