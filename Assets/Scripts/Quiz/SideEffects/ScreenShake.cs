using UnityEngine;

[CreateAssetMenu(menuName = "Quiz/SideEffects/ScreenShake")]
public class ScreenShake : QuizSideEffect {
    public float shakeIntensity = 1f;

    public override void Apply(GameObject player) {
        var impulse = player.GetComponentInChildren<Unity.Cinemachine.CinemachineImpulseSource>();
        impulse?.GenerateImpulse(shakeIntensity);
    }
}