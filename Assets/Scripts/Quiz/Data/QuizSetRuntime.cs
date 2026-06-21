using System.Collections.Generic;

// Runtime quiz set — a named collection of questions.
// Created from QuizSetData SOs or parsed from fetched JSON.
[System.Serializable]
public class QuizSetRuntime {
    public string name;
    public List<QuestionRuntime> questions = new();

    // Where this set came from — useful for debugging
    public enum SourceType { Local, Fetched }
    public SourceType source = SourceType.Local;
}