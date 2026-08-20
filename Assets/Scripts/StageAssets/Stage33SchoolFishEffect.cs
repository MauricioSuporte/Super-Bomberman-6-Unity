using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StageAssets
{
    /// <summary>
    /// A screen-space school for Stage 3-3. Each pass is parented to the active
    /// gameplay camera, so it stays inside the pixel-perfect viewport while the
    /// camera follows the players.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Stage33SchoolFishEffect : MonoBehaviour
    {
        private const float PixelsPerUnit = 16f;
        private const float MaxFormationOffsetX = 2.2f;
        private const float MaxFormationOffsetY = 1.8f;
        private const float ScreenExitPadding = 0.2f;

        // A loose, staggered school: lead fish, trailing fish and fish on both sides.
        private static readonly Vector2[] FormationSlots =
        {
            new(-1.9f, 1.04f),
            new(-0.56f, -1.44f),
            new(0.64f, 0.28f),
            new(1.96f, -0.72f),
            new(1.44f, 1.56f),
            new(-1.4f, -0.16f)
        };

        [Header("Fish")]
        [SerializeField] private Sprite fishSprite;
        [SerializeField, Min(1)] private int fishPerSchool = 6;
        [SerializeField] private int sortingOrder = 8;

        [Header("Timing")]
        [SerializeField] private bool useFixedTestInterval = true;
        [SerializeField, Min(0.01f)] private float testIntervalSeconds = 20f;
        [SerializeField, Min(0.01f)] private float intervalMinSeconds = 20f;
        [SerializeField, Min(0.01f)] private float intervalMaxSeconds = 30f;
        [SerializeField, Min(0.01f)] private float swimDurationSeconds = 1.25f;

        private readonly List<FishInstance> activeFish = new();

        private Coroutine spawnRoutine;

        private void OnEnable()
        {
            spawnRoutine = StartCoroutine(SpawnRoutine());
        }

        private void OnDisable()
        {
            if (spawnRoutine != null)
                StopCoroutine(spawnRoutine);

            spawnRoutine = null;
            ClearActiveFish();
        }

        private IEnumerator SpawnRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(GetNextIntervalSeconds());
                SpawnSchool(Camera.main);
            }
        }

        private void Update()
        {
            if (activeFish.Count == 0)
                return;

            for (int i = activeFish.Count - 1; i >= 0; i--)
            {
                FishInstance fish = activeFish[i];
                if (fish.Transform == null)
                {
                    activeFish.RemoveAt(i);
                    continue;
                }

                float progress = Mathf.Clamp01((Time.time - fish.StartTime) / swimDurationSeconds);
                SetFishPosition(fish.Transform, fish.Start, fish.Control, fish.End, fish.FormationOffset, progress);

                if (progress >= 1f)
                {
                    Destroy(fish.Transform.gameObject);
                    activeFish.RemoveAt(i);
                }
            }
        }

        private void SpawnSchool(Camera camera)
        {
            if (camera == null || fishSprite == null || !camera.orthographic)
                return;

            float halfHeight = camera.orthographicSize;
            float halfWidth = halfHeight * camera.aspect;
            int direction = Random.value < 0.5f ? 1 : -1;
            float spriteHalfWidth = fishSprite.bounds.extents.x;
            float spriteHalfHeight = fishSprite.bounds.extents.y;

            // The full sprite bounds and every formation offset remain outside the
            // viewport at progress 0 and 1, avoiding visible pop-in/pop-out.
            Vector2 start = new(
                -direction * (halfWidth + spriteHalfWidth + MaxFormationOffsetX + ScreenExitPadding),
                -halfHeight * 0.58f);
            Vector2 control = new(0f, -halfHeight * 0.06f);
            Vector2 end = new(
                direction * halfWidth * 0.72f,
                halfHeight + spriteHalfHeight + MaxFormationOffsetY + ScreenExitPadding);

            for (int i = 0; i < fishPerSchool; i++)
            {
                GameObject fishObject = new($"Fish_{i + 1:00}");
                fishObject.transform.SetParent(camera.transform, false);
                fishObject.transform.localPosition = new Vector3(0f, 0f, camera.nearClipPlane + 0.1f);

                SpriteRenderer renderer = fishObject.AddComponent<SpriteRenderer>();
                renderer.sprite = fishSprite;
                renderer.sortingOrder = sortingOrder + i;
                renderer.flipX = direction < 0;

                Vector2 slot = FormationSlots[i % FormationSlots.Length];
                Vector2 formationOffset = new(
                    -direction * slot.x + Random.Range(-0.15f, 0.15f),
                    slot.y + Random.Range(-0.18f, 0.18f));

                // Set the initial position immediately; Update may not run until
                // the following frame, which otherwise briefly shows a fish at
                // the camera origin.
                SetFishPosition(fishObject.transform, start, control, end, formationOffset, 0f);

                activeFish.Add(new FishInstance(
                    fishObject.transform,
                    start,
                    control,
                    end,
                    formationOffset,
                    Time.time));
            }
        }

        private void ClearActiveFish()
        {
            for (int i = 0; i < activeFish.Count; i++)
            {
                if (activeFish[i].Transform != null)
                    Destroy(activeFish[i].Transform.gameObject);
            }

            activeFish.Clear();
        }

        private float GetNextIntervalSeconds()
        {
            if (useFixedTestInterval)
                return testIntervalSeconds;

            float min = Mathf.Min(intervalMinSeconds, intervalMaxSeconds);
            float max = Mathf.Max(intervalMinSeconds, intervalMaxSeconds);
            return Random.Range(min, max);
        }

        private static Vector2 EvaluateQuadraticBezier(Vector2 start, Vector2 control, Vector2 end, float progress)
        {
            float inverse = 1f - progress;
            return inverse * inverse * start + 2f * inverse * progress * control + progress * progress * end;
        }

        private static void SetFishPosition(
            Transform fish,
            Vector2 start,
            Vector2 control,
            Vector2 end,
            Vector2 formationOffset,
            float progress)
        {
            Vector2 position = EvaluateQuadraticBezier(start, control, end, progress) + formationOffset;
            fish.localPosition = new Vector3(Snap(position.x), Snap(position.y), fish.localPosition.z);
        }

        private static float Snap(float value) => Mathf.Round(value * PixelsPerUnit) / PixelsPerUnit;

        private readonly struct FishInstance
        {
            public readonly Transform Transform;
            public readonly Vector2 Start;
            public readonly Vector2 Control;
            public readonly Vector2 End;
            public readonly Vector2 FormationOffset;
            public readonly float StartTime;

            public FishInstance(Transform transform, Vector2 start, Vector2 control, Vector2 end, Vector2 formationOffset, float startTime)
            {
                Transform = transform;
                Start = start;
                Control = control;
                End = end;
                FormationOffset = formationOffset;
                StartTime = startTime;
            }
        }
    }
}
