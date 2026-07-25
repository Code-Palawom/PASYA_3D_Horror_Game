using UnityEngine;

// One enemy spawn slot. groupId (if set) pairs this spawn point to a
// PatrolGroupData with the same groupId, so an enemy spawned here
// walks that specific route.
[System.Serializable]
public class EnemySpawnPointData {
    public Vector3 position;
    public Quaternion rotation = Quaternion.identity;

    [Tooltip("Optional — matches a PatrolGroupData.groupId in ScenePatrolData.")]
    public string groupId;
}