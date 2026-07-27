using UnityEngine;

// One entry per enemy type. enemyType matches EnemySpawnPointData.enemyType /
// EnemySpawnManager.EnemyTypeEntry.typeId. When a jumpscare fires for that
// enemy type, the player is teleported to playerPosition/playerEulerRotation
// and the scene prop registered under propId (see JumpscarePropRegistry)
// plays animationTrigger. The prop itself is NOT a networked enemy — it's a
// static, pre-placed visual with an Animator, so this asset only needs to
// store an id string for it, not a scene reference (assets can't hold those).
[CreateAssetMenu(fileName = "JumpscareLocationSet", menuName = "Jumpscare/Location Set")]
public class JumpscareLocationSet : ScriptableObject {

    [System.Serializable]
    public class Entry {
        [Tooltip("Matches EnemySpawnPointData.enemyType / EnemyTypeEntry.typeId.")]
        public string enemyType;

        [Header("Where the player is sent")]
        public Vector3 playerPosition;
        public Vector3 playerEulerRotation; // facing the prop

        [Header("Prop to animate (looked up at runtime, see JumpscarePropRegistry)")]
        public string propId;
        public string animationTrigger = "Jumpscare";
    }

    public Entry[] entries;

    public Entry GetEntry(string enemyType) {
        foreach (var entry in entries) {
            if (entry.enemyType == enemyType) return entry;
        }
        return null;
    }
}