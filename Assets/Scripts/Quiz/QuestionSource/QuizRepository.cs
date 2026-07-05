using Org.BouncyCastle.Crypto;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

// Single source of truth for all quiz sets.
// Persistent across scenes via DontDestroyOnLoad.

// Two-tier local storage:
//                — lightweight cache for all sets (used by menu)
//   {sha256(setId)}.bin   — full set data with questions (loaded on play)

// Filenames are SHA-256 hashes of the setId — deterministic but opaque.
// The setId → filename mapping is stored in cache.
public class QuizRepository : MonoBehaviour {
    public static QuizRepository Instance { get; private set; }

    [Header("Local Sets (ScriptableObjects)")]
    [Tooltip("Default question banks. Always available, no download needed.")]
    [SerializeField] List<QuizSetData> localSets;

    private readonly List<QuizSetRuntime> _localRuntimeSets = new();

    private string SaveDirectory => Path.Combine(Application.persistentDataPath, "");
    private string CachePath => Path.Combine(SaveDirectory, "");

    /// <summary>Returns the hashed .bin path for a given setId.</summary>
    private string SetPath(string setId) =>
        Path.Combine(SaveDirectory, $"");

    // ─────────────────────────────────────────────────────────
    void Awake() {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        EnsureDirectory();
        LoadLocalSets();
    }

    // ─────────────────────────────────────────────────────────
    // 1. Local SOs
    // ─────────────────────────────────────────────────────────
    void LoadLocalSets() {
        if (localSets == null) return;
        foreach (var so in localSets) {
            if (so == null) continue;
            var runtime = so.ToRuntime();
            runtime.source = QuizSetRuntime.SourceType.Local;
            _localRuntimeSets.Add(runtime);
            Debug.Log($"[QuizRepository] Local set loaded: '{runtime.name}'");
        }
    }

    /// <summary>
    /// Returns a QuizSetMetaEntry for each local SO set.
    /// Local sets skip isVerified and always have hasLocalData = true.
    /// Call in MainMenuUI.Start() alongside LoadCacheImmediately().
    /// </summary>
    public List<QuizSetMetaEntry> GetLocalSetMeta() {
        if (localSets == null) return new List<QuizSetMetaEntry>();
        var result = new List<QuizSetMetaEntry>();
        foreach (var so in localSets) {
            if (so == null) continue;
            result.Add(so.ToMetaEntry());
        }
        return result;
    }

    // ─────────────────────────────────────────────────────────
    // 2. Meta Cache
    // ─────────────────────────────────────────────────────────

    public List<QuizSetMetaEntry> LoadCache() {
        if (!File.Exists(CachePath)) return new List<QuizSetMetaEntry>();
        try {
            string json = SaveEncryption.Decrypt(File.ReadAllBytes(CachePath));
            var wrapper = JsonUtility.FromJson<MetaCacheWrapper>(json);
            if (wrapper?.entries == null) return new List<QuizSetMetaEntry>();

            // Sync hasLocalData with actual disk state
            foreach (var e in wrapper.entries)
                e.hasLocalData = File.Exists(SetPath(e.setId));

            return wrapper.entries;
        } catch (InvalidCipherTextException) {
            Debug.LogWarning("[QuizRepository] tampered. Discarding.");
            DeleteFile(CachePath);
            return new List<QuizSetMetaEntry>();
        } catch (Exception e) {
            Debug.LogWarning($"[QuizRepository] read error: {e.Message}");
            return new List<QuizSetMetaEntry>();
        }
    }

    public void SaveMetaCache(List<QuizSetMetaEntry> entries) {
        try {
            string json = JsonUtility.ToJson(new MetaCacheWrapper { entries = entries }, prettyPrint: true);
            byte[] encrypted = SaveEncryption.Encrypt(json);
            File.WriteAllBytes(CachePath, encrypted);
            Debug.Log($"[QuizRepository] saved ({entries.Count} entries).");
        } catch (Exception e) {
            Debug.LogWarning($"[QuizRepository] save error: {e.Message}");
        }
    }

    // ─────────────────────────────────────────────────────────
    // 3. Per-set .bin
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the full QuizSetRuntime for the given display name.
    /// Checks local SOs first, then loads from disk.
    /// </summary>
    public QuizSetRuntime GetSetByName(string name) {
        var local = _localRuntimeSets.FirstOrDefault(s => s.name == name);
        if (local != null) return local;

        var cache = LoadCache().FirstOrDefault(e => e.name == name);
        if (cache == null) {
            Debug.LogWarning($"[QuizRepository] No cache entry for '{name}'.");
            return null;
        }

        return LoadSetFromDisk(cache.setId);
    }

    public QuizSetRuntime LoadSetFromDisk(string setId) {
        string path = SetPath(setId);
        if (!File.Exists(path)) {
            Debug.LogWarning($"[QuizRepository] No .bin for set '{setId}'. Not downloaded yet.");
            return null;
        }

        try {
            string json = SaveEncryption.Decrypt(File.ReadAllBytes(path));
            var wrapper = JsonUtility.FromJson<QuizSetJsonWrapper>(json);
            if (wrapper?.questions == null) return null;

            var runtime = wrapper.ToRuntime();
            runtime.source = QuizSetRuntime.SourceType.Fetched;
            Debug.Log($"[QuizRepository] Loaded from disk: '{runtime.name}' ({runtime.questions.Count} questions)");
            return runtime;
        } catch (InvalidCipherTextException) {
            Debug.LogWarning($"[QuizRepository] '{setId}' bin tampered. Deleting.");
            DeleteFile(path);
            return null;
        } catch (Exception e) {
            Debug.LogWarning($"[QuizRepository] Failed to load '{setId}': {e.Message}");
            return null;
        }
    }

    public void SaveSetToDisk(string setId, QuizSetRuntime set) {
        try {
            string json = JsonUtility.ToJson(QuizSetJsonWrapper.FromRuntime(set), prettyPrint: true);
            byte[] encrypted = SaveEncryption.Encrypt(json);
            File.WriteAllBytes(SetPath(setId), encrypted);
            Debug.Log($"[QuizRepository] Saved set '{setId}' → {HashId(setId)}.bin");
        } catch (Exception e) {
            Debug.LogWarning($"[QuizRepository] Failed to save '{setId}': {e.Message}");
        }
    }

    public void DeleteSetFromDisk(string setId) {
        DeleteFile(SetPath(setId));
        Debug.Log($"[QuizRepository] Deleted bin for '{setId}'.");
    }

    public bool SetExistsOnDisk(string setId) => File.Exists(SetPath(setId));

    // ─────────────────────────────────────────────────────────
    // 4. Question access
    // ─────────────────────────────────────────────────────────

    public QuestionRuntime GetRandomQuestion(QuestionDifficulty difficulty, string setName) {
        var set = GetSetByName(setName);
        if (set == null) return null;

        var pool = set.questions.Where(q => q.difficulty == difficulty).ToList();
        if (pool.Count == 0) {
            Debug.LogWarning($"[QuizRepository] No '{difficulty}' in '{setName}' — using any.");
            pool = set.questions;
        }

        if (pool.Count == 0) { Debug.LogError($"[QuizRepository] '{setName}' has no questions."); return null; }

        return pool[UnityEngine.Random.Range(0, pool.Count)];
    }

    // ─────────────────────────────────────────────────────────
    // Utilities
    // ─────────────────────────────────────────────────────────

    void EnsureDirectory() {
        if (!Directory.Exists(SaveDirectory))
            Directory.CreateDirectory(SaveDirectory);
    }

    void DeleteFile(string path) {
        try { if (File.Exists(path)) File.Delete(path); } catch (Exception e) { Debug.LogWarning($"[QuizRepository] Delete failed '{path}': {e.Message}"); }
    }

    // Returns a deterministic SHA-256 hex string for the given setId.
    // Used as the filename for per-set .bin files.
    // Example: "science-quiz" → "3b4c1f..."
    static string HashId(string id) {
        using var sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(id));
        return Convert.ToBase64String(hash)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    public List<QuizSetMetaEntry> GetAllMeta() => LoadCache();
    public List<QuizSetMetaEntry> GetMetaByCategory(string cat) =>
        LoadCache().Where(e => e.category == cat).ToList();
    public List<string> GetAllCategories() =>
        LoadCache().Select(e => e.category)
                       .Where(c => !string.IsNullOrWhiteSpace(c))
                       .Distinct().OrderBy(c => c).ToList();
}