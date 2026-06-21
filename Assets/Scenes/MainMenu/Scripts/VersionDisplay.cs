using UnityEngine;
using TMPro;

public class VersionDisplay : MonoBehaviour {
    [SerializeField] private TextMeshProUGUI versionText;

    void Start() {
        versionText.text = $"{Application.version}-alpha";
    }
}