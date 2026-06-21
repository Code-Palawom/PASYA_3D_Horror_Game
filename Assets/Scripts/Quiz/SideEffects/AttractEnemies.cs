using UnityEngine;

[CreateAssetMenu(menuName = "Quiz/SideEffects/AttractEnemies")]
public class AttractEnemies : QuizSideEffect {
    public float alertRadius = 15f;
    public LayerMask enemyLayer;

    public override void Apply(GameObject player) {
        Collider[] nearby = Physics.OverlapSphere(
            player.transform.position, alertRadius, enemyLayer);

        foreach (var col in nearby) {
            if (col.TryGetComponent<IEnemy>(out var enemy))
                enemy.AlertTo(player.transform.position);
        }
    }
    // No Remove needed — enemies stay alerted
}