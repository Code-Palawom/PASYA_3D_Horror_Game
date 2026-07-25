using Unity.Netcode;
using UnityEngine;

public class EnemyPerception : MonoBehaviour {
    [SerializeField] private float viewRadius = 12f;
    [SerializeField] private float viewAngle = 90f;
    [SerializeField] private LayerMask playerMask, obstacleMask;

    public bool CanSeePlayer(out NetworkObject player) {
        player = null;
        var hits = Physics.OverlapSphere(transform.position, viewRadius, playerMask);
        foreach (var hit in hits) {
            Vector3 dir = (hit.transform.position - transform.position).normalized;
            if (Vector3.Angle(transform.forward, dir) > viewAngle / 2f) continue;

            float dist = Vector3.Distance(transform.position, hit.transform.position);
            if (Physics.Raycast(transform.position, dir, dist, obstacleMask)) continue; // blocked

            player = hit.GetComponent<NetworkObject>();
            return true;
        }
        return false;
    }
}