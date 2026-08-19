using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class GeyserVisualFlipper : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float flipIntervalSeconds = 0.12f;

    private SpriteRenderer spriteRenderer;
    private float nextFlipTime;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        nextFlipTime = Time.time + flipIntervalSeconds;
    }

    private void Update()
    {
        if (spriteRenderer == null || Time.time < nextFlipTime)
            return;

        spriteRenderer.flipX = !spriteRenderer.flipX;
        nextFlipTime = Time.time + flipIntervalSeconds;
    }
}
