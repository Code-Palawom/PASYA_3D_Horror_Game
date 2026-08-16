using System.Collections.Generic;

// Runtime quiz set — a named collection of questions.
// Created from QuizSetData SOs or parsed from Firestore via QuizFetcher.
// Meta fields (questionCount, playCount) are sourced from _meta,
// and left at defaults for local SO sets.
[System.Serializable]
public class QuizSetRuntime {
    public string setId;   // stable id — asset name for local sets, Firestore doc id for fetched
    public string name;
    public List<QuestionRuntime> questions = new();

    // Where this set came from — useful for debugging
    public enum SourceType { Local, Fetched }
    public SourceType source = SourceType.Local;

    // ── From _meta (Firestore only) ───────────────────────────
    public int questionCount = 0;
    public int playCount = 0;
}