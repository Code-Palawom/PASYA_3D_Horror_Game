using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[CreateAssetMenu(menuName = "Quiz/SideEffects/BlurVision")]
public class BlurVision : QuizSideEffect {
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