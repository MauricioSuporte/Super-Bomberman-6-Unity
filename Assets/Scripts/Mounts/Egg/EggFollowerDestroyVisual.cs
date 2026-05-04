using System.Collections;
using UnityEngine;

public sealed class EggFollowerDestroyVisual : MonoBehaviour
{
    public AnimatedSpriteRenderer destroyRenderer;
    public AnimatedSpriteRenderer explosionDestroyRenderer;

    public void PlayDestroy()
    {
        PlayDestroyInternal(destroyRenderer);
    }

    public void PlayExplosionDestroy()
    {
        if (explosionDestroyRenderer != null && destroyRenderer != null)
        {
            StartCoroutine(PlayExplosionThenDestroyRoutine());
            return;
        }

        PlayDestroyInternal(explosionDestroyRenderer != null ? explosionDestroyRenderer : destroyRenderer);
    }

    public float GetExplosionThenDestroyDuration()
    {
        if (explosionDestroyRenderer == null || destroyRenderer == null)
            return GetDestroyAnimationDuration(explosionDestroyRenderer != null ? explosionDestroyRenderer : destroyRenderer);

        return GetDestroyAnimationDuration(explosionDestroyRenderer) + GetDestroyAnimationDuration(destroyRenderer);
    }

    IEnumerator PlayExplosionThenDestroyRoutine()
    {
        PlayDestroyInternal(explosionDestroyRenderer);

        float explosionDuration = GetDestroyAnimationDuration(explosionDestroyRenderer);
        if (explosionDuration > 0f)
            yield return new WaitForSeconds(explosionDuration);

        PlayDestroyInternal(destroyRenderer);
    }

    void PlayDestroyInternal(AnimatedSpriteRenderer rendererToPlay)
    {
        var allAnim = GetComponentsInChildren<AnimatedSpriteRenderer>(true);

        for (int i = 0; i < allAnim.Length; i++)
        {
            var a = allAnim[i];
            if (a == null)
                continue;

            bool keep = rendererToPlay != null && a == rendererToPlay;

            a.enabled = keep;

            if (a.TryGetComponent<SpriteRenderer>(out var sr) && sr != null)
                sr.enabled = keep;

            var childSrs = a.GetComponentsInChildren<SpriteRenderer>(true);
            for (int s = 0; s < childSrs.Length; s++)
                if (childSrs[s] != null)
                    childSrs[s].enabled = keep;
        }

        var dv = GetComponentInChildren<EggFollowerDirectionalVisual>(true);
        if (dv != null)
            dv.enabled = false;

        if (rendererToPlay == null)
            return;

        if (!rendererToPlay.gameObject.activeInHierarchy)
            rendererToPlay.gameObject.SetActive(true);

        rendererToPlay.enabled = true;

        if (rendererToPlay.TryGetComponent<SpriteRenderer>(out var destroySr) && destroySr != null)
            destroySr.enabled = true;

        rendererToPlay.idle = false;
        rendererToPlay.loop = false;
        rendererToPlay.pingPong = false;
        rendererToPlay.CurrentFrame = 0;
        rendererToPlay.RefreshFrame();
    }

    static float GetDestroyAnimationDuration(AnimatedSpriteRenderer renderer)
    {
        if (renderer == null)
            return 0f;

        if (renderer.useSequenceDuration)
            return Mathf.Max(0.0001f, renderer.sequenceDuration);

        int frameCount = renderer.animationSprite != null ? renderer.animationSprite.Length : 0;
        return Mathf.Max(0.0001f, renderer.animationTime) * Mathf.Max(1, frameCount);
    }
}
