using System.Collections.Generic;
using UnityEngine;

// A single patrol route/group. A scene can have several (e.g. one per
// enemy spawn point or per area) — groupId is how you pair a spawned
// enemy to the route you want it to walk.
[System.Serializable]
public class PatrolGroupData {
    [Tooltip("Free-form id used to match this route to an enemy spawn point.")]
    public string groupId;

    public List<PatrolPointData> points = new();
}