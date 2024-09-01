using UnityEngine;
using Verse;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace RealRuins
{
    public class StartApi
    {
        public static void CreateSceneObject()
        {
            LongEventHandler.ExecuteWhenFinished(delegate
            {
                if (GameObject.Find("RealRuinProxy") != null)
                {
                    Debug.Error("Another version of the library is already loaded. The HugsLib assembly should be loaded as a standalone mod.");
                }
                else
                {
                    GameObject gameObject = new GameObject("RealRuinProxy");
                    UnityEngine.Object.DontDestroyOnLoad(gameObject);
                    gameObject.AddComponent<CoroutineManager>();
                }
            });
        }

    }
}
