#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(QuestionData))]
public class QuestionDataEditor : Editor {
    public override void OnInspectorGUI() {
        serializedObject.Update();

        EditorGUILayout.LabelField("Common", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("questionText"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("questionType"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("difficulty"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("timeLimit"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("pointValue"));

        EditorGUILayout.Space();

        var typeProp = serializedObject.FindProperty("questionType");
        var type = (QuestionType)typeProp.enumValueIndex;

        switch (type) {
            case QuestionType.MultipleChoice:
                EditorGUILayout.LabelField("Multiple Choice", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("choices"), includeChildren: true);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("correctChoiceIndex"));
                break;

            case QuestionType.TrueOrFalse:
                EditorGUILayout.LabelField("True or False", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("answer"),
                    new GUIContent("Correct Answer (True/False)"));
                break;

            case QuestionType.FillInTheBlank:
                EditorGUILayout.LabelField("Fill in the Blank", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("correctAnswer"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("alternativeAnswers"), includeChildren: true);
                break;

            case QuestionType.ShortAnswer:
                EditorGUILayout.LabelField("Short Answer", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("acceptableAnswers"), includeChildren: true);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("requiredKeywords"), includeChildren: true);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("requiredKeywordCount"));
                break;
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif