using UnityEngine;
using UnityEngine.U2D;

/// <summary>
/// Resolves the final on-screen rectangle of Unity's Pixel Perfect Camera.
/// With Upscale Render Texture enabled, PixelPerfectCamera restores
/// Camera.pixelRect after rendering, so UI updated in LateUpdate must derive
/// the final blit rectangle from the component settings instead.
/// </summary>
public static class PixelPerfectViewport
{
    public static Rect ResolveFinalPixelRect(Camera camera)
    {
        if (camera == null)
            return Rect.zero;

        PixelPerfectCamera pixelPerfectCamera = camera.GetComponent<PixelPerfectCamera>();
        if (pixelPerfectCamera == null)
            return camera.pixelRect;

        int screenWidth = Mathf.Max(1, Screen.width);
        int screenHeight = Mathf.Max(1, Screen.height);
        int referenceWidth = Mathf.Max(1, pixelPerfectCamera.refResolutionX);
        int referenceHeight = Mathf.Max(1, pixelPerfectCamera.refResolutionY);
        int zoom = Mathf.Max(1, Mathf.Min(screenWidth / referenceWidth, screenHeight / referenceHeight));

        bool cropX = pixelPerfectCamera.cropFrameX;
        bool cropY = pixelPerfectCamera.cropFrameY;
        bool cropEitherAxis = cropX || cropY;
        bool cropBothAxes = cropX && cropY;

        if (cropBothAxes && pixelPerfectCamera.stretchFill)
            return FitCentered(screenWidth, screenHeight, (float)referenceWidth / referenceHeight);

        if (!cropEitherAxis && (!pixelPerfectCamera.upscaleRT || zoom <= 1))
            return camera.pixelRect;

        int renderWidth;
        int renderHeight;

        if (pixelPerfectCamera.upscaleRT)
        {
            if (cropBothAxes)
            {
                renderWidth = referenceWidth;
                renderHeight = referenceHeight;
            }
            else if (cropY)
            {
                renderWidth = MakeEven(screenWidth / zoom);
                renderHeight = referenceHeight;
            }
            else if (cropX)
            {
                renderWidth = referenceWidth;
                renderHeight = MakeEven(screenHeight / zoom);
            }
            else
            {
                renderWidth = MakeEven(screenWidth / zoom);
                renderHeight = MakeEven(screenHeight / zoom);
            }
        }
        else if (cropBothAxes)
        {
            renderWidth = referenceWidth;
            renderHeight = referenceHeight;
        }
        else if (cropY)
        {
            return Centered(screenWidth, zoom * referenceHeight, screenWidth, screenHeight);
        }
        else
        {
            return Centered(zoom * referenceWidth, screenHeight, screenWidth, screenHeight);
        }

        return Centered(zoom * renderWidth, zoom * renderHeight, screenWidth, screenHeight);
    }

    static int MakeEven(int value) => Mathf.Max(2, value / 2 * 2);

    static Rect FitCentered(int screenWidth, int screenHeight, float aspect)
    {
        if ((float)screenWidth / screenHeight > aspect)
            return Centered(Mathf.RoundToInt(screenHeight * aspect), screenHeight, screenWidth, screenHeight);

        return Centered(screenWidth, Mathf.RoundToInt(screenWidth / aspect), screenWidth, screenHeight);
    }

    static Rect Centered(float width, float height, int screenWidth, int screenHeight)
    {
        return new Rect(
            (screenWidth - Mathf.FloorToInt(width)) / 2,
            (screenHeight - Mathf.FloorToInt(height)) / 2,
            width,
            height);
    }
}
