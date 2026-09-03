using UnityEngine;
using UnityEngine.U2D;

namespace StageAssets
{
    /// <summary>
    /// Screen-anchored one-pixel sand grains for Stage 3-4. The grains are
    /// children of the gameplay camera, so the camera's pixel-perfect viewport
    /// also clips them to the safe frame.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Stage34SandstormEffect : MonoBehaviour
    {
        private const string RoomOneName = "Room 1";
        private const string RoomThreeName = "Room 3";
        private const int GrainCount = 200;
        private const int GridColumns = 20;
        private const int GridRows = 10;
        private const float DefaultPixelsPerUnit = 16f;
        private const float SpeedPixelsPerSecond = 112f;
        private const float FallPixelsPerSecond = 14f;
        private const float OverlayDistance = 0.5f;

        private static readonly Color[] SandColors =
        {
            new(0.94f, 0.76f, 0.42f, 0.8f),
            new(0.82f, 0.58f, 0.27f, 0.78f),
            new(0.69f, 0.43f, 0.19f, 0.74f),
            new(0.98f, 0.86f, 0.58f, 0.78f),
            new(0.74f, 0.52f, 0.33f, 0.72f)
        };

        private readonly Grain[] grains = new Grain[GrainCount];

        private Camera targetCamera;
        private Sprite grainSprite;
        private Texture2D grainTexture;
        private int pixelsPerUnit;
        private int viewportWidthPixels;
        private int viewportHeightPixels;
        private bool initialized;
        private bool grainsVisible;
        private Collider2D roomOneBounds;
        private Collider2D roomThreeBounds;

        private void Start()
        {
            targetCamera = Camera.main;
            InitializeIfPossible();
        }

        private void LateUpdate()
        {
            RefreshTargetCamera();

            bool shouldShow = IsAllowedRoomOccupied();
            if (!initialized)
            {
                if (shouldShow)
                    InitializeIfPossible();
                return;
            }

            SetGrainsVisible(shouldShow);
            if (!shouldShow)
                return;

            if (!TryResolveViewport(out int width, out int height))
                return;

            if (width != viewportWidthPixels || height != viewportHeightPixels)
            {
                viewportWidthPixels = width;
                viewportHeightPixels = height;
                PlaceAllGrains();
            }

            // Sand remains visible during the stage intro and pause, both of
            // which set Time.timeScale to zero.
            float elapsed = Time.unscaledDeltaTime;
            for (int i = 0; i < grains.Length; i++)
            {
                Grain grain = grains[i];
                grain.xPixels += SpeedPixelsPerSecond * elapsed;
                grain.yPixels -= FallPixelsPerSecond * elapsed;

                // Wrapping preserves the randomized 20x10 distribution: there
                // is always a grain in every band of the safe frame.
                if (grain.xPixels >= viewportWidthPixels)
                    grain.xPixels -= viewportWidthPixels;

                if (grain.yPixels < 0f)
                    grain.yPixels += viewportHeightPixels;

                ApplyPosition(grain);
            }
        }

        private void OnDisable()
        {
            for (int i = 0; i < grains.Length; i++)
            {
                if (grains[i].transform != null)
                    Destroy(grains[i].transform.gameObject);

                grains[i] = null;
            }

            if (grainSprite != null)
                Destroy(grainSprite);

            if (grainTexture != null)
                Destroy(grainTexture);

            initialized = false;
            grainsVisible = false;
        }

        private void RefreshTargetCamera()
        {
            Camera activeCamera = Camera.main;
            if (activeCamera == null || activeCamera == targetCamera)
                return;

            targetCamera = activeCamera;
            for (int i = 0; i < grains.Length; i++)
            {
                if (grains[i]?.transform != null)
                    grains[i].transform.SetParent(targetCamera.transform, false);
            }

            if (initialized)
                PlaceAllGrains();
        }

        private bool IsAllowedRoomOccupied()
        {
            if (roomOneBounds == null)
                roomOneBounds = World3RoomProgressionController.FindRoomBounds(RoomOneName);

            if (roomThreeBounds == null)
                roomThreeBounds = World3RoomProgressionController.FindRoomBounds(RoomThreeName);

            return (roomOneBounds != null && World3RoomProgressionController.IsRoomOccupied(roomOneBounds)) ||
                   (roomThreeBounds != null && World3RoomProgressionController.IsRoomOccupied(roomThreeBounds));
        }

        private void SetGrainsVisible(bool visible)
        {
            if (grainsVisible == visible)
                return;

            grainsVisible = visible;
            for (int i = 0; i < grains.Length; i++)
            {
                if (grains[i]?.transform != null)
                    grains[i].transform.gameObject.SetActive(visible);
            }
        }

        private void InitializeIfPossible()
        {
            if (initialized || targetCamera == null || !TryResolveViewport(out int width, out int height))
                return;

            viewportWidthPixels = width;
            viewportHeightPixels = height;
            grainSprite = CreateGrainSprite();

            for (int i = 0; i < grains.Length; i++)
            {
                GameObject grainObject = new($"SandGrain_{i + 1:000}");
                grainObject.transform.SetParent(targetCamera.transform, false);
                grainObject.transform.localPosition = new Vector3(0f, 0f, OverlayDistance);

                SpriteRenderer renderer = grainObject.AddComponent<SpriteRenderer>();
                renderer.sprite = grainSprite;
                renderer.color = SandColors[Random.Range(0, SandColors.Length)];
                renderer.sortingOrder = 100;

                grains[i] = new Grain { transform = grainObject.transform };
            }

            initialized = true;
            SetGrainsVisible(true);
            PlaceAllGrains();
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

        private void PlaceAllGrains()
        {
            for (int i = 0; i < grains.Length; i++)
                PlaceGrainInRandomCell(grains[i], i);
        }

        private void PlaceGrainInRandomCell(Grain grain, int index)
        {
            int column = index % GridColumns;
            int row = index / GridColumns % GridRows;
            float cellWidth = viewportWidthPixels / (float)GridColumns;
            float cellHeight = viewportHeightPixels / (float)GridRows;

            grain.xPixels = column * cellWidth + Random.Range(0f, cellWidth);
            grain.yPixels = row * cellHeight + Random.Range(0f, cellHeight);
        }

        private void ApplyPosition(Grain grain)
        {
            int x = Mathf.FloorToInt(grain.xPixels);
            float localX = (x + 0.5f - viewportWidthPixels * 0.5f) / pixelsPerUnit;
            int y = Mathf.FloorToInt(grain.yPixels);
            float localY = (y + 0.5f - viewportHeightPixels * 0.5f) / pixelsPerUnit;
            grain.transform.localPosition = new Vector3(localX, localY, OverlayDistance);
        }

        private Sprite CreateGrainSprite()
        {
            grainTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "Stage34SandGrain"
            };
            grainTexture.SetPixel(0, 0, Color.white);
            grainTexture.Apply(false, true);

            return Sprite.Create(grainTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), pixelsPerUnit);
        }

        private sealed class Grain
        {
            public Transform transform;
            public float xPixels;
            public float yPixels;
        }
    }
}
