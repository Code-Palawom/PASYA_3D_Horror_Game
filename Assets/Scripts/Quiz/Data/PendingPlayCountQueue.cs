using Org.BouncyCastle.Crypto;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Persists pending playCount increments across app restarts.
// Each entry is a setId with a count of how many times it needs to be incremented.

// File: persistentDataPath/QuizSets/pending_plays.bin (AES-GCM encrypted)
// Note: This filename is a fixed constant, not setId-derived, so no hashing is applied.

// Usage:
//   PendingPlayCountQueue.Instance.Enqueue("science-quiz");
//   PendingPlayCountQueue.Instance.Flush(db, collection, metaDocId);
public class PendingPlayCountQueue : MonoBehaviour {
    public static PendingPlayCountQueue Instance { get; private set; }

    string QueuePath => Path.Combine(Application.persistentDataPath, "QuizSets", "pending_plays.bin");

    Dictionary<string, int> _pending = new();

    // ─────────────────────────────────────────────────────────
    void Awake() {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Load();
    }

    // ─────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────

    public void Enqueue(string setId) {
        if (string.IsNullOrWhiteSpace(setId)) return;
        _pending.TryGetValue(setId, out int current);
        _pending[setId] = current + 1;
        Save();
        Debug.Log($"[PendingPlayCountQueue] Queued increment for '{setId}'. " +
                  $"Total pending: {_pending[setId]}");
    }

    public bool HasPending => _pending.Count > 0;

    public void Flush(Firebase.Firestore.FirebaseFirestore db,
                      string collection, string metaDocId,
                      Action onComplete = null) {
        if (!HasPending) { onComplete?.Invoke(); return; }

        Debug.Log($"[PendingPlayCountQueue] Flushing {_pending.Count} pending set(s)...");

        var snapshot = new Dictionary<string, int>(_pending);
        int remaining = snapshot.Count;

        foreach (var (setId, count) in snapshot) {
            db.Collection(collection).Document(metaDocId)
              .UpdateAsync($"sets.{setId}.playCount", Firebase.Firestore.FieldValue.Increment(count))
              .ContinueWith(task => {
                  if (task.IsCompletedSuccessfully) {
                      Debug.Log($"[PendingPlayCountQueue] Flushed +{count} for '{setId}'.");
                      lock (_pending) {
                          if (_pending.TryGetValue(setId, out int cur)) {
                              int leftover = cur - count;
                              if (leftover <= 0) _pending.Remove(setId);
                              else _pending[setId] = leftover;
                          }
                      }
                  } else {
                      Debug.LogWarning($"[PendingPlayCountQueue] Flush failed for '{setId}': " +
                                       $"{task.Exception?.InnerException?.Message}");
                  }

                  remaining--;
                  if (remaining > 0) return;
                  Save();
                  onComplete?.Invoke();
              });
        }
    }

    // ─────────────────────────────────────────────────────────
    // Persistence
    // ─────────────────────────────────────────────────────────

    void Load() {
        if (!File.Exists(QueuePath)) return;
        try {
            string json = SaveEncryption.Decrypt(File.ReadAllBytes(QueuePath));
            var wrapper = JsonUtility.FromJson<QueueWrapper>(json);
            _pending = new Dictionary<string, int>();
            if (wrapper?.entries == null) return;
            foreach (var e in wrapper.entries) _pending[e.setId] = e.count;
            Debug.Log($"[PendingPlayCountQueue] Loaded {_pending.Count} pending entry(ies).");
        } catch (InvalidCipherTextException) {
            Debug.LogWarning("[PendingPlayCountQueue] Queue file tampered. Discarding.");
            DeleteFile();
        } catch (Exception e) {
            Debug.LogWarning($"[PendingPlayCountQueue] Load error: {e.Message}");
        }
    }

    void Save() {
        try {
            var wrapper = new QueueWrapper { entries = new List<QueueEntry>() };
            foreach (var (setId, count) in _pending)
                wrapper.entries.Add(new QueueEntry { setId = setId, count = count });

            string json = JsonUtility.ToJson(wrapper);
            byte[] encrypted = SaveEncryption.Encrypt(json);
            File.WriteAllBytes(QueuePath, encrypted);
        } catch (Exception e) {
            Debug.LogWarning($"[PendingPlayCountQueue] Save error: {e.Message}");
        }
    }

    void DeleteFile() {
        try { if (File.Exists(QueuePath)) File.Delete(QueuePath); } catch { /* ignored */ }
    }

    [Serializable] class QueueWrapper { public List<QueueEntry> entries; }
    [Serializable] class QueueEntry { public string setId; public int count; }
}