using UnityEngine;

// One enemy spawn slot. groupId (if set) pairs this spawn point to a
// PatrolGroupData with the same groupId, so an enemy spawned here
// walks that specific route. enemyType (if set) pins this spawn point to
// a specific EnemySpawnManager.EnemyTypeEntry.typeId — leave empty to let
// the spawner pick a random type for this slot.
[System.Serializable]
public class EnemySpawnPointData {
    public Vector3 position;
    public Quaternion rotation = Quaternion.identity;

    [Tooltip("Optional — matches a PatrolGroupData.groupId in ScenePatrolData.")]
    public string groupId;

    [Tooltip("Optional — matches an EnemyTypeEntry.typeId on EnemySpawnManager. Empty = random type.")]
    public string enemyType;
}