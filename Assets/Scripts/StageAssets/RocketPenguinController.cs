using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace StageAssets
{
    public sealed class RocketPenguinController : MonoBehaviour
    {
        private const float PreparationSeconds = 1f;
        private const float AscentSeconds = 1f;
        private const float TargetWarningSeconds = 5f;
        private const float DescentSeconds = 1f;
        private const float LaunchingSeconds = 5f;
        private const float MinimumReloadSeconds = 5f;
        private const float MaximumReloadSeconds = 10f;
        private const float MinimumRoomLaunchGapSeconds = 3f;
        private const float MaximumRoomLaunchGapSeconds = 5f;
        private const float ExplosionSeconds = 0.5f;

        [Header("Penguin")]
        [SerializeField] private AnimatedSpriteRenderer penguin;
        [SerializeField] private AnimatedSpriteRenderer launching;
        [SerializeField] private AnimatedSpriteRenderer reload;
        [Header("Rocket")]
        [SerializeField] private AnimatedSpriteRenderer ascend;
        [SerializeField] private AnimatedSpriteRenderer ascendShadow;
        [SerializeField] private AnimatedSpriteRenderer descend;
        [SerializeField] private AnimatedSpriteRenderer target;
        [SerializeField] private AnimatedSpriteRenderer explosion;
        [SerializeField, Min(0f)] private float arcHeightTiles = 10f;
        [Header("Cycle")]
        [SerializeField, Min(0.1f)] private float minimumRepeatSeconds = 5f;
        [SerializeField, Min(0.1f)] private float maximumRepeatSeconds = 10f;
        [Tooltip("Optional. Automatically resolves the room nearest to the launcher when empty.")]
        [SerializeField] private Collider2D roomBounds;
        [Header("Audio")]
        [SerializeField] private AudioClip launchSfx;
        [SerializeField] private AudioClip descendSfx;
        [SerializeField] private AudioClip explosionSfx;

        private AudioSource audioSource;
        private bool audioPaused;

        private readonly List<Vector3> availableTargets = new();
        private float waitRemaining;
        private float shotAge;
        private float reloadSeconds;
        private float roomLaunchCooldown;
        private float tileHeight = 1f;
        private float roomRetryRemaining;
        private bool firing;
        private bool roomActive;
        private bool configurationValid;
        private int flightPhase;
        private Vector3 launchLocalPosition;
        private Vector3 origin;
        private Vector3 impactPosition;
        private float shadowFrameRemaining;
        private Vector3 shadowJitter;

        private void Awake()
        {
            configurationValid = penguin != null && launching != null && reload != null &&
                ascend != null && ascendShadow != null && descend != null && target != null && explosion != null;
            if (!configurationValid)
            {
                enabled = false;
                return;
            }

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            launchLocalPosition = ascend.transform.localPosition;
            explosion.idle = false;
            explosion.loop = false;
            explosion.useSequenceDuration = true;
            explosion.sequenceDuration = ExplosionSeconds;
        }

        private void OnEnable()
        {
            if (!configurationValid)
                return;
            ResetVisuals();
            ScheduleNextShot();
        }

        private void OnDisable()
        {
            if (!configurationValid)
                return;
            ResetVisuals();
            roomActive = false;
        }

        private void Update()
        {
            if (!configurationValid)
                return;
            if (audioPaused != GamePauseController.IsPaused)
            {
                audioPaused = GamePauseController.IsPaused;
                if (audioPaused)
                    audioSource.Pause();
                else
                    audioSource.UnPause();
            }
            if (GamePauseController.IsPaused)
                return;

            if (roomBounds == null)
            {
                roomRetryRemaining -= Time.deltaTime;
                if (roomRetryRemaining > 0f)
                    return;
                roomRetryRemaining = 1f;
                ResolveRoom();
                if (roomBounds == null)
                    return;
            }
            bool occupied = roomBounds != null && World3RoomProgressionController.IsRoomOccupied(roomBounds);
            if (!occupied)
            {
                if (roomActive)
                {
                    ResetVisuals();
                    ScheduleNextShot();
                }
                roomActive = false;
                return;
            }
            roomActive = true;
            roomLaunchCooldown = Mathf.Max(0f, roomLaunchCooldown - Time.deltaTime);

            if (!firing)
            {
                waitRemaining -= Time.deltaTime;
                return;
            }

            shotAge += Time.deltaTime;
            if (shotAge >= LaunchingSeconds && launching.gameObject.activeSelf)
            {
                Show(launching, false);
                Show(reload, true);
            }
            UpdateFlight();
            if (shotAge >= LaunchingSeconds + reloadSeconds)
            {
                ResetVisuals();
                ScheduleNextShot();
            }
        }

        private void LateUpdate()
        {
            if (GamePauseController.IsPaused || !IsReadyToLaunch() || roomLaunchCooldown > 0f)
                return;

            // All launchers have updated their timers before this arbitration.
            // Reservoir sampling selects a ready launcher without depending on Update order.
            RocketPenguinController[] launchers = FindObjectsByType<RocketPenguinController>();
            RocketPenguinController selected = null;
            int readyCount = 0;
            foreach (RocketPenguinController launcher in launchers)
            {
                if (!launcher.isActiveAndEnabled || launcher.roomBounds != roomBounds)
                    continue;
                if (launcher.roomLaunchCooldown > 0f)
                    return;
                if (launcher.IsReadyToLaunch() && Random.Range(0, ++readyCount) == 0)
                    selected = launcher;
            }

            if (selected == null || !selected.BeginShot())
                return;

            float gap = Random.Range(MinimumRoomLaunchGapSeconds, MaximumRoomLaunchGapSeconds);
            foreach (RocketPenguinController launcher in launchers)
            {
                if (launcher.roomBounds == roomBounds)
                    launcher.roomLaunchCooldown = gap;
            }
        }

        private bool IsReadyToLaunch() =>
            configurationValid && roomActive && roomBounds != null && !firing && waitRemaining <= 0f;

        private bool BeginShot()
        {
            Tilemap ground = GameManager.Instance != null ? GameManager.Instance.groundTilemap : null;
            Tilemap walls = GameManager.Instance != null ? GameManager.Instance.indestructibleTilemap : null;
            availableTargets.Clear();
            if (ground != null)
            {
                tileHeight = Vector3.Distance(ground.GetCellCenterWorld(Vector3Int.zero),
                    ground.GetCellCenterWorld(Vector3Int.up));
                foreach (Vector3Int cell in ground.cellBounds.allPositionsWithin)
                {
                    if (!ground.HasTile(cell))
                        continue;
                    Vector3 center = ground.GetCellCenterWorld(cell);
                    if (roomBounds.OverlapPoint(center) && (walls == null || !walls.HasTile(walls.WorldToCell(center))))
                        availableTargets.Add(center);
                }
            }
            if (availableTargets.Count == 0)
            {
                ScheduleNextShot();
                return false;
            }

            impactPosition = availableTargets[Random.Range(0, availableTargets.Count)];
            origin = transform.TransformPoint(launchLocalPosition);
            impactPosition.z = origin.z;
            firing = true;
            reloadSeconds = Random.Range(MinimumReloadSeconds, MaximumReloadSeconds);
            shotAge = 0f;
            flightPhase = 0;
            Show(penguin, false);
            Show(launching, true);
            Show(ascend, true);
            Show(ascendShadow, false);
            shadowFrameRemaining = 0f;
            Move(ascend, origin);
            Move(ascendShadow, origin + Vector3.down * (2f * tileHeight));
            return true;
        }

        private void UpdateFlight()
        {
            float flightAge = shotAge - PreparationSeconds;
            if (flightAge < 0f)
                return;
            if (flightPhase == 0)
            {
                flightPhase = 1;
                GameAudioSettings.PlaySfx(audioSource, launchSfx);
            }
            if (flightAge >= AscentSeconds && flightPhase == 1)
            {
                flightPhase = 2;
                Show(ascendShadow, false);
                Show(target, true);
                Move(target, impactPosition);
            }
            float descentStartsAt = AscentSeconds + TargetWarningSeconds - DescentSeconds;
            if (flightAge >= descentStartsAt && flightPhase == 2)
            {
                flightPhase = 3;
                audioSource.Stop();
                GameAudioSettings.PlaySfx(audioSource, descendSfx);
                Show(ascend, false);
                Show(descend, true);
            }
            float flightDuration = AscentSeconds + TargetWarningSeconds;
            if (flightAge >= flightDuration && flightPhase == 3)
            {
                flightPhase = 4;
                audioSource.Stop();
                if (explosionSfx != null)
                    audioSource.PlayOneShot(explosionSfx, 2f * GameAudioSettings.ApplySfxVolume(1f));
                Show(descend, false);
                Show(target, false);
                Show(explosion, true);
                Move(explosion, impactPosition + Vector3.up * (0.5f * tileHeight));
                TriggerZubattoRocketEvasion();
                BombController damageSource = FindAnyObjectByType<BombController>();
                if (damageSource != null)
                {
                    damageSource.SpawnSingleTileExplosionDamageForEffect(impactPosition, ExplosionSeconds);
                }
            }
            if (flightPhase == 4)
            {
                if (flightAge >= flightDuration + ExplosionSeconds)
                {
                    Show(explosion, false);
                    flightPhase = 5;
                }
                return;
            }
            if (flightPhase >= 5)
                return;

            // Quadratic ease-in starts the launch at rest, keeping the one-second ascent.
            float ascentProgress = Mathf.Clamp01(flightAge / AscentSeconds);
            // Freeze the arc at its midpoint while the target countdown runs.
            float progress = flightPhase == 1
                ? 0.5f * ascentProgress * ascentProgress
                : flightPhase == 2 ? 0.5f
                : 0.5f + 0.5f * Mathf.Clamp01((flightAge - descentStartsAt) / DescentSeconds);
            Vector3 position = Vector3.Lerp(origin, impactPosition, progress);
            position.y += 4f * arcHeightTiles * tileHeight * progress * (1f - progress);
            if (flightPhase == 3)
                position += Vector3.up * (0.5f * tileHeight);
            Move(flightPhase <= 2 ? ascend : descend, position);
            if (flightPhase == 1)
                UpdateAscentShadow(position);
        }

        private void UpdateAscentShadow(Vector3 position)
        {
            if (position.y - origin.y < 2f * tileHeight)
                return;
            if (!ascendShadow.gameObject.activeSelf)
            {
                Show(ascendShadow, true);
            }
            shadowFrameRemaining -= Time.deltaTime;
            if (shadowFrameRemaining <= 0f)
            {
                shadowFrameRemaining = Mathf.Max(0.01f, ascendShadow.animationTime);
                Vector2 offset = Random.insideUnitCircle * (0.35f * tileHeight);
                shadowJitter = new Vector3(offset.x, offset.y, 0f);
            }
            Move(ascendShadow, position + Vector3.down * (2f * tileHeight) + shadowJitter);
        }

        private void TriggerZubattoRocketEvasion()
        {
            Collider2D[] hits = Physics2D.OverlapBoxAll(impactPosition, Vector2.one * 0.5f, 0f);
            foreach (Collider2D hit in hits)
            {
                ZubattoMovementController zubatto = hit.GetComponentInParent<ZubattoMovementController>();
                if (zubatto != null && zubatto.TryEvadeRocketImpact(impactPosition))
                    return;
            }
        }

        private void ResolveRoom()
        {
            roomBounds = World3RoomProgressionController.FindRoomBoundsContaining(transform.position);
            if (roomBounds != null)
                return;
            // Decorative launchers can sit just outside the playable bounds.
            Tilemap ground = GameManager.Instance != null ? GameManager.Instance.groundTilemap : null;
            if (ground == null)
                return;
            float nearestDistance = float.PositiveInfinity;
            Vector3 nearest = transform.position;
            foreach (Vector3Int cell in ground.cellBounds.allPositionsWithin)
            {
                if (!ground.HasTile(cell))
                    continue;
                Vector3 center = ground.GetCellCenterWorld(cell);
                float distance = (center - transform.position).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = center;
                }
            }
            roomBounds = World3RoomProgressionController.FindRoomBoundsContaining(nearest);
        }

        private void ScheduleNextShot()
        {
            waitRemaining = Random.Range(Mathf.Max(0.1f, Mathf.Min(minimumRepeatSeconds, maximumRepeatSeconds)),
                Mathf.Max(0.1f, Mathf.Max(minimumRepeatSeconds, maximumRepeatSeconds)));
        }

        private void ResetVisuals()
        {
            audioSource.Stop();
            audioPaused = false;
            firing = false;
            Show(penguin, true);
            Show(launching, false);
            Show(reload, false);
            Show(ascend, false);
            Show(ascendShadow, false);
            Show(descend, false);
            Show(target, false);
            Show(explosion, false);
        }

        private static void Show(AnimatedSpriteRenderer visual, bool active) => visual.gameObject.SetActive(active);

        private static void Move(AnimatedSpriteRenderer visual, Vector3 position)
        {
            visual.SetExternalBaseLocalPosition(visual.transform.parent.InverseTransformPoint(position));
        }
    }
}
