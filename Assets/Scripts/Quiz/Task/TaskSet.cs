using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class TaskGroup {
    public string groupName;
    public List<TaskDefinition> tasks = new();
}

// One TaskSet asset per map. Groups activate sequentially — group N+1 only
// becomes active once every task in group N is completed.
[CreateAssetMenu(fileName = "TaskSet", menuName = "Tasks/Task Set")]
public class TaskSet : ScriptableObject {
    public List<TaskGroup> groups = new();
}