using System;
using UnityEngine;

public class CharacterAppearanceController : MonoBehaviour {
    [SerializeField] private Transform modelAnchor; // empty child transform the model spawns under
    [SerializeField] private Animator animator;      // gameplay Animator, shared AnimatorController across all skins

    private GameObject currentModel;
    public event Action<GameObject> OnModelSwapped;

    public void ApplySkin(CharacterSkinSO skin) {
        if (skin == null || skin.modelPrefab == null || modelAnchor == null) return;

        if (currentModel != null) {
            currentModel.transform.SetParent(null); // detach so it's no longer a modelAnchor sibling
            DestroyImmediate(currentModel);          // guarantees it's gone before Rebind runs
        }

        currentModel = Instantiate(skin.modelPrefab, modelAnchor);

        // point the gameplay Animator at the new model's rig root instead of using the imported one.
        var importedAnimator = currentModel.GetComponent<Animator>();
        if (animator != null && importedAnimator != null) {
            animator.avatar = importedAnimator.avatar;
            Destroy(importedAnimator); // avoid two Animators competing on the same hierarchy
            animator.Rebind();
            animator.Update(0f);
        }

        OnModelSwapped?.Invoke(currentModel);
    }
}