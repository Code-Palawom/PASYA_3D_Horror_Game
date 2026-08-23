#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Must live in a folder named "Editor" anywhere under Assets
// (e.g. Assets/Editor/AchievementDefinitionSOEditor.cs) or Unity won't
// exclude it from player builds.
[CustomEditor(typeof(AchievementDefinitionSO))]
public class AchievementDefinitionSOEditor : Editor {
    private SerializedProperty _achievementId;
    private SerializedProperty _displayName;
    private SerializedProperty _description;
    private SerializedProperty _icon;
    private SerializedProperty _hidden;
    private SerializedProperty _triggerType;
    private SerializedProperty _stat;
    private SerializedProperty _threshold;
    private SerializedProperty _eventKey;
    private SerializedProperty _subject;
    private SerializedProperty _requiredCompletionPercent;
    private SerializedProperty _requiredSetId;
    private SerializedProperty _rewardCoins;
    private SerializedProperty _rewardXp;
    private SerializedProperty _rewardSkinId;

    private void OnEnable() {
        _achievementId = serializedObject.FindProperty(nameof(AchievementDefinitionSO.achievementId));
        _displayName = serializedObject.FindProperty(nameof(AchievementDefinitionSO.displayName));
        _description = serializedObject.FindProperty(nameof(AchievementDefinitionSO.description));
        _icon = serializedObject.FindProperty(nameof(AchievementDefinitionSO.icon));
        _hidden = serializedObject.FindProperty(nameof(AchievementDefinitionSO.hidden));
        _triggerType = serializedObject.FindProperty(nameof(AchievementDefinitionSO.triggerType));
        _stat = serializedObject.FindProperty(nameof(AchievementDefinitionSO.stat));
        _threshold = serializedObject.FindProperty(nameof(AchievementDefinitionSO.threshold));
        _eventKey = serializedObject.FindProperty(nameof(AchievementDefinitionSO.eventKey));
        _subject = serializedObject.FindProperty(nameof(AchievementDefinitionSO.subject));
        _requiredCompletionPercent = serializedObject.FindProperty(nameof(AchievementDefinitionSO.requiredCompletionPercent));
        _requiredSetId = serializedObject.FindProperty(nameof(AchievementDefinitionSO.requiredSetId));
        _rewardCoins = serializedObject.FindProperty(nameof(AchievementDefinitionSO.rewardCoins));
        _rewardXp = serializedObject.FindProperty(nameof(AchievementDefinitionSO.rewardXp));
        _rewardSkinId = serializedObject.FindProperty(nameof(AchievementDefinitionSO.rewardSkinId));
    }

    public override void OnInspectorGUI() {
        serializedObject.Update();

        EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_achievementId);
        if (string.IsNullOrWhiteSpace(_achievementId.stringValue)) {
            EditorGUILayout.HelpBox("achievementId is empty — this is the stable id stored in PlayerProfile.UnlockedAchievementIds. Set it before shipping.", MessageType.Warning);
        }
        EditorGUILayout.PropertyField(_displayName);
        EditorGUILayout.PropertyField(_description);
        EditorGUILayout.PropertyField(_icon);
        EditorGUILayout.PropertyField(_hidden);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Trigger", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_triggerType);

        var triggerType = (AchievementTriggerType)_triggerType.enumValueIndex;

        EditorGUI.indentLevel++;
        if (triggerType == AchievementTriggerType.StatThreshold) {
            EditorGUILayout.PropertyField(_stat);
            EditorGUILayout.PropertyField(_threshold);
            if (_threshold.longValue <= 0) {
                EditorGUILayout.HelpBox("Threshold is 0 or less — this would unlock immediately for every player.", MessageType.Warning);
            }
        } else if (triggerType == AchievementTriggerType.CustomEvent) {
            EditorGUILayout.PropertyField(_eventKey);
            if (string.IsNullOrWhiteSpace(_eventKey.stringValue)) {
                EditorGUILayout.HelpBox("eventKey is empty — AchievementManager.ReportEvent(...) calls need to match this exactly to unlock this achievement.", MessageType.Warning);
            }
        } else if (triggerType == AchievementTriggerType.SubjectCompletion) {
            EditorGUILayout.PropertyField(_subject);
            if (string.IsNullOrWhiteSpace(_subject.stringValue)) {
                EditorGUILayout.HelpBox("subject is empty — this must match a QuizSetMetaEntry.subject value exactly, or this achievement can never unlock.", MessageType.Warning);
            }
            EditorGUILayout.PropertyField(_requiredCompletionPercent);
            if (Mathf.Approximately(_requiredCompletionPercent.floatValue, 100f)) {
                EditorGUILayout.HelpBox("Requires 100% — every currently playable set in this subject must be completed.", MessageType.Info);
            }
        } else if (triggerType == AchievementTriggerType.QuizSetCompletion) {
            EditorGUILayout.PropertyField(_requiredSetId);
            if (string.IsNullOrWhiteSpace(_requiredSetId.stringValue)) {
                EditorGUILayout.HelpBox("requiredSetId is empty — this must match a QuizSetMetaEntry.setId exactly, or this achievement can never unlock.", MessageType.Warning);
            }
        }
        EditorGUI.indentLevel--;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Reward", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_rewardCoins);
        EditorGUILayout.PropertyField(_rewardXp);
        EditorGUILayout.PropertyField(_rewardSkinId);
        if (_rewardCoins.intValue == 0 && _rewardXp.intValue == 0 && string.IsNullOrWhiteSpace(_rewardSkinId.stringValue)) {
            EditorGUILayout.HelpBox("No reward set — this achievement will unlock as a badge only.", MessageType.Info);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif