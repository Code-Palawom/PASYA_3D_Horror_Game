using UnityEngine;

public class ChangeCharacterHandler : MonoBehaviour {
    [Header("Renderers")]
    public SkinnedMeshRenderer hairRenderer;
    public SkinnedMeshRenderer bodyRenderer;

    //private Animator anim;

    void Awake() {
        //anim = GetComponent<Animator>();
    }

    public void ApplyCharacter(CharacterData currentData) {
        if(currentData == null) return;

        UpdatePart(hairRenderer, currentData.hairMesh, currentData.hairMaterial);
        UpdatePart(bodyRenderer, currentData.bodyMesh, currentData.bodyMaterial);

        //if (anim) anim.runtimeAnimatorController = currentData.animatorController;
    }

    private void UpdatePart(SkinnedMeshRenderer renderer, Mesh mesh, Material material) {
        if(renderer == null) return;

        if(mesh == null){
            renderer.gameObject.SetActive(false);
            return;
        }

        renderer.gameObject.SetActive(true);
        renderer.sharedMesh = mesh;
        renderer.sharedMaterial = material;
    }
}
