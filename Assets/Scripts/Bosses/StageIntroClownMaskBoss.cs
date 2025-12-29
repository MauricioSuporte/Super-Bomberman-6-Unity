using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class StageIntroClownMaskBoss : MonoBehaviour
{
    [Header("Spotlight (Boss Intro)")]
    public Image spotlightImage;

    [Header("Spotlight Offset")]
    public float spotlightYOffsetWorld = -10.8f;

    [Header("Spotlight Ellipse")]
    public float spotlightEllipseX = 0.3f;
    public float spotlightEllipseY = 0.3f;

    [Header("Spotlight Fade")]
    public float spotlightFadeInDuration = 0.6f;

    Material spotlightMatInstance;

    void Awake()
    {
        if (spotlightImage != null && spotlightImage.material != null)
        {
            spotlightMatInstance = Instantiate(spotlightImage.material);
            spotlightImage.material = spotlightMatInstance;
            spotlightMatInstance.SetColor("_Color", new Color(0f, 0f, 0f, 0f));
            spotlightImage.gameObject.SetActive(false);
        }
        else
        {
            if (spotlightImage != null)
                spotlightImage.gameObject.SetActive(false);
        }
    }

    public void SetFullDarkness(float alpha)
    {
        if (spotlightImage == null || spotlightMatInstance == null) return;

        spotlightMatInstance.SetFloat("_EllipseX", Mathf.Max(spotlightEllipseX, 1e-5f));
        spotlightMatInstance.SetFloat("_EllipseY", Mathf.Max(spotlightEllipseY, 1e-5f));

        spotlightMatInstance.SetColor("_Color", new Color(0f, 0f, 0f, Mathf.Clamp01(alpha)));
        spotlightMatInstance.SetVector("_Center", new Vector4(-10f, -10f, 0f, 0f));
        spotlightMatInstance.SetFloat("_Radius", 0.001f);
        spotlightMatInstance.SetFloat("_Softness", 0.001f);

        spotlightImage.gameObject.SetActive(true);
    }

    public void SetSpotlightWorld(Vector3 worldCenter, float radiusWorld, float darknessAlpha, float softnessWorld)
    {
        if (spotlightImage == null || spotlightMatInstance == null) return;

        var cam = Camera.main;
        if (cam == null) return;

        worldCenter.y += spotlightYOffsetWorld;

        Vector3 vp = cam.WorldToViewportPoint(worldCenter);

        float radiusVp = WorldRadiusToViewportRadius(cam, worldCenter, Mathf.Max(0.01f, radiusWorld));
        float softVp = WorldRadiusToViewportRadius(cam, worldCenter, Mathf.Max(0.001f, softnessWorld));

        softVp = Mathf.Min(softVp, radiusVp * 0.25f);

        spotlightMatInstance.SetFloat("_EllipseX", Mathf.Max(spotlightEllipseX, 1e-5f));
        spotlightMatInstance.SetFloat("_EllipseY", Mathf.Max(spotlightEllipseY, 1e-5f));

        spotlightMatInstance.SetColor("_Color", new Color(0f, 0f, 0f, Mathf.Clamp01(darknessAlpha)));
        spotlightMatInstance.SetVector("_Center", new Vector4(vp.x, vp.y, 0f, 0f));
        spotlightMatInstance.SetFloat("_Radius", Mathf.Clamp(radiusVp, 0.001f, 2f));
        spotlightMatInstance.SetFloat("_Softness", Mathf.Clamp(softVp, 0.001f, 2f));

        spotlightImage.gameObject.SetActive(true);
    }

    public void DisableSpotlight()
    {
        if (spotlightImage != null)
            spotlightImage.gameObject.SetActive(false);
    }

    float WorldRadiusToViewportRadius(Camera cam, Vector3 worldCenter, float radiusWorld)
    {
        Vector3 a = worldCenter;
        Vector3 b = worldCenter + Vector3.right * radiusWorld;

        Vector3 av = cam.WorldToViewportPoint(a);
        Vector3 bv = cam.WorldToViewportPoint(b);

        return Mathf.Abs(bv.x - av.x);
    }

    public IEnumerator FadeToFullDarknessAndWait(float targetAlpha, float duration)
    {
        if (spotlightImage == null || spotlightMatInstance == null)
            yield break;

        float endA = Mathf.Clamp01(targetAlpha);
        float d = Mathf.Max(0.001f, duration);

        spotlightMatInstance.SetFloat("_EllipseX", Mathf.Max(spotlightEllipseX, 1e-5f));
        spotlightMatInstance.SetFloat("_EllipseY", Mathf.Max(spotlightEllipseY, 1e-5f));

        spotlightMatInstance.SetVector("_Center", new Vector4(-10f, -10f, 0f, 0f));
        spotlightMatInstance.SetFloat("_Radius", 0.001f);
        spotlightMatInstance.SetFloat("_Softness", 0.001f);

        spotlightMatInstance.SetColor("_Color", new Color(0f, 0f, 0f, 0f));
        spotlightImage.gameObject.SetActive(true);

        float t = 0f;

        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Lerp(0f, endA, Mathf.Clamp01(t / d));
            spotlightMatInstance.SetColor("_Color", new Color(0f, 0f, 0f, a));
            yield return null;
        }

        spotlightMatInstance.SetColor("_Color", new Color(0f, 0f, 0f, endA));
    }

    public IEnumerator FadeSpotlightAlphaAndWait(float targetAlpha, float duration)
    {
        if (spotlightImage == null || spotlightMatInstance == null)
            yield break;

        float endA = Mathf.Clamp01(targetAlpha);
        float d = Mathf.Max(0.001f, duration);

        spotlightImage.gameObject.SetActive(true);

        float startA = spotlightMatInstance.GetColor("_Color").a;
        float t = 0f;

        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Lerp(startA, endA, Mathf.Clamp01(t / d));
            spotlightMatInstance.SetColor("_Color", new Color(0f, 0f, 0f, a));
            yield return null;
        }

        spotlightMatInstance.SetColor("_Color", new Color(0f, 0f, 0f, endA));
    }
}
