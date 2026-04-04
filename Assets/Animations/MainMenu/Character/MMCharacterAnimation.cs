using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class MMCharacterAnimation : MonoBehaviour {
    [SerializeField] private Animator animator;
    
    private static readonly int isIdle = Animator.StringToHash("IsIdle");
    private static readonly int isStanding = Animator.StringToHash("IsStanding");
    private static readonly int isOutfitChange = Animator.StringToHash("OutfitChange");

    public void SetAnimationState(string setState) {
        if(setState != "outfitchange") animator.SetBool(isIdle, false);
        animator.SetBool(isStanding, false);

        if(setState == "idle") animator.SetBool(isIdle, true);
        if(setState == "standing") animator.SetBool(isStanding, true);
        if(setState == "outfitchange"){
            animator.SetTrigger(isOutfitChange);
        }
    }
}