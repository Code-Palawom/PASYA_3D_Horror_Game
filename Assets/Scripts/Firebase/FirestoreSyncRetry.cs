using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// Shared exponential-backoff retry loop for Firestore config syncs
// so the backoff/cancellation logic lives in one place instead of
// being copy-pasted per sync service.
public static class FirestoreSyncRetry {

    // Keeps retrying indefinitely until trySyncOnce succeeds or cancellationToken fires.
    // Backoff grows exponentially up to maxDelaySeconds, then holds there.
    public static async Task RunPersistentAsync(
        Func<Task<bool>> trySyncOnce,
        Action onSyncSucceeded,
        float initialDelaySeconds = 2f,
        float backoffMultiplier = 2f,
        float maxDelaySeconds = 30f,
        CancellationToken cancellationToken = default,
        string logTag = "FirestoreSyncRetry") {

        float delay = initialDelaySeconds;

        while (true) {
            cancellationToken.ThrowIfCancellationRequested();

            bool success = await trySyncOnce();
            if (success) {
                onSyncSucceeded?.Invoke();
                return;
            }

            Debug.LogWarning($"[{logTag}] Sync failed, retrying in {delay:F1}s.");
            await Task.Delay(TimeSpan.FromSeconds(delay), cancellationToken);
            delay = Mathf.Min(delay * backoffMultiplier, maxDelaySeconds);
        }
    }

    // Finite-attempt version, for callers that don't want an indefinite retry.
    public static async Task<bool> RunWithAttemptCapAsync(
        Func<Task<bool>> trySyncOnce,
        int maxAttempts = 3,
        float initialDelaySeconds = 2f,
        float backoffMultiplier = 2f,
        CancellationToken cancellationToken = default,
        string logTag = "FirestoreSyncRetry") {

        float delay = initialDelaySeconds;

        for (int attempt = 1; attempt <= maxAttempts; attempt++) {
            cancellationToken.ThrowIfCancellationRequested();

            bool success = await trySyncOnce();
            if (success) return true;

            if (attempt < maxAttempts) {
                Debug.LogWarning($"[{logTag}] Sync attempt {attempt}/{maxAttempts} failed, retrying in {delay:F1}s.");
                await Task.Delay(TimeSpan.FromSeconds(delay), cancellationToken);
                delay *= backoffMultiplier;
            }
        }

        Debug.LogWarning($"[{logTag}] All {maxAttempts} sync attempts failed.");
        return false;
    }
}