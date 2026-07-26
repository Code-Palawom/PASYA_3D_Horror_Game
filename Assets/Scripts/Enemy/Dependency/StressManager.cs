using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Server-side singleton, parallel in structure to NoiseManager. Every
// tickInterval, computes each registered player's ambient stress as the
// STRONGEST single contribution from any registered enemy — closer +
// more aggressive enemy state = higher contribution — and pushes it into
// that player's SanityController. This replaces a hard hunted/not-hunted
// binary with a continuous 0-1 value that ramps even while an enemy is
// just patrolling nearby.
public class StressManager : NetworkBehaviour {
    public static StressManager Singleton { get; private set; }

    [Header("Distance falloff per enemy state (0 stress at/after this distance)")]
    [SerializeField] private float patrolMaxDistance = 15f;
    [SerializeField] private float investigateMaxDistance = 20f;
    [SerializeField] private float huntMaxDistance = 30f;

    [Header("Peak contribution per enemy state (at distance 0)")]
    [SerializeField] private float patrolPeakStress = 0.15f;
    [SerializeField] private float investigatePeakStress = 0.35f;
    [SerializeField] private float huntPeakStress = 1f;

    [SerializeField] private float tickInterval = 0.25f;
    private float tickTimer;

    private readonly List<EnemyController> registeredEnemies = new List<EnemyController>();
    private readonly List<SanityController> registeredPlayers = new List<SanityController>();

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

    public void RegisterPlayer(SanityController player) {
        if (!registeredPlayers.Contains(player)) registeredPlayers.Add(player);
    }

    public void UnregisterPlayer(SanityController player) {
        registeredPlayers.Remove(player);
    }

    private void Update() {
        if (!IsServer) return;

        tickTimer += Time.deltaTime;
        if (tickTimer < tickInterval) return;
        tickTimer = 0f;

        registeredEnemies.RemoveAll(e => e == null);
        registeredPlayers.RemoveAll(p => p == null);

        foreach (var player in registeredPlayers) {
            float highestStress = 0f;
            foreach (var enemy in registeredEnemies) {
                float contribution = ComputeContribution(enemy, player.transform.position);
                if (contribution > highestStress) highestStress = contribution;
            }
            player.ServerSetTargetStress(highestStress);
        }
    }

    private float ComputeContribution(EnemyController enemy, Vector3 playerPos) {
        float distance = Vector3.Distance(enemy.transform.position, playerPos);

        switch (enemy.CurrentState) {
            case EnemyState.Hunt:
            case EnemyState.Attack:
                return Falloff(distance, huntMaxDistance, huntPeakStress);
            case EnemyState.Investigate:
            case EnemyState.Search:
                return Falloff(distance, investigateMaxDistance, investigatePeakStress);
            default: // Patrol
                return Falloff(distance, patrolMaxDistance, patrolPeakStress);
        }
    }

    private float Falloff(float distance, float maxDistance, float peak) {
        if (distance >= maxDistance) return 0f;
        float t = 1f - (distance / maxDistance);
        return peak * t;
    }
}