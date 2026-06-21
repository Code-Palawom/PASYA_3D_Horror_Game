using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

// Fetches an ARRAY of quiz sets from a remote endpoint.
//
// Expected format:
// [
//   { "name": "Science Quiz", "questions": [...] },
//   { "name": "Math Quiz",    "questions": [...] }
// ]
//
// Each fetched set is added to QuizRepository.
// If a set with the same name already exists (local or previously fetched),
// it is replaced with the new data.
//
// Usage:
//   QuizFetcher.Instance.Fetch("",
//       onSuccess: sets => Debug.Log($"Loaded {sets.Count} sets"),
//       onError:   err  => Debug.LogError(err));
public class QuizFetcher : MonoBehaviour {
    public static QuizFetcher Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] string defaultEndpoint = "";
    [SerializeField] float timeoutSeconds = 10f;

    public bool IsFetching { get; private set; }

    void Awake() {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ─────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────

    public void FetchDefault(Action<List<QuizSetRuntime>> onSuccess = null,
                             Action<string> onError = null) {
        if (string.IsNullOrWhiteSpace(defaultEndpoint)) {
            onError?.Invoke("[QuizFetcher] No default endpoint set.");
            return;
        }
        Fetch(defaultEndpoint, onSuccess, onError);
    }

    public void Fetch(string url,
                      Action<List<QuizSetRuntime>> onSuccess = null,
                      Action<string> onError = null) {
        if (IsFetching) { onError?.Invoke("[QuizFetcher] Already fetching."); return; }
        StartCoroutine(FetchRoutine(url, onSuccess, onError));
    }

    // ─────────────────────────────────────────────────────────
    IEnumerator FetchRoutine(string url,
                             Action<List<QuizSetRuntime>> onSuccess,
                             Action<string> onError) {
        IsFetching = true;
        Debug.Log($"[QuizFetcher] Fetching from: {url}");

        using var request = UnityWebRequest.Get(url);
        request.timeout = (int)timeoutSeconds;

        yield return request.SendWebRequest();

        IsFetching = false;

        // ── Network error ─────────────────────────────────────
        if (request.result != UnityWebRequest.Result.Success) {
            string err = $"[QuizFetcher] Request failed: {request.error}";
            Debug.LogWarning(err);
            onError?.Invoke(err);
            yield break;
        }

        string json = request.downloadHandler.text;

        // ── Parse array ───────────────────────────────────────
        // JsonUtility can't deserialize top-level arrays.
        // Wrap it: [...]  →  {"items":[...]}
        QuizSetArrayWrapper wrapper = null;
        try {
            string wrapped = $"{{\"items\":{json}}}";
            wrapper = JsonUtility.FromJson<QuizSetArrayWrapper>(wrapped);
        } catch (Exception e) {
            string err = $"[QuizFetcher] JSON parse error: {e.Message}";
            Debug.LogWarning(err);
            onError?.Invoke(err);
            yield break;
        }

        if (wrapper?.items == null || wrapper.items.Count == 0) {
            string err = "[QuizFetcher] Fetched JSON is empty or invalid.";
            Debug.LogWarning(err);
            onError?.Invoke(err);
            yield break;
        }

        // ── Add each set to repository ────────────────────────
        var loadedSets = new List<QuizSetRuntime>();

        foreach (var item in wrapper.items) {
            if (item == null || string.IsNullOrWhiteSpace(item.name)) continue;

            QuizSetRuntime set = item.ToRuntime();
            QuizRepository.Instance.AddFetchedSet(set);   // replaces by name
            loadedSets.Add(set);

            Debug.Log($"[QuizFetcher] Loaded set: '{set.name}' ({set.questions.Count} questions)");
        }

        Debug.Log($"[QuizFetcher] Fetch complete. {loadedSets.Count} sets loaded.");
        onSuccess?.Invoke(loadedSets);
    }
}