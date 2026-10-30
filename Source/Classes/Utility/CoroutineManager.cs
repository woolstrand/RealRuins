using UnityEngine;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace RealRuins {
    public class CoroutineManager : MonoBehaviour {
        private static CoroutineManager _instance;

        private readonly ConcurrentQueue<Action> mainThreadActions = new ConcurrentQueue<Action>();

        // Must be first accessed on the main thread (Unity objects can't be created elsewhere).
        public static CoroutineManager Instance {
            get {
                if (_instance == null) {
                    var go = new GameObject("CoroutineManager");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<CoroutineManager>();
                }
                return _instance;
            }
        }

        public void RunCoroutine(IEnumerator coroutine) {
            StartCoroutine(coroutine);
        }

        public void RunAfter(Action action, float delaySeconds) {
            StartCoroutine(RunAfterCoroutine(action, delaySeconds));
        }

        // Safe to call from any thread: the action runs on the main thread during the next Update.
        public void RunOnMainThread(Action action) {
            mainThreadActions.Enqueue(action);
        }

        private IEnumerator RunAfterCoroutine(Action action, float delaySeconds) {
            yield return new WaitForSecondsRealtime(delaySeconds);
            action();
        }

        private void Update() {
            while (mainThreadActions.TryDequeue(out Action action)) {
                try {
                    action();
                } catch (Exception e) {
                    Debug.Error("Main thread action failed: {0}", e);
                }
            }
        }
    }
}