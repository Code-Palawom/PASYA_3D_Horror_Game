using UnityEngine;

public class ChangeOutfit : MonoBehaviour {
    private SkinnedMeshRenderer skinnedMeshRenderer;

    private void Start() {
        skinnedMeshRenderer = GetComponent<SkinnedMeshRenderer>();
    }

    public void SwapMaterial(int index, Material material) {
        Material[] currentMaterials = skinnedMeshRenderer.materials;

        if(index <  currentMaterials.Length) {
            currentMaterials[index] = material;
            skinnedMeshRenderer.materials = currentMaterials;
        }
    }
}
