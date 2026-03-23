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
        PlayDestroyInternal(explosionDestroyRenderer != null ? explosionDestroyRenderer : destroyRenderer);
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
}