using UnityEngine;

public class GameSession : MonoBehaviour
{
    public static GameSession Instance { get; private set; }

    [Header("Players")]
    [Range(1, 4)]
    [SerializeField] private int activePlayerCount = 1;

    public int ActivePlayerCount => activePlayerCount;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        Clamp();
    }

    public void SetActivePlayerCount(int count)
    {
        activePlayerCount = Mathf.Clamp(count, 1, 4);
    }

    void Clamp()
    {
        activePlayerCount = Mathf.Clamp(activePlayerCount, 1, 4);
    }
}
