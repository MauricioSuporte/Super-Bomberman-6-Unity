using UnityEngine;

public sealed class GeyserLauncherBootstrap : MonoBehaviour
{
    [SerializeField] private Sprite geyserSprite;
    [SerializeField] private AudioClip geyserSfx;

    private void Awake()
    {
        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer == null)
            renderer = gameObject.AddComponent<SpriteRenderer>();

        renderer.sprite = geyserSprite;

        BoxCollider2D trigger = GetComponent<BoxCollider2D>();
        if (trigger == null)
            trigger = gameObject.AddComponent<BoxCollider2D>();

        trigger.isTrigger = true;
        trigger.size = Vector2.one * 0.5f;

        AnimatedSpriteRenderer animation = GetComponent<AnimatedSpriteRenderer>();
        if (animation == null)
            animation = gameObject.AddComponent<AnimatedSpriteRenderer>();

        animation.idleSprite = geyserSprite;
        animation.animationSprite = new[] { geyserSprite };
        animation.idle = true;
        animation.RefreshFrame();

        if (GetComponent<GeyserVisualFlipper>() == null)
            gameObject.AddComponent<GeyserVisualFlipper>();

        SpringLauncher launcher = GetComponent<SpringLauncher>();
        if (launcher == null)
            launcher = gameObject.AddComponent<SpringLauncher>();

        launcher.Configure(animation, geyserSfx, launchDistanceTiles: 4);
    }
}
