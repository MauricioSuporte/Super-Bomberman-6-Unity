using UnityEngine;

/// <summary>
/// Emits the water ripple exactly once as Banbo enters the first frame of its
/// water-jump cycle.
/// </summary>
[DefaultExecutionOrder(100)]
public sealed class BanboWaterSubmersionFrameController : MonoBehaviour
{
    [SerializeField] private AnimatedSpriteRenderer downSprite;

    private bool wasOnFirstDownFrame;

    private void LateUpdate()
    {
        bool isOnFirstDownFrame = downSprite != null &&
                                  downSprite.isActiveAndEnabled &&
                                  downSprite.CurrentFrame == 0;

        if (isOnFirstDownFrame && !wasOnFirstDownFrame)
            SpawnWaterRipple();

        wasOnFirstDownFrame = isOnFirstDownFrame;
    }

    private void OnDisable()
    {
        wasOnFirstDownFrame = false;
    }

    private void SpawnWaterRipple()
    {
        int sortingLayerId = 0;
        int sortingOrder = 4;
        if (downSprite != null && downSprite.TryGetComponent(out SpriteRenderer spriteRenderer))
        {
            sortingLayerId = spriteRenderer.sortingLayerID;
            sortingOrder = spriteRenderer.sortingOrder - 1;
        }

        GameObject effectObject = new("BanboWaterRipple");
        effectObject.transform.position = transform.position;

        FrogWaterJumpEffect effect = effectObject.AddComponent<FrogWaterJumpEffect>();
        effect.Initialize(FrogWaterJumpEffect.EffectType.ExitRipple, sortingLayerId, sortingOrder);
    }
}
