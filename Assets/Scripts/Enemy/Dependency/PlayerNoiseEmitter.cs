using Unity.Netcode;
using UnityEngine;

// Attach to the player prefab. Call EmitNoise(...) from your footstep/
// sprint/interaction scripts. Only the owning client should call it;
// it forwards to the server via RPC so detection stays authoritative.
public class PlayerNoiseEmitter : NetworkBehaviour, IMakesNoise {
    public void EmitNoise(Vector3 position, float loudness) {
        if (!IsOwner) return;
        EmitNoiseServerRpc(position, loudness);
    }

    [ServerRpc]
    private void EmitNoiseServerRpc(Vector3 position, float loudness) {
        if (NoiseManager.Singleton != null)
            NoiseManager.Singleton.ReportNoise(position, loudness, OwnerClientId);
    }
}