using Firebase;
using Firebase.Extensions;
using Firebase.Firestore;
using System;
using UnityEngine;

public class ConfirmFirebaseServices : MonoBehaviour {
    //private void Start() {
    //    Firebase.FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task => {
    //        var dependencyStatus = task.Result;
    //        if (dependencyStatus == Firebase.DependencyStatus.Available) {
    //            // Create and hold a reference to your FirebaseApp,
    //            // where app is a Firebase.FirebaseApp property of your application class.
    //            //app = Firebase.FirebaseApp.DefaultInstance;
    //            Debug.Log("Firebase dependencies are available.");

    //            // Set a flag here to indicate whether Firebase is ready to use by your app.
    //        } else {
    //            UnityEngine.Debug.LogError(System.String.Format(
    //              "Could not resolve all Firebase dependencies: {0}", dependencyStatus));
    //            // Firebase Unity SDK is not safe to use here.
    //        }
    //    });
    //}
    // FirebaseBootstrapper.cs — the ONLY place that calls CheckAndFixDependenciesAsync
    public static ConfirmFirebaseServices Instance { get; private set; }
    public bool IsReady { get; private set; }
    public event Action OnFirebaseReady;

    private void Awake() {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private async void Start() {
        var status = await FirebaseApp.CheckAndFixDependenciesAsync();
        if (status == DependencyStatus.Available) {
            IsReady = true;
            Debug.Log("[ConfirmFirebaseServices] Firebase ready.");
            OnFirebaseReady?.Invoke();
            var db = FirebaseFirestore.DefaultInstance;
            QuizFetcher.Instance.Init(db);
        } else {
            Debug.LogError($"[ConfirmFirebaseServices] Dependency error: {status}");
        }
    }
}
