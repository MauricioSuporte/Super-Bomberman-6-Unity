using UnityEngine;

public sealed class EggFollowerDestroyVisual : MonoBehaviour
{
    public AnimatedSpriteRenderer destroyRenderer;

    public void PlayDestroy()
    {
        var allAnim = GetComponentsInChildren<AnimatedSpriteRenderer>(true);

        for (int i = 0; i < allAnim.Length; i++)
        {
            var a = allAnim[i];
            if (a == null) continue;

            bool keep = (destroyRenderer != null && a == destroyRenderer);

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

        if (destroyRenderer == null)
            return;

        if (!destroyRenderer.gameObject.activeInHierarchy)
            destroyRenderer.gameObject.SetActive(true);

        destroyRenderer.enabled = true;

        if (destroyRenderer.TryGetComponent<SpriteRenderer>(out var destroySr) && destroySr != null)
            destroySr.enabled = true;

        destroyRenderer.idle = false;
        destroyRenderer.loop = false;
        destroyRenderer.pingPong = false;
        destroyRenderer.CurrentFrame = 0;
        destroyRenderer.RefreshFrame();
    }
}
