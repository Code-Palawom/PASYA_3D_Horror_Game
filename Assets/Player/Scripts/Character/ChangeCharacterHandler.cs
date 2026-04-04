using UnityEngine;

public class ChangeCharacterHandler : MonoBehaviour {
    [Header("Renderers")]
    public SkinnedMeshRenderer hairRenderer;
    public SkinnedMeshRenderer headRenderer;
    public SkinnedMeshRenderer bodyRenderer;
    public SkinnedMeshRenderer armsRenderer;
    public SkinnedMeshRenderer rightHandCosmetic;
    public SkinnedMeshRenderer legsRenderer;
    public SkinnedMeshRenderer pantsRenderer;
    public SkinnedMeshRenderer shoesRenderer;

    private MMCharacterAnimation characterAnimation;

    //private Animator anim;

    void Awake() {
        characterAnimation = GetComponent<MMCharacterAnimation>();
        //anim = GetComponent<Animator>();
    }

    public void ApplyCharacter(CharacterData currentData, bool applyAnimation) {

        if(currentData == null) return;
        UpdatePart(rightHandCosmetic, currentData.rightHandCosmeticMesh, currentData.rightHandCosmeticMaterial);

        UpdatePart(hairRenderer, currentData.hairMesh, currentData.hairMaterial);
        UpdatePart(headRenderer, currentData.headMesh, currentData.headMaterial);
        UpdatePart(bodyRenderer, currentData.bodyMesh, currentData.bodyMaterial);
        UpdatePart(pantsRenderer, currentData.pantsMesh, currentData.pantsMaterial);
        UpdatePart(shoesRenderer, currentData.shoesMesh, currentData.shoesMaterial);

        UpdateSkin(legsRenderer, currentData.legsMesh, currentData.skinMaterial);
        UpdateSkin(armsRenderer, currentData.armsMesh, currentData.skinMaterial);

        if(applyAnimation) characterAnimation.SetAnimationState("outfitchange");
        //if (anim) anim.runtimeAnimatorController = currentData.animatorController;
    }

    private void UpdateSkin(SkinnedMeshRenderer renderer, Mesh mesh, Material material) {
        if(renderer == null) return;

        if(mesh == null){
            renderer.gameObject.SetActive(false);
            return;
        }

        renderer.gameObject.SetActive(true);
        renderer.sharedMesh = mesh;
        renderer.sharedMaterial = material;
    }

    private void UpdatePart(SkinnedMeshRenderer renderer, Mesh mesh, Material[] material) {
        if(renderer == null) return;

        if(mesh == null){
            renderer.gameObject.SetActive(false);
            return;
        }

        renderer.gameObject.SetActive(true);
        renderer.sharedMesh = mesh;
        renderer.sharedMaterials = material;
    }
}
