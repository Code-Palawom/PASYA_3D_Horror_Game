using UnityEngine;

public class ActiveMenuButtonState : MonoBehaviour {
    [SerializeField] private MenuHoverAnimation menuHoverAnimation0;
    [SerializeField] private MenuHoverAnimation menuHoverAnimation1;
    [SerializeField] private MenuHoverAnimation menuHoverAnimation2;
    [SerializeField] private MenuHoverAnimation menuHoverAnimation3;
    [SerializeField] private MenuHoverAnimation menuHoverAnimation4;

    private FogRed fogRed;

    private bool isFogRed = false;

    private void Awake() {
        fogRed = GetComponent<FogRed>();
    }

    public void SetActiveMenuButton(int activeMenu) {
        menuHoverAnimation0.SlideOut(activeMenu);
        menuHoverAnimation1.SlideOut(activeMenu);
        menuHoverAnimation2.SlideOut(activeMenu);
        menuHoverAnimation3.SlideOut(activeMenu);
        menuHoverAnimation4.SlideOut(activeMenu);

        if(activeMenu == 4 && !isFogRed){
            isFogRed = true;
            fogRed.ApplyRedFog();
        }else{
            if(isFogRed){
                isFogRed = false;
                fogRed.ResetFog();
            }
        }
    }
}
