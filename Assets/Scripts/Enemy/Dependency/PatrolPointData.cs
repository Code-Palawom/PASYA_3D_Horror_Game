using UnityEngine;

// One patrol point — position and optional facing rotation (used when idling at the point).
[System.Serializable]
public class PatrolPointData {
    public Vector3 position;
    public Quaternion rotation = Quaternion.identity;
}