using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace StageAssets
{
    /// <summary>
    /// Wakes the Room 2 volcano and launches its falling eruption attack.
    /// </summary>
    public sealed class Stage32VolcanoEyesAnimator : MonoBehaviour
    {
        private const int EruptionsPerBurst = 5;
        private const float EruptionLaunchWindowSeconds = 1f;
        private const float EruptionFlightSeconds = 3f;
        private const float TargetRevealSeconds = 1.5f;
        private const float TargetFrameSeconds = 0.1f;
        private const float EruptionImpactSeconds = 0.5f;
        private const float EruptionInAirSfxDelaySeconds = 0.5f;
        private const float FallingVisualYOffset = 0.5f;
        private static readonly int[] EyeFrameSequence = { 0, 0, 1, 2, 3, 0, 1, 2, 3, 2, 1, 0, 0 };

        private sealed class ActiveEruption
        {
            public SpriteRenderer EruptionRenderer;
            public SpriteRenderer TargetRenderer;
            public Vector2 Target;
            public float SpawnedAt;
            public float ImpactStartedAt = -1f;
        }

        [Header("Eyes timing")]
        [SerializeField, Min(0.01f)] private float minimumRepeatSeconds = 8f;
        [SerializeField, Min(0.01f)] private float maximumRepeatSeconds = 10f;
        [SerializeField, Min(0.01f)] private float animationDurationSeconds = 1.8f;

        [Header("Eyes visual")]
        [SerializeField] private Sprite[] eyeSprites;

        [Header("Eruption")]
        [SerializeField] private Vector2 eruptionOrigin = new(21f, -2f);
        [SerializeField, Min(0f)] private float eruptionArcHeightTiles = 20f;
        [SerializeField] private Sprite[] eruptionSprites;
        [SerializeField] private Sprite[] eruptionTargetSprites;
        [SerializeField] private AudioClip eruptionStartSfx;
        [SerializeField] private AudioClip eruptionInTheAirSfx;
        [SerializeField] private AudioClip eruptionFallSfx;

        [Header("Rendering")]
        [SerializeField] private string sortingLayerName = "Default";
        [SerializeField] private int sortingOrder = 4;

        [Header("Room activation")]
        [SerializeField] private Collider2D roomBounds;

        private readonly List<ActiveEruption> activeEruptions = new();
        private readonly List<Vector2> availableTargets = new();

        private SpriteRenderer eyeRenderer;
        private AudioSource audioSource;
        private BombController bombController;
        private Tilemap groundTilemap;
        private float animationStartedAt;
        private float nextAnimationAt;
        private bool animationActive;
        private bool eruptionBurstStarted;
        private int launchedEruptions;
        private float eruptionInAirSfxAt = -1f;

        private void Awake()
        {
            eyeRenderer = GetComponent<SpriteRenderer>();
            if (eyeRenderer != null)
            {
                eyeRenderer.sortingLayerName = sortingLayerName;
                eyeRenderer.sortingOrder = sortingOrder;
                eyeRenderer.enabled = false;
            }

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();

            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }

        private void OnEnable()
        {
            animationActive = false;
            eruptionBurstStarted = false;
            eruptionInAirSfxAt = -1f;
            nextAnimationAt = float.PositiveInfinity;
            HideEyes();
        }

        private void OnDisable() => StopActiveEruptions();

        private void Update()
        {
            if (!HasValidConfiguration())
                return;

            float now = Time.time;
            if (roomBounds == null || !World3RoomProgressionController.IsRoomOccupied(roomBounds))
            {
                animationActive = false;
                eruptionBurstStarted = false;
                nextAnimationAt = float.PositiveInfinity;
                HideEyes();
                StopActiveEruptions();
                return;
            }

            if (float.IsPositiveInfinity(nextAnimationAt))
                nextAnimationAt = now + GetRepeatDelay();

            if (!animationActive && now >= nextAnimationAt)
            {
                animationActive = true;
                eruptionBurstStarted = false;
                animationStartedAt = now;
                eyeRenderer.enabled = true;
            }

            if (animationActive)
                UpdateEyeAnimation(now);

            UpdateEruptions(now);
        }

        private void UpdateEyeAnimation(float now)
        {
            float age = now - animationStartedAt;
            if (!eruptionBurstStarted && age >= 1f)
                StartEruptionBurst(now);

            if (age >= animationDurationSeconds)
            {
                animationActive = false;
                nextAnimationAt = now + GetRepeatDelay();
                HideEyes();
                return;
            }

            int frameIndex = Mathf.Min(
                Mathf.FloorToInt(age / animationDurationSeconds * EyeFrameSequence.Length),
                EyeFrameSequence.Length - 1);
            eyeRenderer.sprite = eyeSprites[EyeFrameSequence[frameIndex]];
        }

        private void StartEruptionBurst(float now)
        {
            eruptionBurstStarted = true;
            launchedEruptions = 0;
            PopulateAvailableTargets();
            LaunchScheduledEruptions(now, 0f);
        }

        private void LaunchScheduledEruptions(float now, float burstAge)
        {
            int desiredLaunchCount = Mathf.Clamp(
                Mathf.FloorToInt(burstAge / (EruptionLaunchWindowSeconds / (EruptionsPerBurst - 1))) + 1,
                0,
                EruptionsPerBurst);

            while (launchedEruptions < desiredLaunchCount)
            {
                if (!TryTakeTarget(out Vector2 target))
                    break;

                SpawnEruption(target, now);
                launchedEruptions++;
                PlaySfx(eruptionStartSfx);

                if (launchedEruptions == EruptionsPerBurst)
                    eruptionInAirSfxAt = now + EruptionInAirSfxDelaySeconds;
            }
        }

        private void SpawnEruption(Vector2 target, float now)
        {
            GameObject eruptionObject = new("Volcano Eruption");
            eruptionObject.transform.SetParent(transform, false);
            SpriteRenderer eruptionRenderer = eruptionObject.AddComponent<SpriteRenderer>();
            eruptionRenderer.sortingLayerName = sortingLayerName;
            eruptionRenderer.sortingOrder = sortingOrder;
            eruptionRenderer.sprite = eruptionSprites[0];
            eruptionRenderer.flipY = false;

            GameObject targetObject = new("Volcano Eruption Target");
            targetObject.transform.SetParent(transform, false);
            targetObject.transform.position = new Vector3(target.x, target.y, -0.05f);
            SpriteRenderer targetRenderer = targetObject.AddComponent<SpriteRenderer>();
            targetRenderer.sortingLayerName = sortingLayerName;
            targetRenderer.sortingOrder = sortingOrder - 1;
            targetRenderer.sprite = eruptionTargetSprites[0];
            targetRenderer.enabled = false;

            activeEruptions.Add(new ActiveEruption
            {
                EruptionRenderer = eruptionRenderer,
                TargetRenderer = targetRenderer,
                Target = target,
                SpawnedAt = now
            });
        }

        private void UpdateEruptions(float now)
        {
            if (eruptionInAirSfxAt >= 0f && now >= eruptionInAirSfxAt)
            {
                PlaySfx(eruptionInTheAirSfx);
                eruptionInAirSfxAt = -1f;
            }

            if (eruptionBurstStarted && launchedEruptions < EruptionsPerBurst)
            {
                float burstAge = now - (animationStartedAt + 1f);
                LaunchScheduledEruptions(now, Mathf.Max(0f, burstAge));
            }

            for (int i = activeEruptions.Count - 1; i >= 0; i--)
            {
                ActiveEruption eruption = activeEruptions[i];
                if (eruption.ImpactStartedAt >= 0f)
                {
                    UpdateImpactAnimation(eruption, now);
                    if (now - eruption.ImpactStartedAt >= EruptionImpactSeconds)
                    {
                        Destroy(eruption.EruptionRenderer.gameObject);
                        activeEruptions.RemoveAt(i);
                    }
                    continue;
                }

                float age = now - eruption.SpawnedAt;
                if (age >= EruptionFlightSeconds)
                {
                    BeginImpact(eruption, now);
                    continue;
                }

                float progress = Mathf.Clamp01(age / EruptionFlightSeconds);
                Vector2 position = Vector2.Lerp(eruptionOrigin, eruption.Target, progress);
                position.y += 4f * eruptionArcHeightTiles * progress * (1f - progress);
                if (progress >= 0.5f)
                    position.y += FallingVisualYOffset;
                eruption.EruptionRenderer.transform.position = new Vector3(position.x, position.y, -0.1f);
                eruption.EruptionRenderer.flipY = progress >= 0.5f;

                int eruptionFrame = progress < 0.5f
                    ? Mathf.Min(Mathf.FloorToInt(age / TargetFrameSeconds), eruptionSprites.Length - 1)
                    : 3 + Mathf.FloorToInt(age / TargetFrameSeconds) % 2;
                eruption.EruptionRenderer.sprite = eruptionSprites[eruptionFrame];

                bool targetVisible = age >= TargetRevealSeconds;
                eruption.TargetRenderer.enabled = targetVisible;
                if (targetVisible)
                    eruption.TargetRenderer.sprite = eruptionTargetSprites[Mathf.FloorToInt(age / TargetFrameSeconds) % 2];
            }
        }

        private void BeginImpact(ActiveEruption eruption, float now)
        {
            PlaySfx(eruptionFallSfx);
            ResolveBombController();
            bombController?.SpawnSingleTileExplosionDamageForEffect(
                eruption.Target,
                EruptionImpactSeconds);

            if (eruption.TargetRenderer != null)
                Destroy(eruption.TargetRenderer.gameObject);

            eruption.ImpactStartedAt = now;
            eruption.EruptionRenderer.transform.position = new Vector3(
                eruption.Target.x,
                eruption.Target.y + FallingVisualYOffset,
                -0.1f);
            eruption.EruptionRenderer.flipY = true;
        }

        private void UpdateImpactAnimation(ActiveEruption eruption, float now)
        {
            float impactAge = now - eruption.ImpactStartedAt;
            int frame = Mathf.Min(
                Mathf.FloorToInt(impactAge / EruptionImpactSeconds * eruptionSprites.Length),
                eruptionSprites.Length - 1);
            eruption.EruptionRenderer.sprite = eruptionSprites[eruptionSprites.Length - 1 - frame];
        }

        private void PopulateAvailableTargets()
        {
            availableTargets.Clear();
            ResolveGroundTilemap();
            if (groundTilemap == null || roomBounds == null)
                return;

            foreach (Vector3Int cell in groundTilemap.cellBounds.allPositionsWithin)
            {
                if (groundTilemap.GetTile(cell) == null)
                    continue;

                Vector3 center = groundTilemap.GetCellCenterWorld(cell);
                if (roomBounds.OverlapPoint(center) && !HasIndestructibleTileAt(center))
                    availableTargets.Add(center);
            }
        }

        private static bool HasIndestructibleTileAt(Vector3 worldPosition)
        {
            Tilemap indestructibleTilemap = GameManager.Instance != null
                ? GameManager.Instance.indestructibleTilemap
                : null;
            return indestructibleTilemap != null &&
                   indestructibleTilemap.GetTile(indestructibleTilemap.WorldToCell(worldPosition)) != null;
        }

        private bool TryTakeTarget(out Vector2 target)
        {
            if (availableTargets.Count == 0)
            {
                target = default;
                return false;
            }

            int index = Random.Range(0, availableTargets.Count);
            target = availableTargets[index];
            availableTargets.RemoveAt(index);
            return true;
        }

        private void ResolveGroundTilemap()
        {
            if (groundTilemap == null && GameManager.Instance != null)
                groundTilemap = GameManager.Instance.groundTilemap;
        }

        private void ResolveBombController()
        {
            if (bombController == null)
                bombController = FindAnyObjectByType<BombController>();
        }

        private void StopActiveEruptions()
        {
            for (int i = 0; i < activeEruptions.Count; i++)
            {
                ActiveEruption eruption = activeEruptions[i];
                if (eruption.EruptionRenderer != null)
                    Destroy(eruption.EruptionRenderer.gameObject);
                if (eruption.TargetRenderer != null)
                    Destroy(eruption.TargetRenderer.gameObject);
            }

            activeEruptions.Clear();
            availableTargets.Clear();
            launchedEruptions = 0;
            eruptionInAirSfxAt = -1f;
        }

        private void PlaySfx(AudioClip clip)
        {
            if (clip != null && audioSource != null)
                audioSource.PlayOneShot(clip);
        }

        private float GetRepeatDelay() => Random.Range(
            Mathf.Min(minimumRepeatSeconds, maximumRepeatSeconds),
            Mathf.Max(minimumRepeatSeconds, maximumRepeatSeconds));

        private bool HasValidConfiguration() =>
            eyeRenderer != null && eyeSprites != null && eyeSprites.Length >= 4 &&
            eyeSprites[0] != null && eyeSprites[1] != null && eyeSprites[2] != null && eyeSprites[3] != null &&
            eruptionSprites != null && eruptionSprites.Length >= 5 &&
            eruptionTargetSprites != null && eruptionTargetSprites.Length >= 2;

        private void HideEyes()
        {
            if (eyeRenderer != null)
                eyeRenderer.enabled = false;
        }
    }
}
