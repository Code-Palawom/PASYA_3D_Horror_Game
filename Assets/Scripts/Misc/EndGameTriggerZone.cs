using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

// Physical end-game trigger. Any player walking into this volume ends
// the game once, server-side:
//   1. GameSessionManager.SendResults() — broadcasts final stats to every
//      client, which is also what fires PlayerStatsRecorder's Firestore write.
//   2. Every currently-connected player is teleported to its own distinct
//      podium spot from podiumSet, assigned by connection order (NOT
//      clientId % count — that can put two players on the same spot).
//
// Can also be triggered remotely (e.g. from PlayerHUD) via
// RequestEndGameRpc(), which shares the same fire-once guard as the
// physical trigger.
[RequireComponent(typeof(Collider))]
public class EndGameTriggerZone : NetworkBehaviour {
    [SerializeField] EndGameTeleportSetSO teleportSet;

    public static EndGameTriggerZone Instance { get; private set; }

    bool _hasFired;

    void Awake() {
        Instance = this;
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other) {
        if (!IsServer || _hasFired) return;
        if (!other.TryGetComponent<Player>(out _)) return; // ignore non-player colliders

        TryFireEndGame();
    }

    // FOR TESTING: allow the end-game trigger to be fired via a keypress (server only) // SEE TaskListUI.cs for a client-side trigger that calls this via RPC.
    // Client-callable entry point (e.g. from PlayerHUD). Runs on the
    // server and shares the same fire-once guard as the physical trigger.
    [Rpc(SendTo.Server)]
    public void RequestEndGameRpc() {
        TryFireEndGame();
    }

    void TryFireEndGame() {
        if (_hasFired) return;
        _hasFired = true;
        StartCoroutine(EndGameSequence());
    }

    // 10s heads-up before results/teleport fire, shown via the podium
    // screen's title+countdown (PodiumScreenUI.ShowCountdown), broadcast
    // to every client each second. Players are NOT frozen during this
    // window, per design.
    IEnumerator EndGameSequence() {
        for (int remaining = 10; remaining > 0; remaining--) {
            CountdownUiRpc(remaining);
            yield return new WaitForSeconds(1f);
        }
        CountdownUiRpc(0);

        GameSessionManager.Instance.SendResults();
        RepositionAllPlayers();
    }

    [Rpc(SendTo.Everyone)]
    void CountdownUiRpc(int secondsRemaining) {
        EndGameScreenUI.Instance.ShowCountdown(secondsRemaining);
    }

    void RepositionAllPlayers() {
        if (teleportSet == null || teleportSet.teleportPoints == null || teleportSet.teleportPoints.Count == 0) {
            Debug.LogWarning("[EndGameTriggerZone] No podium points assigned — players not repositioned.");
            return;
        }

        List<ulong> clientIds = NetworkManager.Singleton.ConnectedClientsIds.ToList();

        if (clientIds.Count > teleportSet.teleportPoints.Count) {
            Debug.LogWarning($"[EndGameTriggerZone] {clientIds.Count} players but only " +
                              $"{teleportSet.teleportPoints.Count} podium spots defined — some will overlap. " +
                              "Add more spots to the asset.");
        }

        for (int i = 0; i < clientIds.Count; i++) {
            ulong clientId = clientIds[i];
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client)) continue;
            if (client.PlayerObject == null) continue;

            SpawnPointData sp = teleportSet.teleportPoints[i % teleportSet.teleportPoints.Count];
            client.PlayerObject.GetComponent<Player>().TeleportClientRpc(sp.position, sp.rotation);

            Debug.Log($"[EndGameTriggerZone] Client {clientId} → podium {i % teleportSet.teleportPoints.Count}.");
        }
    }
}