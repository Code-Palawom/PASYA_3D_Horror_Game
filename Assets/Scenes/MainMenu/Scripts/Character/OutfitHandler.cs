using UnityEngine;

public class OutfitHandler : MonoBehaviour {
    [SerializeField] private ChangeOutfit changeOutfit;

    [SerializeField] private string type;
    [SerializeField] private Material material;

    public void ChangeOutfit() {
        changeOutfit.SwapMaterial(type, material);
    }
}
