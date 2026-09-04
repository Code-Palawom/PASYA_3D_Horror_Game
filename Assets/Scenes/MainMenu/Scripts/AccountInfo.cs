using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Minimal UI hookup for the Sign In button.
// Attach to a Canvas GameObject. Assign the button and label in the Inspector.
public class AccountInfo : MonoBehaviour {
    [SerializeField] private TMP_Text userNameLabel;
    [SerializeField] private TMP_Text coins;

    [Header("Multiplayer Button")]
    [SerializeField] private GameObject hostModeWrapper;

    void Start() {
        AuthManager.Instance.OnPlayerStatsLoaded += OnPlayerStatsLoaded;

        // Reflect whatever state auth is already in (e.g. cached session)
        OnPlayerStatsLoaded(AuthManager.Instance.CurrentProfile);
    }

    void OnDestroy() {
        if (AuthManager.Instance != null)
            AuthManager.Instance.OnPlayerStatsLoaded -= OnPlayerStatsLoaded;
    }

    private void OnPlayerStatsLoaded(PlayerProfile user) {
        bool signedIn = AuthManager.Instance.IsSignedIn;

        userNameLabel.text = signedIn ? user.DisplayName : "Playing as guest";

        hostModeWrapper.SetActive(signedIn);

        if (AuthManager.Instance.CurrentProfile != null) UpdateUserStatsUI(AuthManager.Instance.CurrentProfile);
        AuthManager.Instance.OnPlayerStatsLoaded += UpdateUserStatsUI;
    }

    private void UpdateUserStatsUI(PlayerProfile profile) {
        coins.text = profile.Coins.ToString();
    }
}