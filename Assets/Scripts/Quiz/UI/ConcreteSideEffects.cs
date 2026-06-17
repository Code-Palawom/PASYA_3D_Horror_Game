using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Blur Vision
[CreateAssetMenu(menuName = "Quiz/SideEffects/BlurVision")]
public class BlurVisionSideEffect : QuizSideEffect {
    [Range(0f, 10f)] public float blurIntensity = 5f;

    public override void Apply(GameObject player) {
        var volume = player.GetComponentInChildren<Volume>();
        if (volume == null) return;
        if (volume.profile.TryGet<DepthOfField>(out var dof)) {
            dof.active = true;
            dof.gaussianMaxRadius.value = blurIntensity;
        }
    }

    public override void Remove(GameObject player) {
        var volume = player.GetComponentInChildren<Volume>();
        if (volume == null) return;
        if (volume.profile.TryGet<DepthOfField>(out var dof))
            dof.active = false;
    }
}

// Attract Enemies
[CreateAssetMenu(menuName = "Quiz/SideEffects/AttractEnemies")]
public class AttractEnemiesSideEffect : QuizSideEffect {
    public float alertRadius = 15f;
    public LayerMask enemyLayer;

    public override void Apply(GameObject player) {
        Collider[] nearby = Physics.OverlapSphere(player.transform.position, alertRadius, enemyLayer);
        foreach (var col in nearby) {
            if (col.TryGetComponent<IEnemy>(out var enemy))
                enemy.AlertTo(player.transform.position);
        }
    }
    // No Remove needed — enemies stay alerted
}

// Damage Player
[CreateAssetMenu(menuName = "Quiz/SideEffects/DamagePlayer")]
public class DamagePlayerSideEffect : QuizSideEffect {
    public int damageAmount = 10;

    public override void Apply(GameObject player) {
        if (player.TryGetComponent<PlayerHealth>(out var health))
            health.TakeDamage(damageAmount);
    }
}

// Slow Player
[CreateAssetMenu(menuName = "Quiz/SideEffects/SlowPlayer")]
public class SlowPlayerSideEffect : QuizSideEffect {
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

// Screen Shake (Cinemachine Impulse)
[CreateAssetMenu(menuName = "Quiz/SideEffects/ScreenShake")]
public class ScreenShakeSideEffect : QuizSideEffect {
    public float shakeIntensity = 1f;

    public override void Apply(GameObject player) {
        var impulse = player.GetComponentInChildren<Unity.Cinemachine.CinemachineImpulseSource>();
        impulse?.GenerateImpulse(shakeIntensity);
    }
}