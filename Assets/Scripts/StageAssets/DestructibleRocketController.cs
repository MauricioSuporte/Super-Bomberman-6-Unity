using UnityEngine;

namespace StageAssets
{
    public sealed class DestructibleRocketController : MonoBehaviour
    {
        private const float AscentSeconds = 1f;
        private const float WarningSeconds = 5f;
        private const float DescentSeconds = 1f;
        private const float ExplosionSeconds = 0.25f;

        private const float TargetFrameSeconds = 0.1f;

        [Header("Renderers")]
        [SerializeField] private SpriteRenderer rocket;
        [SerializeField] private SpriteRenderer target;

        [Header("Rocket 2 - Animations")]
        [Tooltip("Rocket 2_2 through Rocket 2_5")]
        [SerializeField] private Sprite[] ascentFrames;

        [Tooltip("Rocket 2_7 through Rocket 2_10")]
        [SerializeField] private Sprite[] descentFrames;

        [Tooltip("Rocket 2_11 through Rocket 2_16")]
        [SerializeField] private Sprite[] explosionFrames;

        [Header("Rocket Target - Animation")]
        [Tooltip("Rocket Target_0 and Rocket Target_1")]
        [SerializeField] private Sprite[] targetFrames;

        [Header("Animation Settings")]
        [Min(1f)]
        [SerializeField] private float flightAnimationFps = 10f;

        [Header("Audio")]
        [SerializeField] private AudioClip launchSfx;
        [SerializeField] private AudioClip descendSfx;
        [SerializeField] private AudioClip explosionSfx;

        private AudioSource audioSource;
        private Collider2D room;
        private Vector3 origin;
        private Vector3 impact;
        private float tileHeight;
        private float age;
        private int phase;
        private bool launched;
        private bool audioPaused;

        public void Launch(
            Vector3 destination,
            float height,
            Collider2D roomBounds)
        {
            if (!Assets.Scripts.Netcode.NetSync.ShouldSimulateLocally)
                return;

            origin = transform.position;
            impact = destination;
            tileHeight = height;
            room = roomBounds;

            audioSource = GetComponent<AudioSource>();

            age = 0f;
            phase = 0;
            audioPaused = GamePauseController.IsPaused;

            rocket.enabled = true;
            target.enabled = false;

            SetLoopFrame(ascentFrames, 0f);
            SetTargetFrame(0f);

            launched = true;

            Assets.Scripts.Netcode.BombermanNetworkManager
                .CoopTrackRenderer(rocket);

            Assets.Scripts.Netcode.BombermanNetworkManager
                .CoopTrackRenderer(target);

            GameAudioSettings.PlaySfx(audioSource, launchSfx);

            if (audioPaused && audioSource != null)
                audioSource.Pause();
        }

        private void Update()
        {
            if (!Assets.Scripts.Netcode.NetSync.ShouldSimulateLocally)
                return;

            if (!launched)
                return;

            bool isPaused = GamePauseController.IsPaused;

            if (audioPaused != isPaused)
            {
                audioPaused = isPaused;

                if (audioSource != null)
                {
                    if (audioPaused)
                        audioSource.Pause();
                    else
                        audioSource.UnPause();
                }
            }

            if (isPaused)
                return;

            if (room == null ||
                !World3RoomProgressionController.IsRoomOccupied(room))
            {
                Destroy(gameObject);
                return;
            }

            age += Time.deltaTime;

            float descentStart =
                AscentSeconds + WarningSeconds - DescentSeconds;

            float impactTime =
                AscentSeconds + WarningSeconds;

            // Phase 0 -> 1: ascent finished.
            if (age >= AscentSeconds && phase == 0)
            {
                phase = 1;

                rocket.enabled = false;

                target.transform.position = impact;

                // Start target animation from frame 0.
                SetTargetFrame(0f);

                target.enabled = true;
            }

            // Phase 1 -> 2: begin descent.
            if (age >= descentStart && phase == 1)
            {
                phase = 2;

                rocket.enabled = true;

                SetLoopFrame(descentFrames, 0f);

                if (audioSource != null)
                    audioSource.Stop();

                GameAudioSettings.PlaySfx(
                    audioSource,
                    descendSfx);
            }

            // Phase 2 -> 3: impact.
            if (age >= impactTime && phase == 2)
            {
                phase = 3;

                target.enabled = false;

                rocket.transform.position =
                    impact + Vector3.up * (0.25f * tileHeight);

                if (audioSource != null)
                    audioSource.Stop();

                GameAudioSettings.PlaySfx(
                    audioSource,
                    explosionSfx,
                    2f);

                // Resolve damage at impact.
                BombController damageSource =
                    FindAnyObjectByType<BombController>();

                if (damageSource != null)
                {
                    damageSource.SpawnSingleTileExplosionDamageForEffect(
                        impact,
                        ExplosionSeconds);
                }
            }

            // Animate target while visible.
            // Alternates frames every 0.1 seconds.
            if (target != null && target.enabled)
            {
                float targetElapsed = age - AscentSeconds;

                SetTargetFrame(targetElapsed);
            }

            // Phase 3: explosion animation, played once.
            if (phase == 3)
            {
                float explosionAge = age - impactTime;

                if (explosionAge >= ExplosionSeconds)
                {
                    Destroy(gameObject);
                    return;
                }

                SetExplosionFrame(explosionAge);
                return;
            }

            // Phase 0: ascending.
            if (phase == 0)
            {
                float progress =
                    Mathf.Clamp01(age / AscentSeconds);

                rocket.transform.position =
                    origin +
                    Vector3.up *
                    ((0.25f + 10f * progress * progress)
                    * tileHeight);

                SetLoopFrame(ascentFrames, age);
            }
            // Phase 2: descending.
            else if (phase == 2)
            {
                float progress = Mathf.Clamp01(
                    (age - descentStart) / DescentSeconds);

                rocket.transform.position =
                    impact +
                    Vector3.up *
                    ((0.25f + 10f * (1f - progress))
                    * tileHeight);

                SetLoopFrame(
                    descentFrames,
                    age - descentStart);
            }
        }

        // Repeats all rocket frames continuously.
        private void SetLoopFrame(Sprite[] frames, float elapsed)
        {
            if (rocket == null ||
                frames == null ||
                frames.Length == 0)
                return;

            float fps = Mathf.Max(1f, flightAnimationFps);

            int index =
                Mathf.FloorToInt(elapsed * fps) % frames.Length;

            if (frames[index] != null)
                rocket.sprite = frames[index];
        }

        // Alternates target sprites every 0.1 seconds.
        private void SetTargetFrame(float elapsed)
        {
            if (target == null ||
                targetFrames == null ||
                targetFrames.Length == 0)
                return;

            int index =
                Mathf.FloorToInt(
                    Mathf.Max(0f, elapsed) / TargetFrameSeconds
                ) % targetFrames.Length;

            if (targetFrames[index] != null)
                target.sprite = targetFrames[index];
        }

        // Plays all explosion frames once over 0.5 seconds.
        private void SetExplosionFrame(float elapsed)
        {
            if (rocket == null ||
                explosionFrames == null ||
                explosionFrames.Length == 0)
                return;

            float normalizedTime =
                Mathf.Clamp01(elapsed / ExplosionSeconds);

            int index = Mathf.Min(
                explosionFrames.Length - 1,
                Mathf.FloorToInt(
                    normalizedTime * explosionFrames.Length));

            if (explosionFrames[index] != null)
                rocket.sprite = explosionFrames[index];
        }
    }
}