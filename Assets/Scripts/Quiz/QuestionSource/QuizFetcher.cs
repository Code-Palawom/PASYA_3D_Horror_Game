using System;
using System.Collections.Generic;
using UnityEngine;
using Firebase.Firestore;
using Firebase.Extensions;

// Manages quiz set fetching via a Firestore snapshot listener.

// Startup flow:
//   1. LoadCacheImmediately() → populate menu from meta.bin right away
//   2. StartListening()       → attach snapshot listener
//      - On first fire (or any change): diff, flush queue, background-download stale sets
//      - Only sets where isVerified == true are processed
//      - On error: log + fire OnFetchStatus(false, message)

// Firestore set entry fields:
//   lastUpdated, name, category, questionCount, playCount,
//   isVerified, author: { id, name }

// Events:
//   OnSetReady(entry)           — fires per verified set as background downloads complete
//   OnFetchStatus(bool, string) — true = success, false = error (for status bar)
public class QuizFetcher : MonoBehaviour {
    public static QuizFetcher Instance { get; private set; }

    [Header("Firestore Settings")]
    [SerializeField] string collectionName = "";
    [SerializeField] string metaDocId = "";

    public event Action<QuizSetMetaEntry> OnSetReady;
    public event Action<bool, string> OnFetchStatus;
    public event Action OnFirebaseInit;

    FirebaseFirestore db;
    ListenerRegistration Listener;

    // ─────────────────────────────────────────────────────────
    void Awake() {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy() => StopListening();

    // ─────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────

    public void Init(FirebaseFirestore firestoreInstance) {
        db = firestoreInstance;
        OnFirebaseInit?.Invoke();
    }

    // Step 1 — call immediately after Init().
    // Loads meta.bin so the menu can populate before the listener fires.
    // Only returns verified entries.
    public List<QuizSetMetaEntry> LoadCacheImmediately() {
        var all = QuizRepository.Instance.LoadCache();
        var verified = all.FindAll(e => e.isVerified);
        Debug.Log($"[QuizFetcher] Cache: {all.Count} total, {verified.Count} verified.");
        return verified;
    }

    // Step 2 — attach the  snapshot listener.
    // Fires immediately with current Firestore state, then on every remote change.
    public void StartListening() {
        if (db == null) { Debug.LogWarning("[QuizFetcher] Not initialized."); return; }
        if (Listener != null) return;

        Debug.Log("[QuizFetcher] Attaching  listener...");
        Listener = db.Collection(collectionName).Document(metaDocId)
            .Listen(MetadataChanges.Exclude, snapshot => {
                if (snapshot == null) {
                    OnMetaError(new Exception("Snapshot is null."));
                    return;
                }
                OnMetaSnapshot(snapshot);
            });
    }

    public void StopListening() {
        Listener?.Stop();
        Listener = null;
    }

    // Queues or immediately writes a playCount increment.
    // Online → direct Firestore write. Offline → persisted queue.
    public void IncrementPlayCount(string setId) {
        if (string.IsNullOrWhiteSpace(setId)) return;

        if (IsConnected() && db != null) {
            db.Collection(collectionName).Document(metaDocId)
              .UpdateAsync($"sets.{setId}.playCount", FieldValue.Increment(1))
              .ContinueWithOnMainThread(task => {
                  if (!task.IsCompletedSuccessfully) {
                      Debug.LogWarning($"[QuizFetcher] playCount write failed for '{setId}'. Queuing.");
                      PendingPlayCountQueue.Instance.Enqueue(setId);
                  } else {
                      Debug.Log($"[QuizFetcher] Incremented playCount for '{setId}'.");
                  }
              });
        } else {
            Debug.Log($"[QuizFetcher] Offline — queuing playCount for '{setId}'.");
            PendingPlayCountQueue.Instance.Enqueue(setId);
        }
    }

    // ─────────────────────────────────────────────────────────
    // Snapshot listener callbacks
    // ─────────────────────────────────────────────────────────

    void OnMetaSnapshot(DocumentSnapshot snapshot) {
        if (!snapshot.Exists) {
            Debug.LogWarning("[QuizFetcher]  document does not exist.");
            OnFetchStatus?.Invoke(false, "Failed to update quiz sets.");
            return;
        }

        Debug.Log("[QuizFetcher]  snapshot received. Diffing...");

        if (PendingPlayCountQueue.Instance.HasPending)
            PendingPlayCountQueue.Instance.Flush(db, collectionName, metaDocId);

        Dictionary<string, QuizSetMetaEntry> remoteMeta = ParseMeta(snapshot);
        List<QuizSetMetaEntry> localMeta = QuizRepository.Instance.LoadCache();
        Dictionary<string, QuizSetMetaEntry> localDict = new();
        foreach (var e in localMeta) localDict[e.setId] = e;

        // Deletions — remove sets no longer in Firestore or no longer verified
        foreach (var entry in localMeta) {
            bool removedFromFirestore = !remoteMeta.ContainsKey(entry.setId);
            bool unverified = remoteMeta.TryGetValue(entry.setId, out var remote) && !remote.isVerified;

            if (removedFromFirestore || unverified) {
                QuizRepository.Instance.DeleteSetFromDisk(entry.setId);
                Debug.Log($"[QuizFetcher] Removed set '{entry.setId}' " +
                          $"(reason: {(removedFromFirestore ? "deleted" : "unverified")}).");
            }
        }

        // Build updated meta + download list — verified sets only
        var updatedMeta = new List<QuizSetMetaEntry>();
        var toDownload = new List<string>();

        foreach (var (setId, entry) in remoteMeta) {
            // Always save all entries to meta.bin (including unverified)
            // so we can react to isVerified changing later
            bool existsLocally = QuizRepository.Instance.SetExistsOnDisk(setId);
            bool isStale = localDict.TryGetValue(setId, out var local)
                                 && local.lastUpdated < entry.lastUpdated;

            entry.hasLocalData = existsLocally && !isStale;
            updatedMeta.Add(entry);

            // Only download verified sets
            if (entry.isVerified && (!existsLocally || isStale))
                toDownload.Add(setId);
        }

        QuizRepository.Instance.SaveMetaCache(updatedMeta);
        OnFetchStatus?.Invoke(true, "Quiz sets updated.");

        if (toDownload.Count == 0) { Debug.Log("[QuizFetcher] All verified sets up to date."); return; }

        Debug.Log($"[QuizFetcher] Background downloading {toDownload.Count} verified set(s)...");

        Dictionary<string, QuizSetMetaEntry> metaLookup = new();
        foreach (var e in updatedMeta) metaLookup[e.setId] = e;

        foreach (string setId in toDownload)
            DownloadSetInBackground(setId, metaLookup);
    }

    void OnMetaError(Exception e) {
        Debug.LogError($"[QuizFetcher]  listener error: {e?.Message}");
        OnFetchStatus?.Invoke(false, "Failed to update quiz sets.");
    }

    // ─────────────────────────────────────────────────────────
    // Background download
    // ─────────────────────────────────────────────────────────

    void DownloadSetInBackground(string setId, Dictionary<string, QuizSetMetaEntry> metaLookup) {
        db.Collection(collectionName).Document(setId)
          .GetSnapshotAsync()
          .ContinueWithOnMainThread(docTask => {
              if (!docTask.IsCompletedSuccessfully || !docTask.Result.Exists) {
                  Debug.LogWarning($"[QuizFetcher] Background download failed: '{setId}'.");
                  return;
              }

              QuizSetRuntime set = ParseDocument(docTask.Result);
              if (set == null) return;

              metaLookup.TryGetValue(setId, out var entry);
              if (entry != null) {
                  set.category = entry.category;
                  set.questionCount = entry.questionCount;
                  set.playCount = entry.playCount;
                  entry.hasLocalData = true;
              }

              QuizRepository.Instance.SaveSetToDisk(setId, set);

              var currentMeta = QuizRepository.Instance.LoadCache();
              var toUpdate = currentMeta.Find(e => e.setId == setId);
              if (toUpdate != null) {
                  toUpdate.hasLocalData = true;
                  QuizRepository.Instance.SaveMetaCache(currentMeta);
              }

              Debug.Log($"[QuizFetcher] Background download complete: '{set.name}'");
              OnSetReady?.Invoke(entry);
          });
    }

    // ─────────────────────────────────────────────────────────
    // Parsing
    // ─────────────────────────────────────────────────────────

    Dictionary<string, QuizSetMetaEntry> ParseMeta(DocumentSnapshot meta) {
        var result = new Dictionary<string, QuizSetMetaEntry>();
        if (!meta.TryGetValue("sets", out Dictionary<string, object> sets)) return result;

        foreach (var (setId, val) in sets) {
            if (val is not Dictionary<string, object> fields) continue;

            long ts = 0;
            if (fields.TryGetValue("lastUpdated", out var tsVal)) {
                if (tsVal is Timestamp stamp) ts = ((DateTimeOffset)stamp.ToDateTime()).ToUnixTimeSeconds();
                else if (tsVal is long l) ts = l;
            }

            // Parse author sub-object
            string authorId = "";
            string authorName = "";
            if (fields.TryGetValue("author", out var authorVal) &&
                authorVal is Dictionary<string, object> authorFields) {
                authorId = authorFields.TryGetValue("id", out var aid) ? aid.ToString() : "";
                authorName = authorFields.TryGetValue("name", out var aname) ? aname.ToString() : "";
            }

            result[setId] = new QuizSetMetaEntry {
                setId = setId,
                name = fields.TryGetValue("name", out var n) ? n.ToString() : setId,
                category = fields.TryGetValue("category", out var cat) ? cat.ToString() : "",
                questionCount = fields.TryGetValue("questionCount", out var qc) ? Convert.ToInt32(qc) : 0,
                playCount = fields.TryGetValue("playCount", out var pc) ? Convert.ToInt32(pc) : 0,
                isVerified = fields.TryGetValue("isVerified", out var iv) && Convert.ToBoolean(iv),
                lastUpdated = ts,
                authorId = authorId,
                authorName = authorName
            };
        }

        return result;
    }

    QuizSetRuntime ParseDocument(DocumentSnapshot doc) {
        try {
            string name = doc.GetValue<string>("name");
            if (string.IsNullOrWhiteSpace(name)) return null;

            var questions = new List<QuestionRuntime>();

            if (!doc.TryGetValue("questions", out List<object> rawList) || rawList == null) {
                Debug.LogWarning($"[QuizFetcher] '{doc.Id}' has no questions field.");
                return new QuizSetRuntime { name = name };
            }

            foreach (var rawObj in rawList) {
                if (rawObj is not Dictionary<string, object> q) continue;
                var r = ParseQuestion(doc.Id, q);
                if (r != null) questions.Add(r);
            }

            return new QuizSetRuntime { name = name, questions = questions };
        } catch (Exception e) {
            Debug.LogWarning($"[QuizFetcher] Parse error '{doc.Id}': {e.Message}");
            return null;
        }
    }

    QuestionRuntime ParseQuestion(string docId, Dictionary<string, object> q) {
        if (!q.TryGetValue("type", out var typeVal)) {
            Debug.LogWarning($"[QuizFetcher] '{docId}': question missing 'type'. Skipping.");
            return null;
        }

        var type = (QuestionType)Convert.ToInt32(typeVal);
        var difficulty = q.TryGetValue("difficulty", out var dif)
                         ? (QuestionDifficulty)Convert.ToInt32(dif)
                         : QuestionDifficulty.Easy;

        var r = new QuestionRuntime {
            questionText = q.TryGetValue("question", out var qt) ? qt.ToString() : "",
            questionType = type,
            difficulty = difficulty,
            timeLimit = q.TryGetValue("timeLimit", out var tl) ? Convert.ToSingle(tl) : 15f,
            pointValue = q.TryGetValue("pointValue", out var pv) ? Convert.ToInt32(pv) : 100,
        };

        switch (type) {
            case QuestionType.MultipleChoice:
                if (!q.TryGetValue("choices", out var choicesVal) || choicesVal is not List<object> choiceList)
                    Debug.LogWarning($"[QuizFetcher] '{docId}': MultipleChoice missing 'choices'.");
                else
                    foreach (var c in choiceList) r.choices.Add(c.ToString());

                if (!q.TryGetValue("correctChoiceIndex", out var cci))
                    Debug.LogWarning($"[QuizFetcher] '{docId}': MultipleChoice missing 'correctChoiceIndex'.");
                else
                    r.correctChoiceIndex = Convert.ToInt32(cci);
                break;

            case QuestionType.TrueOrFalse:
                if (!q.TryGetValue("answer", out var ansVal))
                    Debug.LogWarning($"[QuizFetcher] '{docId}': TrueOrFalse missing 'answer'.");
                else {
                    r.answerBool = Convert.ToBoolean(ansVal);
                    r.correctChoiceIndex = r.answerBool ? 0 : 1;
                }
                break;

            case QuestionType.FillInTheBlank:
                if (!q.TryGetValue("correctAnswer", out var ca))
                    Debug.LogWarning($"[QuizFetcher] '{docId}': FillInTheBlank missing 'correctAnswer'.");
                else
                    r.correctAnswer = ca.ToString();

                if (!q.TryGetValue("alternativeAnswers", out var altVal) || altVal is not List<object> altList)
                    Debug.LogWarning($"[QuizFetcher] '{docId}': FillInTheBlank missing 'alternativeAnswers'.");
                else
                    foreach (var a in altList) r.alternativeAnswers.Add(a.ToString());
                break;

            case QuestionType.ShortAnswer:
                if (!q.TryGetValue("acceptableAnswers", out var accVal) || accVal is not List<object> accList)
                    Debug.LogWarning($"[QuizFetcher] '{docId}': ShortAnswer missing 'acceptableAnswers'.");
                else
                    foreach (var a in accList) r.acceptableAnswers.Add(a.ToString());

                if (!q.TryGetValue("requiredKeywords", out var kwVal) || kwVal is not List<object> kwList)
                    Debug.LogWarning($"[QuizFetcher] '{docId}': ShortAnswer missing 'requiredKeywords'.");
                else
                    foreach (var k in kwList) r.requiredKeywords.Add(k.ToString());

                if (!q.TryGetValue("requiredKeywordCount", out var kwc))
                    Debug.LogWarning($"[QuizFetcher] '{docId}': ShortAnswer missing 'requiredKeywordCount'.");
                else
                    r.requiredKeywordCount = Convert.ToInt32(kwc);
                break;
        }

        return r;
    }

    static bool IsConnected() =>
        Application.internetReachability != NetworkReachability.NotReachable;
}