#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// Editor-only tool: reads every achievementId from an AchievementDatabaseSO
// and produces a ready-to-paste JSON array for config/localAchievementIds'
// "Ids" field in the Firebase console — see the Firestore rules'
// isValidAchievementGrant()/knownAchievementIds() functions for why that doc
// needs to exist and stay in sync with your local achievement assets.
//
// Deliberately does NOT write to Firestore directly — that would need the
// Editor to authenticate as a Developer-role account (your rules require
// callerRole() == "Developer" to write under config/), which means either an
// interactive sign-in flow or cached credentials sitting in EditorPrefs/a
// local file, plus pumping Firebase's async calls manually via
// EditorApplication.update since Edit mode doesn't run Unity's normal
// per-frame coroutine machinery. Not worth it for a low-frequency
// maintenance task — this just generates the value; you paste it into the
// console (or a seed script) yourself.
//
// Must live in a folder named "Editor" anywhere under Assets.
public class AchievementIdExporterWindow : EditorWindow {
    private AchievementDatabaseSO database;
    private string generatedJson = "";
    private Vector2 scroll;

    [MenuItem("Tools/Pasya/Export Local Achievement IDs")]
    private static void Open() {
        var window = GetWindow<AchievementIdExporterWindow>("Achievement ID Export");
        window.minSize = new Vector2(420, 320);
    }

    private void OnGUI() {
        EditorGUILayout.HelpBox(
            "Generates the JSON array to paste into config/localAchievementIds' " +
            "'Ids' field in the Firebase console, so Firestore Security Rules can " +
            "validate grants for local (non-Firestore-sourced) achievements. " +
            "This does not write to Firestore — copy the result yourself.",
            MessageType.Info);

        EditorGUILayout.Space();
        database = (AchievementDatabaseSO)EditorGUILayout.ObjectField(
            "Achievement Database", database, typeof(AchievementDatabaseSO), false);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(database == null)) {
            if (GUILayout.Button("Generate", GUILayout.Height(28))) Generate();
        }

        if (!string.IsNullOrEmpty(generatedJson)) {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Result", EditorStyles.boldLabel);

            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(140));
            EditorGUILayout.TextArea(generatedJson, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Copy to Clipboard")) {
                EditorGUIUtility.systemCopyBuffer = generatedJson;
                Debug.Log("[AchievementIdExporter] Copied to clipboard.");
            }
            if (GUILayout.Button("Save to File...")) SaveToFile();
            EditorGUILayout.EndHorizontal();
        }
    }

    private void Generate() {
        if (database == null || database.achievements == null) {
            generatedJson = "[]";
            return;
        }

        var ids = database.achievements
            .Where(a => a != null)
            .Select(a => a.achievementId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToList();

        var duplicates = ids.GroupBy(id => id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicates.Count > 0) {
            Debug.LogWarning($"[AchievementIdExporter] Duplicate achievementId(s) found in '{database.name}': {string.Join(", ", duplicates)}. Each achievementId should be unique — fix these in the database before relying on the export.");
        }

        var missingCount = database.achievements.Count(a => a != null && string.IsNullOrWhiteSpace(a.achievementId));
        if (missingCount > 0) {
            Debug.LogWarning($"[AchievementIdExporter] {missingCount} entry(ies) in '{database.name}' have no achievementId set — skipped from the export.");
        }

        var distinctSortedIds = ids.Distinct().OrderBy(id => id).ToList();
        generatedJson = BuildJsonArray(distinctSortedIds);

        Debug.Log($"[AchievementIdExporter] Generated {distinctSortedIds.Count} unique achievement id(s) from '{database.name}'.");
    }

    private static string BuildJsonArray(System.Collections.Generic.List<string> ids) {
        var sb = new StringBuilder();
        sb.Append("[\n");
        for (int i = 0; i < ids.Count; i++) {
            sb.Append("  \"").Append(ids[i]).Append('"');
            if (i < ids.Count - 1) sb.Append(',');
            sb.Append('\n');
        }
        sb.Append(']');
        return sb.ToString();
    }

    private void SaveToFile() {
        string path = EditorUtility.SaveFilePanel("Save Achievement IDs", Application.dataPath, "localAchievementIds", "json");
        if (string.IsNullOrEmpty(path)) return;
        File.WriteAllText(path, generatedJson);
        Debug.Log($"[AchievementIdExporter] Saved to '{path}'.");
    }
}
#endif