using System.Collections;
using UnityEngine;

public sealed class FuramaFlameVisual : MonoBehaviour
{
    [SerializeField] AnimatedSpriteRenderer flameAnimation;
    [SerializeField] Rigidbody2D flameBody;

    public void Play(
        Vector2 origin,
        Vector2 direction,
        float startTileDistance,
        float endTileDistance,
        float tileSize,
        float durationSeconds)
    {
        direction = ToCardinal(direction);
        float safeTileSize = Mathf.Max(0.01f, tileSize);
        Vector2 start = origin + direction * (Mathf.Max(0.01f, startTileDistance) * safeTileSize);
        Vector2 end = origin + direction * (Mathf.Max(startTileDistance, endTileDistance) * safeTileSize);

        transform.position = start;
        if (flameBody != null)
            flameBody.position = start;

        transform.rotation = Quaternion.FromToRotation(Vector3.up, direction);
        StartCoroutine(PlayRoutine(start, end, Mathf.Max(0.01f, durationSeconds)));
    }

    IEnumerator PlayRoutine(Vector2 start, Vector2 end, float durationSeconds)
    {
        if (flameAnimation != null)
        {
            flameAnimation.enabled = true;
            flameAnimation.idle = false;
            flameAnimation.loop = false;
            flameAnimation.useSequenceDuration = true;
            flameAnimation.sequenceDuration = durationSeconds;
            flameAnimation.RestartAnimation();
        }

        float elapsed = 0f;
        while (elapsed < durationSeconds)
        {
            elapsed += Time.deltaTime;
            Vector2 position = Vector2.Lerp(start, end, Mathf.Clamp01(elapsed / durationSeconds));
            if (flameBody != null)
                flameBody.MovePosition(position);
            else
                transform.position = position;

            yield return new WaitForFixedUpdate();
        }

        Destroy(gameObject);
    }

    static Vector2 ToCardinal(Vector2 direction)
    {
        if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y))
            return direction.x >= 0f ? Vector2.right : Vector2.left;

        return direction.y >= 0f ? Vector2.up : Vector2.down;
    }
}
