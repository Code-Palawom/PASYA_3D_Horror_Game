using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Minimal list UI: shows the active group's name and its visible tasks,
// struck through when completed. TaskManager already caps how many
// incomplete tasks are handed back, so this just renders what it gets.
//
// taskEntryPrefab needs a Button component (on the root or the text
// object) plus a TMP_Text child — clicking a task's title shows its
// description as a local toast via ToastNotification.ShowLocalToast.
public class TaskListUI : MonoBehaviour {
    [SerializeField] Transform listContainer;
    [SerializeField] GameObject taskEntryPrefab;
    [SerializeField] TMP_Text groupTitleText; // optional

    private Action _onTasksChanged;

    void OnEnable() {
        _onTasksChanged = () => Refresh();

        groupTitleText.gameObject.SetActive(false);
        SceneManager.sceneLoaded += OnSceneLoaded;

        if (SceneManager.GetActiveScene().name == "Lobby") {
            groupTitleText.gameObject.SetActive(false);
            return;
        }

        groupTitleText.gameObject.SetActive(true);
        if (TaskManager.Instance != null) TaskManager.Instance.OnTasksChanged += _onTasksChanged;
        Refresh();
    }

    void OnDisable() {
        if (TaskManager.Instance != null) TaskManager.Instance.OnTasksChanged -= _onTasksChanged;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        if (scene.name == "Lobby") {
            groupTitleText.gameObject.SetActive(false);
            return;
        }

        groupTitleText.gameObject.SetActive(true);
        if (TaskManager.Instance != null) TaskManager.Instance.OnTasksChanged += _onTasksChanged;
        Refresh();
    }

    void Refresh() {
        foreach (Transform child in listContainer) Destroy(child.gameObject);
        if (TaskManager.Instance == null) return;

        if (groupTitleText != null)
            groupTitleText.text = TaskManager.Instance.AllGroupsCompleted
                ? "All tasks complete"
                : $"Task: {TaskManager.Instance.CurrentGroupName}";

        foreach (var (def, progress) in TaskManager.Instance.GetTasksForUI()) {
            var entry = Instantiate(taskEntryPrefab, listContainer);

            var text = entry.GetComponentInChildren<TMP_Text>();
            if (text != null) {
                text.text = progress.completed
                    ? $"<s>{def.title}</s>"
                    : $"{def.title}{(progress.taskType == TaskType.SpecificGate ? "" : $" ({progress.currentCount}/{progress.requiredCount})" )}";
            }

            var button = entry.GetComponentInChildren<Button>();
            if (button != null) {
                string description = def.description;
                button.onClick.AddListener(() => ActionbarToastNotification.Instance.ShowLocalToast(description));
            }
        }
    }
}