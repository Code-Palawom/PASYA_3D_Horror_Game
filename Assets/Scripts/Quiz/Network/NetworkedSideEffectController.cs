using Unity.Netcode;
using UnityEngine;

// Sits on the Player prefab.
public class NetworkedSideEffectController : NetworkBehaviour {
    [SerializeField] SideEffectRegistry registry;

    // Call on the local player to broadcast side effects to all clients.
    [Rpc(SendTo.Server)]
    public void TriggerSideEffectsRpc(int[] effectIndices) {
        ApplySideEffectsRpc(effectIndices);
    }

    [Rpc(SendTo.Everyone)]
    void ApplySideEffectsRpc(int[] effectIndices) {
        bool isLocalPlayer = IsOwner;

        foreach (int idx in effectIndices) {
            QuizSideEffect effect = registry.GetByIndex(idx);
            if (effect != null)
                StartCoroutine(effect.ApplyWithDuration(gameObject, isLocalPlayer));
        }
    }
}