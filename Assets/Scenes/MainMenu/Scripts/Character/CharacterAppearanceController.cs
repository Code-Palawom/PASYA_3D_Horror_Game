using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CharacterAppearanceController : MonoBehaviour {
    [SerializeField] private Transform modelAnchor; // empty child transform the model spawns under
    [SerializeField] private Animator animator;      // gameplay Animator, shared AnimatorController across all skins

    private GameObject currentModel;
    public event Action<GameObject> OnModelSwapped;

    private CharacterSkinSO currentSkin;
    private bool isFirstLaunched = true;

    public void ApplySkin(CharacterSkinSO skin) {
        if (skin == null || skin.modelPrefab == null || modelAnchor == null) return;

        // Capture current animation state per layer before tearing anything down,
        // so the swap doesn't pop back to frame 0 of the default state.
        int layerCount = animator != null ? animator.layerCount : 0;
        var stateHashes = new int[layerCount];
        var normalizedTimes = new float[layerCount];
        for (int i = 0; i < layerCount; i++) {
            var info = animator.GetCurrentAnimatorStateInfo(i);
            stateHashes[i] = info.fullPathHash;
            normalizedTimes[i] = info.normalizedTime;
        }

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

            // Resume each layer at the same clip/time it was at before the swap.
            for (int i = 0; i < layerCount; i++)
                animator.Play(stateHashes[i], i, normalizedTimes[i]);
            animator.Update(0f);
        }

        if (SceneManager.GetActiveScene().name == "MainMenu") {
            if (!isFirstLaunched) animator.SetBool(Animator.StringToHash("IsStanding"), true);
            if (!isFirstLaunched && currentSkin != skin) {
                animator.SetBool(Animator.StringToHash("IsStanding"), false);
                animator.SetTrigger(Animator.StringToHash("OutfitChange"));
            }

            currentSkin = skin;
            if (isFirstLaunched) isFirstLaunched = false;
        }

        OnModelSwapped?.Invoke(currentModel);
    }
}