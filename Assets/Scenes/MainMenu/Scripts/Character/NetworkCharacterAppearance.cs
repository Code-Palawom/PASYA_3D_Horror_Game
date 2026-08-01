using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

// Networked wrapper around CharacterAppearanceController.
// Skin is chosen once, in the main menu, before the player spawns into gameplay.
// This component reads the saved choice on spawn and writes it once into a
// NetworkVariable, so it's included in the spawn payload other clients (including
// late joiners) receive automatically. There is no runtime skin-change path here —
// the NetworkVariable is set once at spawn and never touched again.
[RequireComponent(typeof(CharacterAppearanceController))]
public class NetworkCharacterAppearance : NetworkBehaviour {
    [SerializeField] private SkinDatabaseSO database;
    [SerializeField] private CharacterAppearanceController appearance;

    // Owner-writable so each player can set their own skin without going through the host.
    private readonly NetworkVariable<FixedString32Bytes> skinId = new(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    public override void OnNetworkSpawn() {
        skinId.OnValueChanged += OnSkinIdChanged;

        // Covers two cases in one call:
        // 1. Late joiners: skinId already holds the synced value from spawn payload.
        // 2. Owner on first spawn: skinId is still default, so load local save and push it.
        if (IsOwner) {
            // Owner writes directly — WritePermission.Owner allows this without an RPC hop.
            string savedId = SkinSaveSystem.Load();
            if (!string.IsNullOrEmpty(savedId))
                skinId.Value = savedId;
        }

        // Applies immediately for late joiners (skinId already holds the synced
        // spawn-payload value) and for the owner's own initial value above.
        ApplyById(skinId.Value.ToString());
    }

    public override void OnNetworkDespawn() {
        skinId.OnValueChanged -= OnSkinIdChanged;
    }

    private void OnSkinIdChanged(FixedString32Bytes previous, FixedString32Bytes current) {
        ApplyById(current.ToString());
    }

    private void ApplyById(string id) {
        if (string.IsNullOrEmpty(id)) return;
        var skin = database.GetById(id);
        if (skin != null)
            appearance.ApplySkin(skin);
    }
}