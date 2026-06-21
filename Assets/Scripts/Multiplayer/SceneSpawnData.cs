using System.Collections.Generic;
using UnityEngine;

// ScriptableObject holding spawn points for ONE scene.
// Create one asset per scene (e.g. LobbySpawns, LevelSpawns).
[CreateAssetMenu(menuName = "Quiz/SpawnData/SceneSpawnData")]
public class SceneSpawnData : ScriptableObject {
    [Tooltip("Must exactly match the scene name in Build Settings.")]
    public string sceneName;

    [Tooltip("One entry per spawn slot. Players are assigned in round-robin order.")]
    public List<SpawnPointData> spawnPoints = new();
}