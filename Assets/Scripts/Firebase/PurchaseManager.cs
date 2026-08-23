using System.Threading.Tasks;

// Entry point for skin purchases. Routes to AuthManager for currency spends
// (transaction-safe balance check) and IAP grants (after store confirmation).
public static class PurchaseManager {

    public static async Task<SkinPurchaseResult> PurchaseWithCurrency(CharacterSkinSO skin) {
        if (AuthManager.Instance == null || AuthManager.Instance.CurrentProfile == null)
            return SkinPurchaseResult.NotSignedIn;

        return await AuthManager.Instance.PurchaseSkinWithCurrencyAsync(skin.skinId, skin.EffectiveCurrencyCost);
    }

    public static async Task<SkinPurchaseResult> PurchaseWithIAP(CharacterSkinSO skin) {
        if (string.IsNullOrEmpty(skin.iapProductId))
            return SkinPurchaseResult.Error;

        // Most IAP store SDKs (Unity IAP included) surface their own failure
        // reason for connectivity issues once a purchase is actually attempted
        // (e.g. a network-related PurchaseFailureReason), so prefer mapping
        // that to SkinPurchaseResult.Offline over this check once wired up.
        // Kept here as a fail-fast placeholder so this stub behaves consistently
        // with PurchaseWithCurrency in the meantime.
        if (AuthManager.IsOffline())
            return SkinPurchaseResult.Offline;

        // TODO: wire to your actual store implementation (Unity IAP / native plugin).
        // Once the store confirms a successful, verified purchase, call:
        //     await AuthManager.Instance.GrantSkinAsync(skin.iapProductId... /* or skin.skinId */);
        // and return SkinPurchaseResult.Success. Until that's wired in, this is a no-op stub.
        //
        // Example shape once you have your IAP SDK:
        // var receipt = await IAPService.Instance.PurchaseAsync(skin.iapProductId);
        // if (receipt == null) return SkinPurchaseResult.Error;
        // await AuthManager.Instance.GrantSkinAsync(skin.skinId);
        // return SkinPurchaseResult.Success;
        await Task.Delay(1000); // simulate async store call

        return SkinPurchaseResult.Error;
    }

    // TODO: hook up to your store SDK's localized price lookup, used by
    // SkinPurchasePopupUI to display "$1.99" style pricing before purchase.
    public static string GetIAPPriceString(string productId) => "...";
}