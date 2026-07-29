using UnityEngine;

public enum TaskType {
    SpecificGate,     // completed by one exact NetworkedQuizGate (matched via gateId)
    GenericCount,      // completed after N correct answers on ANY gate
    DifficultyOrTag     // completed by any gate matching a difficulty (and optional tag)
}

[CreateAssetMenu(fileName = "TaskDefinition", menuName = "Tasks/Task Definition")]
public class TaskDefinition : ScriptableObject {
    [Tooltip("Unique id, e.g. \"open_east_gate\". Must be unique within a TaskSet.")]
    public string taskId;

    public string title;
    [TextArea] public string description;

    public TaskType type;

    [Header("SpecificGate — must match NetworkedQuizGate.GateId")]
    public string targetGateId;

    [Header("GenericCount — any gate, N correct answers")]
    public int requiredCount = 1;

    [Header("DifficultyOrTag — matches by difficulty, optionally also by tag")]
    public QuestionDifficulty targetDifficulty;
    [Tooltip("Leave blank to match on difficulty alone.")]
    public string targetTag;
}