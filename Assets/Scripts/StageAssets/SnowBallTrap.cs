using UnityEngine;
using UnityEngine.Tilemaps;

namespace StageAssets
{
    [DisallowMultipleComponent]
    public sealed class SnowBallTrap : MonoBehaviour
    {
        [SerializeField] private Tilemap supportTilemap;
        [SerializeField] private Collider2D roomBounds;
        [SerializeField] private Vector3Int[] supportCells =
        {
            new(62, 2, 0), new(63, 2, 0), new(62, 3, 0)
        };
        [SerializeField] private GameObject idleVisual;
        [SerializeField] private AnimatedSpriteRenderer rollingVisual;
        [SerializeField] private AudioClip rollingSfx;
        [SerializeField, Min(1f)] private float rollingSfxGain = 3f;
        [SerializeField, Min(0.01f)] private float speed = 4f;
        [SerializeField, Min(0.01f)] private float fallDistance = 16f;
        [SerializeField] private Vector2 damageHalfSize = new(0.9f, 0.75f);

        private SpriteRenderer rollingRenderer;
        private AudioSource audioSource;
        private AudioClip boostedRollingSfx;
        private bool rolling;
        private bool audioPaused;
        private float endY;

        private void Awake()
        {
            GameObject audioObject = new("SnowBall_RollingSfx");
            audioObject.transform.SetParent(transform, false);
            audioSource = audioObject.AddComponent<AudioSource>();
            if (rollingVisual != null) rollingRenderer = rollingVisual.GetComponent<SpriteRenderer>();
            audioSource.playOnAwake = false;
            audioSource.loop = true;
            audioSource.spatialBlend = 0f;
            if (idleVisual != null) idleVisual.SetActive(true);
            if (rollingVisual != null) rollingVisual.gameObject.SetActive(false);
        }

        private void Start()
        {
            if (roomBounds == null)
                roomBounds = World3RoomProgressionController.FindRoomBounds("Room 3");
            if (supportTilemap != null) return;
            GameManager manager = FindAnyObjectByType<GameManager>();
            if (manager != null) supportTilemap = manager.destructibleTilemap;
        }

        private void Update()
        {
            bool paused = GamePauseController.IsPaused || Time.timeScale <= 0f;
            if (rolling)
            {
                audioSource.volume = GameAudioSettings.ApplySfxVolume(1f);
                if (paused && !audioPaused) audioSource.Pause();
                if (!paused && audioPaused) audioSource.UnPause();
                audioPaused = paused;
            }
            if (paused) return;

            if (!rolling)
            {
                if (supportTilemap == null || supportCells == null || supportCells.Length == 0) return;
                foreach (Vector3Int cell in supportCells)
                    if (supportTilemap.HasTile(cell)) return;

                rolling = true;
                endY = transform.position.y - fallDistance;
                if (idleVisual != null) idleVisual.SetActive(false);
                if (rollingVisual != null)
                {
                    rollingVisual.gameObject.SetActive(true);
                    rollingVisual.enabled = true;
                    rollingVisual.idle = false;
                    rollingVisual.loop = true;
                    rollingVisual.RestartAnimation();
                }
                GameAudioSettings.PlaySfxClip(audioSource, GetRollingClip());
            }

            Vector3 previous = transform.position;
            Vector3 current = previous;
            current.y = roomBounds != null
                ? previous.y - speed * Time.deltaTime
                : Mathf.MoveTowards(previous.y, endY, speed * Time.deltaTime);
            transform.position = current;
            ApplySweep(previous, current);
            float topY = rollingRenderer != null
                ? rollingRenderer.bounds.max.y : current.y + damageHalfSize.y;
            if (roomBounds != null ? topY < roomBounds.bounds.min.y : current.y <= endY)
                FinishRolling();
        }

        private bool IsInSweep(Vector3 position, Vector3 previous, Vector3 current)
        {
            return Mathf.Abs(position.x - current.x) <= damageHalfSize.x &&
                position.y >= current.y - damageHalfSize.y &&
                position.y <= previous.y + damageHalfSize.y;
        }

        private void ApplySweep(Vector3 previous, Vector3 current)
        {
            // Query the entire segment so a slow frame cannot skip a target.
            foreach (CharacterHealth health in FindObjectsByType<CharacterHealth>())
            {
                if (!health.isActiveAndEnabled || !IsInSweep(health.transform.position, previous, current)) continue;
                MovementController movement = health.GetComponent<MovementController>();
                if (movement != null)
                {
                    if (movement.isDead || movement.IsEndingStage || health.IsInvulnerable) continue;
                    if (movement.TryGetComponent(out PlayerMountCompanion companion))
                    {
                        if (movement.IsRidingPlaying())
                        {
                            companion.HandleDamageWhileMounting(1);
                            continue;
                        }
                        if (movement.IsMounted)
                        {
                            CharacterHealth mountHealth = companion.GetMountedLouieHealth();
                            if (mountHealth == null || !mountHealth.IsInvulnerable)
                                companion.OnMountedLouieHit(1, false);
                            continue;
                        }
                    }
                }
                health.TakeDamage(1);
            }
            foreach (CoreMechanismsDestructible core in FindObjectsByType<CoreMechanismsDestructible>())
                if (IsInSweep(core.transform.position, previous, current)) core.PlayDeath();
        }

        public void CancelRollIfRunning()
        {
            if (rolling) FinishRolling();
        }

        private void FinishRolling()
        {
            // Keep the same source and playback position; disabling looping lets
            // the current iteration finish naturally after the snowball is gone.
            if (audioSource != null)
            {
                audioSource.transform.SetParent(null, true);
                audioSource.loop = false;
                SnowBallSfxTail tail = audioSource.gameObject.AddComponent<SnowBallSfxTail>();
                tail.Initialize(audioSource, boostedRollingSfx, audioPaused);
                audioSource = null;
                boostedRollingSfx = null;
            }
            rolling = false;
            Destroy(gameObject);
        }

        private AudioClip GetRollingClip()
        {
            if (boostedRollingSfx != null) return boostedRollingSfx;
            if (rollingSfx == null || rollingSfxGain <= 1f) return rollingSfx;

            // The source is imported as preloaded PCM. Boost samples instead of
            // AudioSource.volume, which Unity limits to 1, preserving SFX volume.
            float[] samples = new float[rollingSfx.samples * rollingSfx.channels];
            if (!rollingSfx.GetData(samples, 0)) return rollingSfx;

            float peak = 0f;
            foreach (float sample in samples) peak = Mathf.Max(peak, Mathf.Abs(sample));
            if (peak <= 0f) return rollingSfx;

            float gain = Mathf.Min(rollingSfxGain, 0.98f / peak);
            if (gain <= 1f) return rollingSfx;
            for (int i = 0; i < samples.Length; i++) samples[i] *= gain;

            boostedRollingSfx = AudioClip.Create("SnowBall_Boosted", rollingSfx.samples,
                rollingSfx.channels, rollingSfx.frequency, false);
            boostedRollingSfx.SetData(samples, 0);
            return boostedRollingSfx;
        }

        private void OnDestroy()
        {
            if (boostedRollingSfx != null) Destroy(boostedRollingSfx);
        }

        private void OnDisable()
        {
            if (audioSource != null) audioSource.Stop();
        }
    }
}