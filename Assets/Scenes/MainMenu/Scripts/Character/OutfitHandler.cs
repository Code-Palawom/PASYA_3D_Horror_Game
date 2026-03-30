using UnityEngine;

public class OutfitHandler : MonoBehaviour {
    [SerializeField] private ChangeOutfit changeOutfit;

    [SerializeField] private Material material;
    [SerializeField] private Outfits outfits;

    public void ChangeOutfit() {
        int index = GetOutfitIndex(outfits);

        if(index == -1){
            Debug.Log("Nope");
        }else{
            changeOutfit.SwapMaterial(index, material);
        }
    }

    private int GetOutfitIndex(Outfits outfits) {
        return outfits switch {
            Outfits.TShirt => 0,
            Outfits.Head => 3,
            _ => -1,
        };
    }

    public enum Outfits {
        TShirt = 1,
        Head = 3
    }
}
