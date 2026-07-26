using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public enum EnemyState { Patrol, Investigate, Hunt, Search, Attack }

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyController : NetworkBehaviour {
    [Header("Refs")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private EnemyPerception perception;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    // Assigned at spawn time via SetPatrolPoints — prefabs can't reference
    // scene Transforms directly, so patrol data comes in as plain Vector3s
    // (see PatrolGroupData / ScenePatrolData).
    private List<Vector3> patrolPoints = new();

    [Header("Movement")]
    [SerializeField] private float huntSpeed = 5f;
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float investigateSpeed = 3f;

    [Header("Combat")]
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float attackStateTimeout = 5f; // safety net — see TickAttack

    [Header("Search (expanding radius)")]
    [SerializeField] private float searchInitialRadius = 4f;
    [SerializeField] private float searchRadiusGrowthPerCycle = 3f;
    [SerializeField] private float searchMaxRadius = 20f;
    [SerializeField] private float searchPointArriveDistance = 0.75f;
    [SerializeField] private float searchTimeout = 15f; // total seconds before giving up
    [SerializeField] private int searchNavSampleAttempts = 8;

    private NetworkVariable<EnemyState> currentState =
        new NetworkVariable<EnemyState>(EnemyState.Patrol,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    private int patrolIndex = -1;
    private Vector3 lastKnownPlayerPos;
    private NetworkObject targetPlayer;

    // hunted vision (red screen effect on whichever player is currently
    // being hunted/attacked — only ever sent to that one player's client)
    private NetworkObject huntedVisionTarget;
    private bool huntedVisionActive;

    // search state
    private Vector3 searchOrigin;
    private float currentSearchRadius;
    private float searchElapsed;

    // attack state safety net
    private float attackElapsed;

    private void Log(string msg) {
        if (debugLogs) Debug.Log($"[EnemyController:{name}] {msg}", this);
    }

    public override void OnNetworkSpawn() {
        if (!IsServer) { enabled = false; return; }

        if (NoiseManager.Singleton != null)
            NoiseManager.Singleton.RegisterEnemy(this);

        Log("Spawned (server). Entering Patrol.");
        SetState(EnemyState.Patrol);
    }

    public override void OnNetworkDespawn() {
        if (IsServer && NoiseManager.Singleton != null)
            NoiseManager.Singleton.UnregisterEnemy(this);

        if (IsServer && huntedVisionActive)
            SetHuntedVision(huntedVisionTarget, false);
    }

    public void SetPatrolPoints(List<Vector3> points) {
        patrolPoints = points ?? new List<Vector3>();
        patrolIndex = -1;
        Log($"Patrol points assigned: {patrolPoints.Count}");
    }

    private void Update() {
        if (!IsServer) return;

        switch (currentState.Value) {
            case EnemyState.Patrol: TickPatrol(); break;
            case EnemyState.Investigate: TickInvestigate(); break;
            case EnemyState.Hunt: TickHunt(); break;
            case EnemyState.Search: TickSearch(); break;
            case EnemyState.Attack: TickAttack(); break;
        }
    }

    private void SetState(EnemyState state) {
        if (currentState.Value != state)
            Log($"State: {currentState.Value} -> {state} (agent.isOnNavMesh={agent.isOnNavMesh}, isStopped={agent.isStopped})");

        currentState.Value = state;
        agent.speed = state switch {
            EnemyState.Hunt => huntSpeed,
            EnemyState.Investigate => investigateSpeed,
            _ => patrolSpeed
        };

        if (state == EnemyState.Hunt) OnEnterHuntClientRpc();
        if (state == EnemyState.Search) BeginSearch();
        if (state == EnemyState.Attack) attackElapsed = 0f;

        // Hunt and Attack both count as "actively threatening a specific
        // player" — vision stays on across that Hunt -> Attack handoff and
        // only clears once we drop to Investigate/Search/Patrol.
        bool shouldShowVision = state == EnemyState.Hunt || state == EnemyState.Attack;
        if (shouldShowVision != huntedVisionActive) {
            SetHuntedVision(shouldShowVision ? targetPlayer : huntedVisionTarget, shouldShowVision);
            huntedVisionActive = shouldShowVision;
        }
    }

    [ClientRpc]
    private void OnEnterHuntClientRpc() { /* jumpscare stinger, heartbeat sfx, etc */ }

    // Sends the hunted-vision toggle to exactly one client — the target
    // player's own OwnerClientId — via ClientRpcParams.Send.TargetClientIds.
    // No one else's screen ever receives this RPC.
    private void SetHuntedVision(NetworkObject target, bool hunted) {
        if (target == null) return;

        var vision = target.GetComponent<VisionEffectController>();
        if (vision == null) return;

        var rpcParams = new ClientRpcParams {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { target.OwnerClientId } }
        };

        vision.SetHuntedClientRpc(hunted, rpcParams);

        huntedVisionTarget = hunted ? target : null;
    }

    // ---------------- Patrol ----------------

    private void TickPatrol() {
        if (perception.CanSeePlayer(out var player)) {
            Log($"Saw player {player.OwnerClientId} during Patrol -> Hunt");
            targetPlayer = player;
            SetState(EnemyState.Hunt);
            return;
        }

        if (patrolPoints.Count == 0) return;

        if (!agent.hasPath || agent.remainingDistance < 0.5f) {
            patrolIndex = (patrolIndex + 1) % patrolPoints.Count;
            Log($"Patrol -> point {patrolIndex}: {patrolPoints[patrolIndex]}");
            agent.SetDestination(patrolPoints[patrolIndex]);
        }
    }

    // ---------------- Investigate (noise-driven) ----------------

    public void HearNoise(Vector3 position, float loudness) {
        if (currentState.Value == EnemyState.Hunt || currentState.Value == EnemyState.Attack)
            return;

        Log($"Heard noise at {position} (loudness={loudness}) -> Investigate");
        lastKnownPlayerPos = position;
        agent.SetDestination(position);
        SetState(EnemyState.Investigate);
    }

    private void TickInvestigate() {
        if (perception.CanSeePlayer(out var player)) {
            targetPlayer = player;
            Log("Spotted player during Investigate -> Hunt");
            SetState(EnemyState.Hunt);
            return;
        }

        if (!agent.hasPath || agent.remainingDistance < 0.5f) {
            Log("Reached noise source, nothing found -> Search");
            searchOrigin = lastKnownPlayerPos;
            SetState(EnemyState.Search);
        }
    }

    // ---------------- Hunt ----------------

    private void TickHunt() {
        if (targetPlayer == null) {
            Log("Hunt: targetPlayer is null -> Search");
            searchOrigin = transform.position;
            SetState(EnemyState.Search);
            return;
        }

        if (perception.CanSeePlayer(out var player) && player == targetPlayer) {
            lastKnownPlayerPos = targetPlayer.transform.position;
            agent.SetDestination(lastKnownPlayerPos);

            float dist = Vector3.Distance(transform.position, targetPlayer.transform.position);
            if (dist <= attackRange) {
                Log($"In attack range ({dist:F2} <= {attackRange}) -> Attack");
                SetState(EnemyState.Attack);
            }
        } else {
            Log("Lost sight of target during Hunt -> Search");
            searchOrigin = lastKnownPlayerPos;
            SetState(EnemyState.Search);
        }
    }

    // ---------------- Search (expanding radius) ----------------

    private void BeginSearch() {
        currentSearchRadius = searchInitialRadius;
        searchElapsed = 0f;
        targetPlayer = null;
        Log($"BeginSearch at origin {searchOrigin}, radius {currentSearchRadius}");
        PickNewSearchPoint();
    }

    private void TickSearch() {
        if (perception.CanSeePlayer(out var player)) {
            targetPlayer = player;
            Log("Spotted player during Search -> Hunt");
            SetState(EnemyState.Hunt);
            return;
        }

        searchElapsed += Time.deltaTime;
        if (searchElapsed >= searchTimeout) {
            Log("Search timed out -> Patrol");
            SetState(EnemyState.Patrol);
            return;
        }

        if (!agent.hasPath || agent.remainingDistance < searchPointArriveDistance) {
            currentSearchRadius = Mathf.Min(currentSearchRadius + searchRadiusGrowthPerCycle, searchMaxRadius);
            Log($"Search point reached/no path, expanding radius to {currentSearchRadius}");
            PickNewSearchPoint();
        }
    }

    private void PickNewSearchPoint() {
        for (int i = 0; i < searchNavSampleAttempts; i++) {
            Vector2 offset2D = Random.insideUnitCircle * currentSearchRadius;
            Vector3 candidate = searchOrigin + new Vector3(offset2D.x, 0f, offset2D.y);

            if (NavMesh.SamplePosition(candidate, out var hit, 2f, NavMesh.AllAreas)) {
                Log($"New search point: {hit.position} (attempt {i + 1})");
                agent.SetDestination(hit.position);
                return;
            }
        }
        // IMPORTANT: if every attempt fails (bad NavMesh coverage / origin off-mesh),
        // agent.hasPath stays false, so TickSearch will re-enter this method every
        // single frame — expanding the radius each frame and spamming this log.
        // That reads as "the enemy is frozen" even though the state machine is alive.
        Log($"FAILED to find a valid search point after {searchNavSampleAttempts} attempts " +
            $"(origin={searchOrigin}, radius={currentSearchRadius}). Enemy will appear stuck.");
    }

    // ---------------- Attack ----------------

    private void TickAttack() {
        // TODO: this is currently a stub — no damage is dealt and nothing
        // ever moves the enemy out of Attack, so once it enters this state
        // it will sit here permanently. That is very likely your "freeze
        // after Hunt" symptom.
        //
        // attackStateTimeout below is a temporary safety net so you can
        // confirm the diagnosis — remove it once real attack logic exists.
        attackElapsed += Time.deltaTime;
        if (attackElapsed >= attackStateTimeout) {
            Log($"TickAttack stub timed out after {attackStateTimeout}s with no real logic -> Search (safety net)");
            searchOrigin = transform.position;
            SetState(EnemyState.Search);
        }
    }
}