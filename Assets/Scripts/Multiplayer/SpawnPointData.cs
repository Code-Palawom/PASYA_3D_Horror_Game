using UnityEngine;

// One spawn point entry — position and rotation.
[System.Serializable]
public class SpawnPointData {
    public Vector3 position;
    public Quaternion rotation = Quaternion.identity;
}