using System.Collections.Generic;
using UnityEngine;

namespace StageAssets
{
    [DisallowMultipleComponent]
    public sealed class SteamController : MonoBehaviour
    {
        private const float FrameSeconds = 0.1f;
        private const float SteamStart = 2 * FrameSeconds;
        private const float SteamEnd = 7 * FrameSeconds;
        private const float CoreDuration = 8 * FrameSeconds;

        [SerializeField] private AnimatedSpriteRenderer core;
        [SerializeField] private AnimatedSpriteRenderer steamRight;
        [SerializeField] private AnimatedSpriteRenderer steamUp;
        [SerializeField, Min(0.1f)] private float minimumInterval = 10f;
        [SerializeField, Min(0.1f)] private float maximumInterval = 20f;
        [SerializeField, Min(0.01f)] private float tileSize = 1f;
        [SerializeField, Min(0.01f)] private float stunSeconds = 0.5f;
        [SerializeField] private AudioClip steamSfx;
        [SerializeField, Range(0f, 1f)] private float steamSfxVolume = 1f;

        private AudioSource steamAudio;
        private bool audioPaused;
        private Collider2D roomBounds;
        private bool roomActive;

        private readonly HashSet<MovementController> hitPlayers = new();
        private AnimatedSpriteRenderer firstJet;
        private AnimatedSpriteRenderer secondJet;
        private Vector2 direction;
        private float remainingInterval;
        private float elapsed;
        private bool cycling;
        private bool emitting;

        private void Awake()
        {
            steamAudio = gameObject.AddComponent<AudioSource>();
            steamAudio.playOnAwake = false;
            steamAudio.loop = true;
            steamAudio.spatialBlend = 0f;
            steamAudio.clip = steamSfx;
            if (core == null || steamRight == null || steamUp == null)
            {
                enabled = false;
                return;
            }

            core.SetManualAnimationUpdate(true);
            steamRight.gameObject.SetActive(false);
            steamUp.gameObject.SetActive(false);
            ResetCore();
        }

        private void OnEnable()
        {
            roomBounds = World3RoomProgressionController.FindRoomBoundsContaining(transform.position);
            roomActive = false;
        }

        private void OnDisable() => StopSteam();

        private void StopSteam()
        {
            roomActive = false;
            StopJets();
            ResetCore();
            cycling = false;
            hitPlayers.Clear();
        }

        private void Update()
        {
            if (roomBounds == null)
                roomBounds = World3RoomProgressionController.FindRoomBoundsContaining(transform.position);

            if (roomBounds == null || !World3RoomProgressionController.IsRoomOccupied(roomBounds))
            {
                if (roomActive)
                    StopSteam();
                return;
            }

            if (!roomActive)
            {
                roomActive = true;
                ScheduleNext();
            }

            bool paused = GamePauseController.IsPaused || Time.deltaTime <= 0f ||
                (StageIntroTransition.Instance != null &&
                 (StageIntroTransition.Instance.IntroRunning || StageIntroTransition.Instance.EndingRunning));
            if (emitting && steamAudio != null)
            {
                steamAudio.volume = GameAudioSettings.ApplySfxVolume(steamSfxVolume);
                if (paused && !audioPaused)
                    steamAudio.Pause();
                else if (!paused && audioPaused)
                    steamAudio.UnPause();
                audioPaused = paused;
            }
            if (paused)
                return;

            if (!cycling)
            {
                remainingInterval -= Time.deltaTime;
                if (remainingInterval > 0f)
                    return;

                cycling = true;
                elapsed = 0f;
                hitPlayers.Clear();
                direction = Random.value < 0.5f ? Vector2.right : Vector2.up;
                core.idle = false;
                core.RestartAnimation();
                return;
            }

            elapsed += Time.deltaTime;
            core.CurrentFrame = Mathf.Min(7, Mathf.FloorToInt(elapsed / FrameSeconds));
            core.RefreshFrame();

            if (elapsed >= SteamStart && elapsed < SteamEnd)
            {
                if (!emitting)
                    StartJets();
                UpdateJet(firstJet);
                UpdateJet(secondJet);
                StunAdjacentPlayers();
            }
            else if (emitting)
            {
                StopJets();
            }

            if (elapsed >= CoreDuration)
            {
                ResetCore();
                cycling = false;
                ScheduleNext();
            }
        }

        private void StartJets()
        {
            AnimatedSpriteRenderer template = direction == Vector2.right ? steamRight : steamUp;
            firstJet = CreateJet(template, direction, false);
            secondJet = CreateJet(template, -direction, true);
            emitting = true;
            if (steamAudio != null && steamSfx != null)
            {
                steamAudio.volume = GameAudioSettings.ApplySfxVolume(steamSfxVolume);
                steamAudio.Play();
            }
        }

        private AnimatedSpriteRenderer CreateJet(AnimatedSpriteRenderer template, Vector2 offset, bool flip)
        {
            AnimatedSpriteRenderer jet = Instantiate(template, transform);
            jet.transform.position = transform.position + (Vector3)(offset * tileSize);
            jet.SetManualAnimationUpdate(true);
            jet.idle = false;
            jet.loop = false;
            jet.gameObject.SetActive(true);
            jet.enabled = true;
            SpriteRenderer sprite = jet.GetComponent<SpriteRenderer>();
            sprite.flipX = flip && direction == Vector2.right;
            sprite.flipY = flip && direction == Vector2.up;
            jet.RestartAnimation();
            return jet;
        }

        private void UpdateJet(AnimatedSpriteRenderer jet)
        {
            // Preserve the authored blank frames and fit the whole sequence into 0.5s.
            int count = jet.animationSprite.Length;
            jet.CurrentFrame = Mathf.Min(count - 1,
                Mathf.FloorToInt((elapsed - SteamStart) / (SteamEnd - SteamStart) * count));
            jet.RefreshFrame();
        }

        private void StunAdjacentPlayers()
        {
            foreach (MovementController player in FindObjectsByType<MovementController>())
            {
                if (!player.CompareTag("Player") || player.isDead || hitPlayers.Contains(player))
                    continue;

                Vector2 position = player.Rigidbody != null ? player.Rigidbody.position : (Vector2)player.transform.position;
                Vector2 collisionOrigin = transform.position;
                if (direction == Vector2.up)
                    collisionOrigin += Vector2.down * (tileSize * 0.5f);
                Vector2 relative = (position - collisionOrigin) / tileSize;
                Vector2Int cell = new(Mathf.FloorToInt(relative.x + 0.5f), Mathf.FloorToInt(relative.y + 0.5f));
                Vector2Int target = Vector2Int.RoundToInt(direction);
                if (cell != target && cell != -target)
                    continue;

                if (!player.TryGetComponent<StunReceiver>(out var receiver))
                {
                    hitPlayers.Add(player);
                    continue;
                }

                if (receiver.IsStunned || !receiver.CanReceiveStun)
                    continue;

                receiver.Stun(stunSeconds);
                if (receiver.IsStunned)
                {
                    hitPlayers.Add(player);
                }
            }
        }

        private void StopJets()
        {
            if (steamAudio != null)
                steamAudio.Stop();
            audioPaused = false;
            if (firstJet != null)
            {
                firstJet.gameObject.SetActive(false);
                Destroy(firstJet.gameObject);
            }
            if (secondJet != null)
            {
                secondJet.gameObject.SetActive(false);
                Destroy(secondJet.gameObject);
            }
            firstJet = null;
            secondJet = null;
            emitting = false;
        }

        private void ResetCore()
        {
            if (core == null)
                return;
            core.idle = true;
            core.RestartAnimation();
        }

        private void ScheduleNext()
        {
            remainingInterval = Random.Range(Mathf.Max(0.1f, minimumInterval), Mathf.Max(minimumInterval, maximumInterval));
        }

    }
}
