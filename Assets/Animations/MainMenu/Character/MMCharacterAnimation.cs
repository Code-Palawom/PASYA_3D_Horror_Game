using System.Collections;
using UnityEngine;

public class MMCharacterAnimation : MonoBehaviour {
    [SerializeField] private Animator animator;
    
    private static readonly int isIdle = Animator.StringToHash("IsIdle");
    private static readonly int isStanding = Animator.StringToHash("IsStanding");
    private static readonly int outfitChange = Animator.StringToHash("OutfitChange");
    private static readonly int lookingOver = Animator.StringToHash("LookingOver");

    public void SetAnimationState(string setState) {
        if(setState != "lookingOver"){
            animator.SetBool(isIdle, false);
            animator.SetBool(isStanding, false);
        }

        if(setState == "idle") animator.SetBool(isIdle, true);
        if(setState == "standing") animator.SetBool(isStanding, true);
        if(setState == "outfitchange") animator.SetTrigger(outfitChange);
        if(setState == "lookingOver" && !animator.GetBool(isStanding)){
            animator.SetBool(isIdle, false);
            animator.SetTrigger(lookingOver);
        }
    }
}