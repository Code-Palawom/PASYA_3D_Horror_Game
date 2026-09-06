using Unity.Netcode;
using UnityEngine;

public class EnemyPerception : MonoBehaviour {
    [SerializeField] private float viewRadius = 12f;
    [SerializeField] private float viewAngle = 90f;
    [SerializeField] private LayerMask playerMask, obstacleMask;

    [Header("Debug Visualization")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private float eyeHeight = 1.5f;

    private NetworkObject lastSeenPlayer; // cached for gizmo drawing only

    public bool CanSeePlayer(out NetworkObject player) {
        player = null;
        var hits = Physics.OverlapSphere(transform.position, viewRadius, playerMask);

        foreach (var hit in hits) {
            var candidate = hit.GetComponentInParent<NetworkObject>();
            if (candidate == null) continue;

            Vector3 origin = transform.position + Vector3.up * eyeHeight;
            Vector3 targetPos = candidate.transform.position + Vector3.up * eyeHeight;
            Vector3 dir = (targetPos - origin).normalized;

            if (Vector3.Angle(transform.forward, dir) > viewAngle / 2f) continue;

            float dist = Vector3.Distance(origin, targetPos);
            if (Physics.Raycast(origin, dir, dist, obstacleMask)) continue;

            player = candidate;
            lastSeenPlayer = candidate;
            return true;
        }

        lastSeenPlayer = null;
        return false;
    }

    private void OnDrawGizmosSelected() {
        if (!showGizmos) return;

        Vector3 origin = transform.position + Vector3.up * eyeHeight;

        // View radius
        Gizmos.color = new Color(1f, 1f, 0f, 0.15f);
        Gizmos.DrawSphere(transform.position, viewRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, viewRadius);

        // View cone edges
        Quaternion leftRot = Quaternion.AngleAxis(-viewAngle / 2f, Vector3.up);
        Quaternion rightRot = Quaternion.AngleAxis(viewAngle / 2f, Vector3.up);
        Vector3 leftDir = leftRot * transform.forward;
        Vector3 rightDir = rightRot * transform.forward;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(origin, origin + leftDir * viewRadius);
        Gizmos.DrawLine(origin, origin + rightDir * viewRadius);
        Gizmos.DrawLine(origin, origin + transform.forward * viewRadius);

        // Live check against all colliders on playerMask, color-coded
        var hits = Physics.OverlapSphere(transform.position, viewRadius, playerMask);
        foreach (var hit in hits) {
            var candidate = hit.GetComponentInParent<NetworkObject>();
            Vector3 targetPos = hit.transform.position + Vector3.up * eyeHeight;
            Vector3 dir = (targetPos - origin).normalized;
            float dist = Vector3.Distance(origin, targetPos);

            bool inAngle = Vector3.Angle(transform.forward, dir) <= viewAngle / 2f;
            bool blocked = Physics.Raycast(origin, dir, dist, obstacleMask);

            Gizmos.color =
                candidate == null ? Color.magenta :   // hit collider has no NetworkObject in parent chain — the bug case
                !inAngle ? Color.gray :                // outside view angle
                blocked ? Color.red :                   // los blocked
                Color.green;                            // fully visible

            Gizmos.DrawLine(origin, targetPos);
            Gizmos.DrawWireSphere(targetPos, 0.2f);
        }
    }
}