using System.Collections.Generic;
using UnityEngine;

namespace StageAssets
{
    [DisallowMultipleComponent]
    public sealed class Stage34BatSwarmEffect : MonoBehaviour
    {
        private const float PixelsPerUnit = 16f;

        [Header("Sprites (32 x 32, 2 frames)")]
        [SerializeField] private Sprite[] batFrames;
        [SerializeField] private AudioClip swarmStartSfx;
        [SerializeField, Range(0f, 1f)] private float swarmStartSfxVolume = 0.55f;
        [SerializeField] private string sortingLayerName = "Default";
        [SerializeField] private int sortingOrder = 100;

        [Header("Flock")]
        [SerializeField, Min(1)] private int batCount = 10;
        [SerializeField, Min(0.01f)] private float flightDurationSeconds = 3f;
        [SerializeField, Min(0.01f)] private float frameDurationSeconds = 0.1f;
        [SerializeField, Min(0f)] private float entryStaggerSeconds = 1.2f;

        private readonly List<BatFlight> activeFlights = new();
        private Camera activeCamera;
        private AudioSource audioSource;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();

            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;
        }

        public void Play(Camera camera)
        {
            ClearFlights();
            activeCamera = camera != null ? camera : Camera.main;
            if (activeCamera == null || !activeCamera.orthographic || batFrames == null || batFrames.Length < 2)
                return;

            PlaySwarmStartSfx();
            for (int i = 0; i < batCount; i++)
                CreateBat(i);
        }

        private void Update()
        {
            if (activeFlights.Count == 0)
                return;

            float now = Time.unscaledTime;
            for (int i = activeFlights.Count - 1; i >= 0; i--)
            {
                BatFlight flight = activeFlights[i];
                if (flight.Renderer == null)
                {
                    activeFlights.RemoveAt(i);
                    continue;
                }

                float age = now - flight.StartTime;
                if (age < 0f)
                {
                    flight.Renderer.enabled = false;
                    continue;
                }

                float progress = Mathf.Clamp01(age / flightDurationSeconds);
                flight.Renderer.enabled = true;
                flight.Renderer.sprite = batFrames[Mathf.FloorToInt(age / frameDurationSeconds) % batFrames.Length];
                SetPixelPerfectPosition(flight.Transform, EvaluateQuadraticBezier(flight.Start, flight.Control, flight.End, progress));

                if (progress < 1f)
                    continue;

                Destroy(flight.Transform.gameObject);
                activeFlights.RemoveAt(i);
            }
        }

        private void OnDisable() => ClearFlights();

        private void CreateBat(int index)
        {
            float halfHeight = activeCamera.orthographicSize;
            float halfWidth = halfHeight * activeCamera.aspect;
            bool leftToRight = index % 2 == 0;
            float direction = leftToRight ? 1f : -1f;
            int sideIndex = index / 2;
            int batsPerSide = Mathf.Max(1, (batCount + 1) / 2);
            float lane = (sideIndex + Random.Range(0.1f, 0.9f)) / batsPerSide;
            float startY = Mathf.Lerp(halfHeight * 0.18f, halfHeight * 0.95f, lane);
            float exitY = Mathf.Lerp(halfHeight * 0.2f, halfHeight * 0.94f, Random.value);
            float spriteHalfHeight = batFrames[0].bounds.extents.y;
            float lowestCenterY = -halfHeight + spriteHalfHeight;
            float lowestPassY = Mathf.Lerp(lowestCenterY, -halfHeight * 0.38f, Random.value);
            float controlY = 2f * lowestPassY - 0.5f * (startY + exitY);
            float controlX = Random.Range(-halfWidth * 0.42f, halfWidth * 0.42f);
            const float spritePadding = 1.25f;

            Vector2 start = new(-direction * (halfWidth + spritePadding), startY);
            Vector2 control = new(controlX, controlY);
            Vector2 end = new(direction * (halfWidth + spritePadding), exitY);

            GameObject batObject = new($"Room 2 Bat {index + 1:00}");
            batObject.transform.SetParent(activeCamera.transform, false);
            batObject.transform.localPosition = new Vector3(0f, 0f, activeCamera.nearClipPlane + 0.1f);

            SpriteRenderer renderer = batObject.AddComponent<SpriteRenderer>();
            renderer.sprite = batFrames[0];
            renderer.sortingLayerName = sortingLayerName;
            renderer.sortingOrder = sortingOrder + index;
            renderer.flipX = !leftToRight;
            renderer.enabled = false;

            float evenlySpacedEntry = index / (float)Mathf.Max(1, batCount - 1) * entryStaggerSeconds;
            float timingJitter = Random.Range(-0.06f, 0.06f);
            activeFlights.Add(new BatFlight(batObject.transform, renderer, start, control, end,
                Time.unscaledTime + Mathf.Max(0f, evenlySpacedEntry + timingJitter)));
        }

        private void PlaySwarmStartSfx()
        {
            if (audioSource == null || swarmStartSfx == null)
                return;

            GameAudioSettings.PlaySfx(audioSource, swarmStartSfx, swarmStartSfxVolume);
        }

        private void ClearFlights()
        {
            for (int i = 0; i < activeFlights.Count; i++)
                if (activeFlights[i].Transform != null)
                    Destroy(activeFlights[i].Transform.gameObject);

            activeFlights.Clear();
        }

        private static Vector2 EvaluateQuadraticBezier(Vector2 start, Vector2 control, Vector2 end, float progress)
        {
            float inverse = 1f - progress;
            return inverse * inverse * start + 2f * inverse * progress * control + progress * progress * end;
        }

        private static void SetPixelPerfectPosition(Transform transform, Vector2 position)
        {
            position.x = Mathf.Round(position.x * PixelsPerUnit) / PixelsPerUnit;
            position.y = Mathf.Round(position.y * PixelsPerUnit) / PixelsPerUnit;
            transform.localPosition = new Vector3(position.x, position.y, transform.localPosition.z);
        }

        private readonly struct BatFlight
        {
            public readonly Transform Transform;
            public readonly SpriteRenderer Renderer;
            public readonly Vector2 Start;
            public readonly Vector2 Control;
            public readonly Vector2 End;
            public readonly float StartTime;

            public BatFlight(Transform transform, SpriteRenderer renderer, Vector2 start, Vector2 control, Vector2 end, float startTime)
            {
                Transform = transform;
                Renderer = renderer;
                Start = start;
                Control = control;
                End = end;
                StartTime = startTime;
            }
        }
    }
}
