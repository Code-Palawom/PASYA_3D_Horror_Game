using System.Collections.Generic;
using UnityEngine;

// ScriptableObject holding enemy spawn points for ONE scene.
// Create one asset per scene (e.g. BasementEnemySpawns).
[CreateAssetMenu(menuName = "Enemy/SpawnData/SceneEnemySpawnData")]
public class SceneEnemySpawnData : ScriptableObject {
    [Tooltip("Must exactly match the scene name in Build Settings.")]
    public string sceneName;

    public List<EnemySpawnPointData> spawnPoints = new();
}