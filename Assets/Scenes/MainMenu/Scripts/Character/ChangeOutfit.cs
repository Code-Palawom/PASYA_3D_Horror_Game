using UnityEngine;

public class ChangeOutfit : MonoBehaviour {
    private new Renderer renderer;

    private void Start() {
        renderer = GetComponent<Renderer>();
    }

    public void SwapMaterial(string type, Material material) {
        Material[] currentMaterials = renderer.sharedMaterials;

        currentMaterials[currentMaterials.Length - 1] = material;
        renderer.sharedMaterials = currentMaterials;
        //if(index <  currentMaterials.Length) {
        //    currentMaterials[index] = material;
        //    skinnedMeshRenderer.materials = currentMaterials;
        //}
    }
}
