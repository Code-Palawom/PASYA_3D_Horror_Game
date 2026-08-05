using System.Collections.Generic;
using UnityEngine;

// Same pattern as your scene-spawn assets, but for the end-game podium
// spots players get teleported to when a game ends. One asset per level
// scene. Reuses the existing SpawnPointData type (position + rotation)
// that RepositionClient already uses.
[CreateAssetMenu(fileName = "EndGameTeleportSet", menuName = "End Game/Teleport Set")]
public class EndGameTeleportSetSO : ScriptableObject {
    public string sceneName;
    public List<SpawnPointData> teleportPoints;
}