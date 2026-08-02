using System;
using UnityEngine;

public enum SkinPaywallType {
    Free,
    Currency,   // in-game currency (coins/gems)
    IAP         // real-money purchase
}

public enum SkinAvailabilityStatus {
    Available,
    Loading,    // isLimitedTime is true but the Firestore sync hasn't returned yet
    Expired
}

[CreateAssetMenu(menuName = "Character/Skin")]
public class CharacterSkinSO : ScriptableObject {
    public string skinId;
    public string displayName;
    public GameObject modelPrefab;   // Humanoid Avatar assigned, own SkinnedMeshRenderer + rig
    public Sprite previewIcon;

    [Header("Paywall")]
    public SkinPaywallType paywallType = SkinPaywallType.Free;

    [Tooltip("If true, every player owns this skin regardless of OwnedSkinIds — used for free/default skins.")]
    public bool ownedByDefault = true;

    [Tooltip("Used when paywallType == Currency")]
    public int currencyCost;

    [Tooltip("Used when paywallType == IAP — must match the product ID configured in your store backend")]
    public string iapProductId;

    [Header("Limited-Time Availability")]
    [Tooltip("If true, this skin's purchase window is fetched from the timeLimitedSkins/{skinId} Firestore doc — the actual dates aren't configured here.")]
    public bool isLimitedTime;

    // Runtime-only — populated by TimeLimitedSkinSyncService, never serialized/editor-configured.
    // No local fallback: until the sync completes, the shop shows a "..." loading state instead
    // of guessing at a window, so there's never a stale/wrong date to keep in sync manually.
    [NonSerialized] private DateTime? remoteAvailableFrom;
    [NonSerialized] private DateTime? remoteAvailableUntil;
    [NonSerialized] private bool availabilitySynced;

    public void ApplyRemoteAvailabilityWindow(DateTime? from, DateTime? until) {
        remoteAvailableFrom = from;
        remoteAvailableUntil = until;
        availabilitySynced = true;
    }

    // Called when no timeLimitedSkins doc exists remotely for this skin.
    public void ClearRemoteAvailabilityWindow() {
        remoteAvailableFrom = null;
        remoteAvailableUntil = null;
        availabilitySynced = true;
    }

    public SkinAvailabilityStatus GetAvailabilityStatus(DateTime nowUtc) {
        if (!isLimitedTime) return SkinAvailabilityStatus.Available;
        if (!availabilitySynced) return SkinAvailabilityStatus.Loading;

        if (remoteAvailableFrom.HasValue && nowUtc < remoteAvailableFrom.Value) return SkinAvailabilityStatus.Expired;
        if (remoteAvailableUntil.HasValue && nowUtc > remoteAvailableUntil.Value) return SkinAvailabilityStatus.Expired;

        return SkinAvailabilityStatus.Available;
    }
}