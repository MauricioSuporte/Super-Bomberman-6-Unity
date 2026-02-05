using System.Collections;
using UnityEngine;

public sealed class GlobalCoroutineRunner : MonoBehaviour
{
    private static GlobalCoroutineRunner _instance;

    public static Coroutine Run(IEnumerator routine)
    {
        if (routine == null)
            return null;

        if (_instance == null)
        {
            var go = new GameObject("GlobalCoroutineRunner");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<GlobalCoroutineRunner>();
        }

        return _instance.StartCoroutine(routine);
    }
}
