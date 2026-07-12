#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class Logger : MonoBehaviour {
    void Awake() {
        DontDestroyOnLoad(gameObject);
        Application.logMessageReceived += HandleLog;
    }

    void OnDisable() => Application.logMessageReceived -= HandleLog;

    void HandleLog(string msg, string stack, LogType type) {
        string path = Application.persistentDataPath + "/log.txt";
        System.IO.File.AppendAllText(path, $"[{type}] {msg}\n{stack}\n---\n");
    }

    [MenuItem("Tools/Find Missing Scripts In Scene")]
    static void Find() {
        var allObjects = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int count = 0;

        foreach (var go in allObjects) {
            var components = go.GetComponents<Component>();
            foreach (var c in components) {
                if (c == null) {
                    Debug.LogWarning($"Missing script on: {GetPath(go)}", go);
                    count++;
                }
            }
        }

        Debug.Log($"Missing script scan complete. Found {count} missing script(s).");
    }

    static string GetPath(GameObject go) {
        string path = go.name;
        var t = go.transform;
        while (t.parent != null) {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}
#endif