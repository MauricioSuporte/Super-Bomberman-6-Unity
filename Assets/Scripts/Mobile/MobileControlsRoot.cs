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

    public void RefreshVisibilityFromSavedPreference()
    {
        ApplyPlatformVisibility();
    }

    public void SetTouchButtonsVisible(bool visible)
    {
        SaveSystem.SetMobileTouchButtonsVisible(visible);
        ApplyPlatformVisibility();
    }

    void ApplyPlatformVisibility()
    {
        bool shouldShow = true;

        if (showOnlyOnMobile)
            shouldShow = Application.isMobilePlatform;

        if (shouldShow)
            shouldShow = SaveSystem.GetMobileTouchButtonsVisible();

        gameObject.SetActive(shouldShow);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
