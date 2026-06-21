using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

// Single source of truth for all quiz sets.
// Persistent across scenes (MainMenu → Lobby → Level) via DontDestroyOnLoad,
// since the Main Menu needs it to populate quiz dropdowns before any
// network connection exists.

// Saved files are protected by HMAC-SHA256 signatures (see QuizDataIntegrity).
public class QuizRepository : MonoBehaviour {
    public static QuizRepository Instance { get; private set; }

    [Header("Local Sets (ScriptableObjects)")]
    [Tooltip("Default question banks. Fetched data with the same name will override these.")]
    [SerializeField] List<QuizSetData> localSets;

    private List<QuizSetRuntime> _sets = new();

    private string SaveDirectory => Path.Combine(Application.persistentDataPath, "QuizSets");
    private string SigPath(string jsonPath) => jsonPath + ".sig";

    // ─────────────────────────────────────────────────────────
    void Awake() {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadLocalSets();
        LoadSavedSetsFromDisk();
    }

    // ─────────────────────────────────────────────────────────
    // 1. Local SOs — always trusted, loaded first
    // ─────────────────────────────────────────────────────────
    void LoadLocalSets() {
        if (localSets == null) return;

        foreach (var so in localSets) {
            if (so == null) continue;
            var runtime = so.ToRuntime();
            runtime.source = QuizSetRuntime.SourceType.Local;
            _sets.Add(runtime);
            Debug.Log($"[QuizRepository] Local: '{runtime.name}' ({runtime.questions.Count} questions)");
        }
    }

    // ─────────────────────────────────────────────────────────
    // 2. Disk JSON — verify signature before accepting
    // ─────────────────────────────────────────────────────────
    void LoadSavedSetsFromDisk() {
        if (!Directory.Exists(SaveDirectory)) return;

        foreach (var file in Directory.GetFiles(SaveDirectory, "*.json")) {
            try {
                string json = File.ReadAllText(file);
                string sigFile = SigPath(file);

                if (!File.Exists(sigFile)) {
                    Debug.LogWarning($"[QuizRepository] Missing signature for '{file}'. Skipping.");
                    continue;
                }

                string savedSig = File.ReadAllText(sigFile).Trim();

                if (!QuizDataIntegrity.Verify(json, savedSig)) {
                    Debug.LogWarning($"[QuizRepository] Tampered file detected: '{file}'. Skipping.");
                    DeleteCorruptedFile(file, sigFile);
                    continue;
                }

                var wrapper = JsonUtility.FromJson<QuizSetJsonWrapper>(json);
                if (wrapper?.questions == null) continue;

                var runtime = wrapper.ToRuntime();
                runtime.source = QuizSetRuntime.SourceType.Fetched;

                RemoveByName(runtime.name);
                _sets.Add(runtime);

                Debug.Log($"[QuizRepository] Disk (verified): '{runtime.name}' ({runtime.questions.Count} questions)");
            } catch (System.Exception e) {
                Debug.LogWarning($"[QuizRepository] Failed to load '{file}': {e.Message}");
            }
        }
    }

    // ─────────────────────────────────────────────────────────
    // 3. AddFetchedSet — called by QuizFetcher
    // ─────────────────────────────────────────────────────────
    public void AddFetchedSet(QuizSetRuntime set) {
        set.source = QuizSetRuntime.SourceType.Fetched;

        RemoveByName(set.name);
        _sets.Add(set);

        SaveToDisk(set);

        Debug.Log($"[QuizRepository] Added/updated '{set.name}'. " +
                  $"Total sets: {_sets.Count}, total questions: {TotalQuestions}");
    }

    // ─────────────────────────────────────────────────────────
    void SaveToDisk(QuizSetRuntime set) {
        try {
            if (!Directory.Exists(SaveDirectory))
                Directory.CreateDirectory(SaveDirectory);

            string safeName = string.Concat(set.name.Split(Path.GetInvalidFileNameChars()));
            string jsonPath = Path.Combine(SaveDirectory, $"{safeName}.json");
            string json = JsonUtility.ToJson(QuizSetJsonWrapper.FromRuntime(set), prettyPrint: true);

            File.WriteAllText(jsonPath, json);

            string sig = QuizDataIntegrity.ComputeSignature(json);
            File.WriteAllText(SigPath(jsonPath), sig);

            Debug.Log($"[QuizRepository] Saved + signed '{set.name}' → {jsonPath}");
        } catch (System.Exception e) {
            Debug.LogWarning($"[QuizRepository] Save failed for '{set.name}': {e.Message}");
        }
    }

    void DeleteCorruptedFile(string jsonPath, string sigPath) {
        try {
            if (File.Exists(jsonPath)) File.Delete(jsonPath);
            if (File.Exists(sigPath)) File.Delete(sigPath);
            Debug.Log($"[QuizRepository] Deleted corrupted file: '{jsonPath}'");
        } catch (System.Exception e) {
            Debug.LogWarning($"[QuizRepository] Could not delete corrupted file: {e.Message}");
        }
    }

    // ─────────────────────────────────────────────────────────
    // Get a random question across ALL sets, filtered by difficulty
    // ─────────────────────────────────────────────────────────
    public QuestionRuntime GetRandomQuestion(QuestionDifficulty difficulty) {
        var pool = _sets
            .SelectMany(s => s.questions)
            .Where(q => q.difficulty == difficulty)
            .ToList();

        if (pool.Count == 0) {
            Debug.LogWarning($"[QuizRepository] No '{difficulty}' questions — using all.");
            pool = _sets.SelectMany(s => s.questions).ToList();
        }

        if (pool.Count == 0) {
            Debug.LogError("[QuizRepository] Repository is empty.");
            return null;
        }

        return pool[Random.Range(0, pool.Count)];
    }

    // ─────────────────────────────────────────────────────────
    // Get a random question from ONE specific named set, filtered by difficulty.
    // Used once a quiz session has a selected set (from the lobby).
    // Falls back to searching all sets if setName is empty or not found.
    // ─────────────────────────────────────────────────────────
    public QuestionRuntime GetRandomQuestion(QuestionDifficulty difficulty, string setName) {
        if (string.IsNullOrWhiteSpace(setName))
            return GetRandomQuestion(difficulty);

        var set = _sets.FirstOrDefault(s => s.name == setName);
        if (set == null) {
            Debug.LogWarning($"[QuizRepository] Set '{setName}' not found — falling back to all sets.");
            return GetRandomQuestion(difficulty);
        }

        var pool = set.questions.Where(q => q.difficulty == difficulty).ToList();

        if (pool.Count == 0) {
            Debug.LogWarning($"[QuizRepository] No '{difficulty}' questions in set '{setName}' — using any from that set.");
            pool = set.questions;
        }

        if (pool.Count == 0) {
            Debug.LogError($"[QuizRepository] Set '{setName}' has no questions at all.");
            return null;
        }

        return pool[Random.Range(0, pool.Count)];
    }

    // ─────────────────────────────────────────────────────────
    // Utilities
    // ─────────────────────────────────────────────────────────
    void RemoveByName(string name) =>
        _sets.RemoveAll(s => s.name == name);

    public List<QuizSetRuntime> GetAllSets() => _sets;
    public int TotalQuestions => _sets.Sum(s => s.questions.Count);

    // Names of all available quiz sets — used to populate dropdowns. </summary>
    public List<string> GetAllSetNames() => _sets.Select(s => s.name).ToList();

    // Look up one set by exact name. Returns null if not found. </summary>
    public QuizSetRuntime GetSetByName(string name) =>
        _sets.FirstOrDefault(s => s.name == name);
}