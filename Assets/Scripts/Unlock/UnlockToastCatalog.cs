using UnityEngine;

public static class UnlockToastCatalog
{
    private const bool EnableSurgicalLogs = true;

    public readonly struct ToastInfo
    {
        public readonly string Title;
        public readonly string Subtitle;
        public readonly string IconResourcePath;

        public ToastInfo(string title, string subtitle, string iconResourcePath)
        {
            Title = title;
            Subtitle = subtitle;
            IconResourcePath = iconResourcePath;
        }
    }

    public static ToastInfo Get(BomberSkin skin)
    {
        switch (skin)
        {
            case BomberSkin.Gray:
                return new ToastInfo(
                    "Like a Yoshi",
                    "Ride in A Louie",
                    "UI/Unlocks/Icons/GrayBomber"
                );

            default:
                return new ToastInfo(
                    "New Character",
                    $"{skin} unlocked",
                    $"UI/Unlocks/Icons/{skin}"
                );
        }
    }

    public static Sprite LoadIcon(BomberSkin skin)
    {
        ToastInfo info = Get(skin);

        if (string.IsNullOrWhiteSpace(info.IconResourcePath))
        {
            SLog($"LoadIcon | skin={skin} | resource path is empty");
            return null;
        }

        Sprite sprite = Resources.Load<Sprite>(info.IconResourcePath);

        SLog($"LoadIcon | skin={skin} | path={info.IconResourcePath} | found={(sprite != null)}");

        return sprite;
    }

    private static void SLog(string message)
    {
        if (!EnableSurgicalLogs)
            return;

        Debug.Log($"[UnlockToastCatalog] {message}");
    }
}