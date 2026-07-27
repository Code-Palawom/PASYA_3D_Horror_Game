using UnityEngine;

// Plain scene singleton (not networked) — only ever read on the server,
// inside PlayerHealth's jumpscare sequence coroutine. Holds a respawn
// point set separate from normal spawn points (e.g. SceneSpawnManager's).
public class RespawnManager : MonoBehaviour {
    public static RespawnManager Instance { get; private set; }

    [SerializeField] private Transform[] respawnPoints;

    private void Awake() {
        if (Instance != null && Instance != this) {
            Debug.LogWarning("[RespawnManager] Duplicate instance in scene, destroying extra.", this);
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public Vector3 GetRandomRespawnPoint(out Quaternion rotation) {
        if (respawnPoints == null || respawnPoints.Length == 0) {
            Debug.LogError("[RespawnManager] No respawn points assigned!", this);
            rotation = Quaternion.identity;
            return transform.position;
        }

        var point = respawnPoints[Random.Range(0, respawnPoints.Length)];
        rotation = point.rotation;
        return point.position;
    }
}