#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Place this script inside a folder named "Editor" anywhere in Assets
// (e.g. Assets/Scripts/Tasks/Editor/) — Unity excludes anything in an
// "Editor" folder from player builds automatically.
[CustomEditor(typeof(TaskDefinition))]
public class TaskDefinitionEditor : Editor {
    SerializedProperty _taskId, _title, _description, _type;
    SerializedProperty _targetGateId;
    SerializedProperty _requiredCount;
    SerializedProperty _targetDifficulty, _targetTag;

    void OnEnable() {
        _taskId = serializedObject.FindProperty(nameof(TaskDefinition.taskId));
        _title = serializedObject.FindProperty(nameof(TaskDefinition.title));
        _description = serializedObject.FindProperty(nameof(TaskDefinition.description));
        _type = serializedObject.FindProperty(nameof(TaskDefinition.type));

        _targetGateId = serializedObject.FindProperty(nameof(TaskDefinition.targetGateId));
        _requiredCount = serializedObject.FindProperty(nameof(TaskDefinition.requiredCount));
        _targetDifficulty = serializedObject.FindProperty(nameof(TaskDefinition.targetDifficulty));
        _targetTag = serializedObject.FindProperty(nameof(TaskDefinition.targetTag));
    }

    public override void OnInspectorGUI() {
        serializedObject.Update();

        EditorGUILayout.PropertyField(_taskId);
        EditorGUILayout.PropertyField(_title);
        EditorGUILayout.PropertyField(_description);

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(_type);

        EditorGUILayout.Space();
        var type = (TaskType)_type.enumValueIndex;

        switch (type) {
            case TaskType.SpecificGate:
                EditorGUILayout.LabelField("Specific Gate", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_targetGateId,
                    new GUIContent("Target Gate Id", "Must match NetworkedQuizGate.GateId"));
                break;

            case TaskType.GenericCount:
                EditorGUILayout.LabelField("Generic Count", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_requiredCount,
                    new GUIContent("Required Count", "Correct answers on ANY gate"));
                break;

            case TaskType.DifficultyOrTag:
                EditorGUILayout.LabelField("Difficulty / Tag", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_targetDifficulty, new GUIContent("Target Difficulty"));
                EditorGUILayout.PropertyField(_targetTag,
                    new GUIContent("Target Tag", "Leave blank to match on difficulty alone"));
                break;
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif