using UnityEngine;

public class Logger : MonoBehaviour {
    void Awake() {
        DontDestroyOnLoad(gameObject);
        Application.logMessageReceived += HandleLog;
    }

    void OnDisable() => Application.logMessageReceived -= HandleLog;

    void HandleLog(string msg, string stack, LogType type) {
        string path = Application.persistentDataPath + "/log.txt";
        System.IO.File.AppendAllText(path,
            $"[{type}] {msg}\n{stack}\n---\n");
    }
}