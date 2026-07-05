using System;
using System.Collections.Generic;
using UnityEngine;

// Queues actions from background threads (e.g. Firebase callbacks) onto Unity's main thread.
// Add this to your Bootstrap/persistent GameObject.
public class MainThreadDispatcher : MonoBehaviour {
    public static MainThreadDispatcher Instance { get; private set; }

    private readonly Queue<Action> _queue = new Queue<Action>();

    void Awake() {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Update() {
        lock (_queue) {
            while (_queue.Count > 0)
                _queue.Dequeue()?.Invoke();
        }
    }

    public void Enqueue(Action action) {
        if (action == null) return;
        lock (_queue) _queue.Enqueue(action);
    }
}