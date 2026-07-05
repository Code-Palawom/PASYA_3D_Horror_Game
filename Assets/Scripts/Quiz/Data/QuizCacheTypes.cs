using System;
using System.Collections.Generic;

// Serializable types for the local quiz metadata cache (meta.bin).

// meta.bin  — stores lightweight set info for menu display only (no questions).
// {sha256(setId)}.bin — stores full QuizSetRuntime per set, written when downloaded.

// A set is shown in the menu only if isVerified == true and hasLocalData == true.
[Serializable]
public class QuizSetMetaEntry {
    public string setId;
    public string name;
    public string category;
    public int questionCount;
    public int playCount;
    public long lastUpdated;
    public bool hasLocalData;
    public bool isVerified;

    // ── Author ─────────────────────────────────────────────
    public string authorId;
    public string authorName;
}

[Serializable]
public class MetaCacheWrapper {
    public List<QuizSetMetaEntry> entries = new();
}