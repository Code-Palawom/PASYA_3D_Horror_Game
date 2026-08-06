using System;
using UnityEngine;

public enum SkinPaywallType {
    Free,
    Currency,   // in-game currency (coins/gems) — fully server-priced, see below
    IAP         // real-money purchase
}

public enum SkinAvailabilityStatus {
    Available,
    Loading,    // Currency skin whose Firestore pricing/window sync hasn't returned yet
    Expired     // window closed, OR (see GetAvailabilityStatus) no price configured at all
}

[CreateAssetMenu(menuName = "PASYA/Character Skin")]
public class CharacterSkinSO : ScriptableObject {
    public string skinId;
    public string displayName;
    public GameObject modelPrefab;   // Humanoid Avatar assigned, own SkinnedMeshRenderer + rig
    public Sprite previewIcon;

    [Header("Paywall")]
    public SkinPaywallType paywallType = SkinPaywallType.Free;

    [Tooltip("If true, every player owns this skin regardless of OwnedSkinIds — used for free/default skins.")]
    public bool ownedByDefault = true;

    [Tooltip("Used when paywallType == IAP — must match the product ID configured in your store backend")]
    public string iapProductId;

    // ── Currency pricing/availability — entirely server-driven, no local fields ──────────
    // Every Currency-paywall skin's price (and, optionally, purchase window) comes from a
    // single config/skinPricing Firestore doc via SkinPricingSyncService. There's no local
    // currencyCost field and no local date fields to keep in sync — until the sync lands,
    // the skin just shows "..." and can't be purchased.
    [NonSerialized] private DateTime? remoteAvailableFrom;
    [NonSerialized] private DateTime? remoteAvailableUntil;
    [NonSerialized] private int? remoteCurrencyCost;
    [NonSerialized] private bool pricingSynced;

    // What the shop should charge. Only meaningful once GetAvailabilityStatus returns
    // Available — don't call this while still Loading/Expired.
    public int EffectiveCurrencyCost => remoteCurrencyCost ?? 0;

    // Called by SkinPricingSyncService. from/until are null if this skin has no window
    // restriction; currencyCost is null if this skin has no config/skinPricing entry at all
    // (which GetAvailabilityStatus treats as Expired — a missing price is a content-config
    // gap, not something that should silently let the skin be bought for free).
    public void ApplyRemotePricing(DateTime? from, DateTime? until, int? currencyCost) {
        remoteAvailableFrom = from;
        remoteAvailableUntil = until;
        remoteCurrencyCost = currencyCost;
        pricingSynced = true;
    }

    public SkinAvailabilityStatus GetAvailabilityStatus(DateTime nowUtc) {
        if (paywallType != SkinPaywallType.Currency) return SkinAvailabilityStatus.Available; // Free/IAP unaffected by this sync

        if (!pricingSynced) return SkinAvailabilityStatus.Loading;
        if (remoteCurrencyCost == null) return SkinAvailabilityStatus.Expired; // no price configured server-side at all

        if (remoteAvailableFrom.HasValue && nowUtc < remoteAvailableFrom.Value) return SkinAvailabilityStatus.Expired;
        if (remoteAvailableUntil.HasValue && nowUtc > remoteAvailableUntil.Value) return SkinAvailabilityStatus.Expired;

        return SkinAvailabilityStatus.Available;
    }
}