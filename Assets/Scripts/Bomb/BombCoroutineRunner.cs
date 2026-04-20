using System.Collections;
using UnityEngine;

public sealed class BombCoroutineRunner : MonoBehaviour
{
    private static BombCoroutineRunner instance;

    public static BombCoroutineRunner Instance
    {
        get
        {
            if (instance != null)
                return instance;

            var go = new GameObject("[BombCoroutineRunner]");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<BombCoroutineRunner>();
            return instance;
        }
    }

    public static Coroutine Run(IEnumerator routine)
    {
        if (routine == null)
            return null;

        return Instance.StartCoroutine(routine);
    }
}