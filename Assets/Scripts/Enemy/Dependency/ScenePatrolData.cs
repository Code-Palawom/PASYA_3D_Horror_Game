using System.Collections.Generic;
using UnityEngine;

// ScriptableObject holding patrol routes for ONE scene.
// Create one asset per scene (e.g. BasementPatrols, AtticPatrols).
[CreateAssetMenu(menuName = "Enemy/SpawnData/ScenePatrolData")]
public class ScenePatrolData : ScriptableObject {
    [Tooltip("Must exactly match the scene name in Build Settings.")]
    public string sceneName;

    [Tooltip("One entry per patrol route. Pair with enemy spawn points via groupId.")]
    public List<PatrolGroupData> patrolGroups = new();
}