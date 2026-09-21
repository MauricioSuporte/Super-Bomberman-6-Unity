using UnityEngine;
using UnityEngine.Tilemaps;

namespace StageAssets
{
    [DisallowMultipleComponent]
    public sealed class IgluRoofController : MonoBehaviour, IExplosionDestructible
    {
        [SerializeField] private Vector2 explosionOffset = new(-0.5f, -1f);
        [SerializeField, Min(0.01f)] private float jumpHeightTiles = 8f;
        [SerializeField, Min(0.01f)] private float bounceHeightTiles = 1f;
        [SerializeField, Min(0.01f)] private float gravityTilesPerSecondSquared = 16f;
        [SerializeField] private AudioClip landingSfx;
        [SerializeField, Min(1f)] private float landingSfxGain = 3f;

        private AudioSource audioSource;
        private AudioClip boostedLandingSfx;
        private BoxCollider2D explosionTarget;
        private Vector3 restingPosition;
        private float tileHeight;
        private float elapsed;
        private bool jumping;
        private bool bouncing;
        private bool audioPaused;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;

            // Match the existing Stage explosion-target contract without blocking movement.
            explosionTarget = gameObject.AddComponent<BoxCollider2D>();
            explosionTarget.isTrigger = true;
            explosionTarget.offset = explosionOffset;
            explosionTarget.size = Vector2.one * 0.4f;
            int stageLayer = LayerMask.NameToLayer("Stage");
            if (stageLayer >= 0)
                gameObject.layer = stageLayer;
        }

        private void OnEnable()
        {
            restingPosition = transform.position;
            explosionTarget.enabled = true;
        }

        public bool TryDestroyByExplosion(BombExplosion.ExplosionPart explosionPart)
        {
            if (!isActiveAndEnabled || jumping || GamePauseController.IsPaused ||
                explosionPart != BombExplosion.ExplosionPart.Start)
                return false;

            restingPosition = transform.position;
            Tilemap ground = GameManager.Instance != null ? GameManager.Instance.groundTilemap : null;
            tileHeight = ground != null
                ? Vector3.Distance(ground.GetCellCenterWorld(Vector3Int.zero),
                    ground.GetCellCenterWorld(Vector3Int.up))
                : 1f;
            tileHeight = Mathf.Max(0.01f, tileHeight);
            elapsed = 0f;
            bouncing = false;
            jumping = true;
            explosionTarget.enabled = false;
            return true;
        }

        private void Update()
        {
            bool paused = GamePauseController.IsPaused;
            if (audioPaused != paused)
            {
                audioPaused = paused;
                if (paused)
                    audioSource.Pause();
                else
                    audioSource.UnPause();
            }
            if (paused || !jumping)
                return;

            elapsed += Time.deltaTime;
            float gravity = Mathf.Max(0.01f, gravityTilesPerSecondSquared) * tileHeight;
            float height = Mathf.Max(0.01f, jumpHeightTiles) * tileHeight;
            float duration = 2f * Mathf.Sqrt(2f * height / gravity);
            if (!bouncing && elapsed >= duration)
            {
                elapsed -= duration;
                bouncing = true;
                transform.position = restingPosition;
                GameAudioSettings.PlaySfx(audioSource, GetLandingClip());
            }

            if (bouncing)
            {
                height = Mathf.Max(0.01f, bounceHeightTiles) * tileHeight;
                duration = 2f * Mathf.Sqrt(2f * height / gravity);
                if (elapsed >= duration)
                {
                    transform.position = restingPosition;
                    jumping = false;
                    explosionTarget.enabled = true;
                    return;
                }
            }

            // A ballistic arc slows to rest at the apex and accelerates on descent.
            float progress = Mathf.Clamp01(elapsed / duration);
            transform.position = restingPosition + Vector3.up * (4f * height * progress * (1f - progress));
        }

        private AudioClip GetLandingClip()
        {
            if (boostedLandingSfx != null) return boostedLandingSfx;
            if (landingSfx == null || landingSfxGain <= 1f) return landingSfx;

            // Match SnowBall: amplify PCM samples while keeping headroom and global SFX volume.
            float[] samples = new float[landingSfx.samples * landingSfx.channels];
            if (!landingSfx.GetData(samples, 0)) return landingSfx;

            float peak = 0f;
            foreach (float sample in samples) peak = Mathf.Max(peak, Mathf.Abs(sample));
            if (peak <= 0f) return landingSfx;

            float gain = Mathf.Min(landingSfxGain, 0.98f / peak);
            if (gain <= 1f) return landingSfx;
            for (int i = 0; i < samples.Length; i++) samples[i] *= gain;

            boostedLandingSfx = AudioClip.Create("IgluRoof_Boosted", landingSfx.samples,
                landingSfx.channels, landingSfx.frequency, false);
            boostedLandingSfx.SetData(samples, 0);
            return boostedLandingSfx;
        }

        private void OnDestroy()
        {
            if (boostedLandingSfx != null) Destroy(boostedLandingSfx);
        }

        private void OnDisable()
        {
            transform.position = restingPosition;
            jumping = false;
            bouncing = false;
            explosionTarget.enabled = false;
            audioSource.Stop();
            audioPaused = false;
        }
    }
}
