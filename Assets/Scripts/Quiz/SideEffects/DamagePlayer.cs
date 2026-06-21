using UnityEngine;

[CreateAssetMenu(menuName = "Quiz/SideEffects/DamagePlayer")]
public class DamagePlayer : QuizSideEffect {
    public int damageAmount = 10;

    public override void Apply(GameObject player) {
        if (player.TryGetComponent<PlayerHealth>(out var health))
            health.TakeDamage(damageAmount);
    }
}