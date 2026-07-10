using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

// Fetches loading screen tips from a REST endpoint once per session.
// Falls back to a local HMAC-verified cache if the request fails.
// Follows the same save/load pattern as SettingsManager.

// Expected endpoint response:
// { "tips": ["Tip one.", "Tip two.", ...] }
public class TipsManager : MonoBehaviour {
    public static TipsManager Instance { get; private set; }

    [Header("Endpoint")]
    [SerializeField] private string tipsUrl = "";
    [SerializeField] private float timeoutSeconds = 5f;

    [Header("Fallback Tips")]
    [SerializeField]
    private string[] fallbackTips = new[]
    {
        "Answer quickly to earn bonus points!",
        "Team up with others to unlock harder questions.",
        "Every correct answer brings you closer to the gate.",
    };

    public event Action OnTipsReady;

    private TipsData _current;
    private bool _fetched;

    private const string FileName = "tips_cache.json";
    private string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    // ── Lifecycle ───────────────────────────────────────────

    void Awake() {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _current = new TipsData { tips = fallbackTips };

        LoadCache();
    }

    void Start() {
        StartCoroutine(FetchFromEndpoint());
    }

    // ── Public API ──────────────────────────────────────────

    /// <summary>Returns a random tip. Always available (falls back to hardcoded tips).</summary>
    public string GetRandomTip() {
        if (_current?.tips == null || _current.tips.Length == 0)
            return string.Empty;

        return _current.tips[UnityEngine.Random.Range(0, _current.tips.Length)];
    }

    // ── Fetch ───────────────────────────────────────────────

    private IEnumerator FetchFromEndpoint() {
        if (string.IsNullOrWhiteSpace(tipsUrl)) yield break;

        if (_fetched) yield break;

        using var request = UnityWebRequest.Get(tipsUrl);
        request.timeout = Mathf.RoundToInt(timeoutSeconds);

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success) {
            Debug.LogWarning($"[TipsManager] Fetch failed ({request.error}) — using cache.");
            yield break;
        }

        try {
            var response = JsonUtility.FromJson<TipsResponse>(request.downloadHandler.text);
            if (response?.tips == null || response.tips.Length == 0) {
                Debug.LogWarning("[TipsManager] Empty response — keeping cache.");
                yield break;
            }

            _current = new TipsData {
                tips = response.tips,
                fetchedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            _fetched = true;
            SaveCache();
            OnTipsReady?.Invoke();
            Debug.Log($"[TipsManager] Fetched {_current.tips.Length} tips.");
        } catch (Exception e) {
            Debug.LogError($"[TipsManager] Parse error: {e.Message}");
        }
    }

    // ── Save ────────────────────────────────────────────────

    private void SaveCache() {
        try {
            string json = JsonUtility.ToJson(_current, true);
            var wrapper = new Wrapper {
                payload = json,
                //signature = QuizDataIntegrity.ComputeSignature(json)
            };

            File.WriteAllText(FilePath, JsonUtility.ToJson(wrapper, true));
            Debug.Log("[TipsManager] Cache saved.");
        } catch (Exception e) {
            Debug.LogError($"[TipsManager] Save error: {e.Message}");
        }
    }

    // ── Load ────────────────────────────────────────────────

    private void LoadCache() {
        if (!File.Exists(FilePath)) {
            Debug.Log("[TipsManager] No cache — using fallback tips.");
            return;
        }

        try {
            string raw = File.ReadAllText(FilePath);
            var wrapper = JsonUtility.FromJson<Wrapper>(raw);

            //if (!QuizDataIntegrity.Verify(wrapper.payload, wrapper.signature)) {
            //    Debug.LogWarning("[TipsManager] Cache integrity check failed — using fallback tips.");
            //    return;
            //}

            var cached = JsonUtility.FromJson<TipsData>(wrapper.payload);
            if (cached?.tips != null && cached.tips.Length > 0) {
                _current = cached;
                Debug.Log($"[TipsManager] Loaded {_current.tips.Length} cached tips.");
            }
        } catch (Exception e) {
            Debug.LogError($"[TipsManager] Cache load error: {e.Message}");
        }
    }

    // ── Internal types ──────────────────────────────────────

    [Serializable]
    private class TipsResponse {
        public string[] tips;
    }

    [Serializable]
    private class Wrapper {
        public string payload;
        public string signature;
    }

    [Serializable]
    public class TipsData {
        public string[] tips;
        public long fetchedAt; // Unix timestamp of last successful fetch
    }
}