using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace RealRuins
{
    public class CoroutineManager : MonoBehaviour
    {
        private static CoroutineManager _instance;

        public static CoroutineManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("CoroutineManager");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<CoroutineManager>();
                }
                return _instance;
            }
        }

        public void RunCoroutine(IEnumerator coroutine)
        {
            StartCoroutine(coroutine);
        }
    }
}