using UnityEngine;

public class MobileControlsRoot : MonoBehaviour
{
    public static MobileControlsRoot Instance { get; private set; }

    [SerializeField] private bool showOnlyOnMobile = true;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        ApplyPlatformVisibility();
    }

    void ApplyPlatformVisibility()
    {
        bool shouldShow = true;

        if (showOnlyOnMobile)
            shouldShow = Application.isMobilePlatform;

        gameObject.SetActive(shouldShow);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}