using UnityEngine;
using UnityEngine.U2D;

namespace StageAssets
{
    /// <summary>
    /// Screen-anchored pixel snow for Stage 3-5. The flakes follow the active
    /// gameplay camera, so they stay inside its pixel-perfect safe frame.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Stage35SnowfallEffect : MonoBehaviour
    {
        private const int FlakeCount = 120;
        private const int GridColumns = 15;
        private const int GridRows = 8;
        private const float DefaultPixelsPerUnit = 16f;
        private const float DriftLeftPixelsPerSecond = 18f;
        private const float FallPixelsPerSecond = 30f;
        private const float OverlayDistance = 0.5f;

        private static readonly Color[] SnowColors =
        {
            new(1f, 1f, 1f, 0.92f),
            new(0.84f, 0.94f, 1f, 0.86f),
            new(0.68f, 0.84f, 1f, 0.80f)
        };

        private readonly Flake[] flakes = new Flake[FlakeCount];

        private Camera targetCamera;
        private Sprite flakeSprite;
        private Texture2D flakeTexture;
        private int pixelsPerUnit;
        private int viewportWidthPixels;
        private int viewportHeightPixels;
        private bool initialized;

        private void Start()
        {
            targetCamera = Camera.main;
            InitializeIfPossible();
        }

        private void LateUpdate()
        {
            RefreshTargetCamera();
            if (!initialized)
            {
                InitializeIfPossible();
                return;
            }

            if (!TryResolveViewport(out int width, out int height))
                return;

            if (width != viewportWidthPixels || height != viewportHeightPixels)
            {
                viewportWidthPixels = width;
                viewportHeightPixels = height;
                PlaceAllFlakes();
            }

            // Snow remains visible during the stage intro and pause, both of
            // which set Time.timeScale to zero.
            float elapsed = Time.unscaledDeltaTime;
            for (int i = 0; i < flakes.Length; i++)
            {
                Flake flake = flakes[i];
                flake.xPixels -= DriftLeftPixelsPerSecond * elapsed;
                flake.yPixels -= FallPixelsPerSecond * elapsed;

                if (flake.xPixels < 0f)
                    flake.xPixels += viewportWidthPixels;

                if (flake.yPixels < 0f)
                    flake.yPixels += viewportHeightPixels;

                ApplyPosition(flake);
            }
        }

        private void OnDisable()
        {
            for (int i = 0; i < flakes.Length; i++)
            {
                if (flakes[i]?.transform != null)
                    Destroy(flakes[i].transform.gameObject);

                flakes[i] = null;
            }

            if (flakeSprite != null)
                Destroy(flakeSprite);

            if (flakeTexture != null)
                Destroy(flakeTexture);

            initialized = false;
        }

        private void RefreshTargetCamera()
        {
            Camera activeCamera = Camera.main;
            if (activeCamera == null || activeCamera == targetCamera)
                return;

            targetCamera = activeCamera;
            for (int i = 0; i < flakes.Length; i++)
            {
                if (flakes[i]?.transform != null)
                    flakes[i].transform.SetParent(targetCamera.transform, false);
            }

            if (initialized)
                PlaceAllFlakes();
        }

        private void InitializeIfPossible()
        {
            if (initialized || targetCamera == null || !TryResolveViewport(out int width, out int height))
                return;

            viewportWidthPixels = width;
            viewportHeightPixels = height;
            flakeSprite = CreateFlakeSprite();

            for (int i = 0; i < flakes.Length; i++)
            {
                GameObject flakeObject = new($"SnowFlake_{i + 1:000}");
                flakeObject.transform.SetParent(targetCamera.transform, false);
                flakeObject.transform.localPosition = new Vector3(0f, 0f, OverlayDistance);

                SpriteRenderer renderer = flakeObject.AddComponent<SpriteRenderer>();
                renderer.sprite = flakeSprite;
                renderer.color = SnowColors[Random.Range(0, SnowColors.Length)];
                renderer.sortingOrder = 100;

                flakes[i] = new Flake { transform = flakeObject.transform };
            }

            initialized = true;
            PlaceAllFlakes();
        }

        private bool TryResolveViewport(out int width, out int height)
        {
            width = 0;
            height = 0;

            if (targetCamera == null || !targetCamera.orthographic)
                return false;

            PixelPerfectCamera pixelPerfectCamera = targetCamera.GetComponent<PixelPerfectCamera>();
            pixelsPerUnit = pixelPerfectCamera != null
                ? Mathf.Max(1, pixelPerfectCamera.assetsPPU)
                : Mathf.RoundToInt(DefaultPixelsPerUnit);

            height = Mathf.Max(1, Mathf.RoundToInt(targetCamera.orthographicSize * 2f * pixelsPerUnit));
            width = Mathf.Max(1, Mathf.RoundToInt(height * targetCamera.aspect));
            return true;
        }

        private void PlaceAllFlakes()
        {
            for (int i = 0; i < flakes.Length; i++)
                PlaceFlakeInRandomCell(flakes[i], i);
        }

        private void PlaceFlakeInRandomCell(Flake flake, int index)
        {
            int column = index % GridColumns;
            int row = index / GridColumns % GridRows;
            float cellWidth = viewportWidthPixels / (float)GridColumns;
            float cellHeight = viewportHeightPixels / (float)GridRows;

            flake.xPixels = column * cellWidth + Random.Range(0f, cellWidth);
            flake.yPixels = row * cellHeight + Random.Range(0f, cellHeight);
        }

        private void ApplyPosition(Flake flake)
        {
            int x = Mathf.FloorToInt(flake.xPixels);
            float localX = (x + 0.5f - viewportWidthPixels * 0.5f) / pixelsPerUnit;
            int y = Mathf.FloorToInt(flake.yPixels);
            float localY = (y + 0.5f - viewportHeightPixels * 0.5f) / pixelsPerUnit;
            flake.transform.localPosition = new Vector3(localX, localY, OverlayDistance);
        }

        private Sprite CreateFlakeSprite()
        {
            flakeTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "Stage35SnowFlake"
            };
            flakeTexture.SetPixel(0, 0, Color.white);
            flakeTexture.Apply(false, true);

            return Sprite.Create(flakeTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), pixelsPerUnit);
        }

        private sealed class Flake
        {
            public Transform transform;
            public float xPixels;
            public float yPixels;
        }
    }
}
