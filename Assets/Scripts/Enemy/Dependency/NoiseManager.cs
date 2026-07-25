using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Server-side singleton. Enemies register themselves on spawn; when a
// noise is reported, any enemy within (loudness * radiusMultiplier) of
// the source gets notified. Runs only on the server/host.
public class NoiseManager : NetworkBehaviour {
    public static NoiseManager Singleton { get; private set; }

    [SerializeField] private float radiusMultiplier = 3f; // loudness -> detection radius

    private readonly List<EnemyController> registeredEnemies = new List<EnemyController>();

    public override void OnNetworkSpawn() {
        if (!IsServer) { enabled = false; return; }
        Singleton = this;
    }

    public override void OnNetworkDespawn() {
        if (Singleton == this) Singleton = null;
    }

    public void RegisterEnemy(EnemyController enemy) {
        if (!registeredEnemies.Contains(enemy)) registeredEnemies.Add(enemy);
    }

    public void UnregisterEnemy(EnemyController enemy) {
        registeredEnemies.Remove(enemy);
    }

    public void ReportNoise(Vector3 position, float loudness, ulong sourceClientId) {
        float radius = loudness * radiusMultiplier;

        foreach (var enemy in registeredEnemies) {
            if (enemy == null) continue;
            float dist = Vector3.Distance(enemy.transform.position, position);
            if (dist <= radius)
                enemy.HearNoise(position, loudness);
        }
    }
}