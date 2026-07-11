using UnityEngine;
using TMPro;

public class VersionDisplay : MonoBehaviour {
    [SerializeField] private TextMeshProUGUI versionText;
    [SerializeField] private TextMeshProUGUI latestVersionText;

    void Start() {
        versionText.text = Application.version;
        latestVersionText.text = VersionChecker.Instance.LatestVersion;

        VersionChecker.Instance.OnCheckComplete += DisplayNewVersion;
    }

    private void DisplayNewVersion(bool success, string version) {
        VersionChecker.Instance.OnCheckComplete -= DisplayNewVersion;

        if (success) latestVersionText.text = version;
    }
}