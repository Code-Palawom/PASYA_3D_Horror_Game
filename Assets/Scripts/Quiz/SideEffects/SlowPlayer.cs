using UnityEngine;

[CreateAssetMenu(menuName = "Quiz/SideEffects/SlowPlayer")]
public class SlowPlayer : QuizSideEffect {
    [Range(0f, 1f)] public float speedMultiplier = 0.4f;

    public override void Apply(GameObject player) {
        if (player.TryGetComponent<Player>(out var movement))
            movement.SetSpeedMultiplier(speedMultiplier);
    }

    public override void Remove(GameObject player) {
        if (player.TryGetComponent<Player>(out var movement))
            movement.SetSpeedMultiplier(1f);
    }
}