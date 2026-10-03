using UnityEngine;

namespace StageAssets
{
    public sealed class DestructibleRocketController : MonoBehaviour
    {
        private const float AscentSeconds = 1f;
        private const float WarningSeconds = 5f;
        private const float DescentSeconds = 1f;
        private const float ExplosionSeconds = 0.5f;

        [SerializeField] private SpriteRenderer rocket;
        [SerializeField] private Sprite ascentSprite;
        [SerializeField] private Sprite descentSprite;
        [SerializeField] private Sprite[] explosionFrames;
        [SerializeField] private SpriteRenderer target;
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

        public void Launch(Vector3 destination, float height, Collider2D roomBounds)
        {
            origin = transform.position;
            impact = destination;
            tileHeight = height;
            room = roomBounds;
            audioSource = GetComponent<AudioSource>();
            rocket.sprite = ascentSprite;
            target.enabled = false;
            launched = true;
            GameAudioSettings.PlaySfx(audioSource, launchSfx);
        }

        private void Update()
        {
            if (!launched)
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
            if (room == null || !World3RoomProgressionController.IsRoomOccupied(room))
            {
                Destroy(gameObject);
                return;
            }

            age += Time.deltaTime;
            float descentStart = AscentSeconds + WarningSeconds - DescentSeconds;
            float impactTime = AscentSeconds + WarningSeconds;
            if (age >= AscentSeconds && phase == 0)
            {
                phase = 1;
                rocket.enabled = false;
                target.transform.position = impact;
                target.enabled = true;
            }
            if (age >= descentStart && phase == 1)
            {
                phase = 2;
                rocket.sprite = descentSprite;
                rocket.enabled = true;
                audioSource.Stop();
                GameAudioSettings.PlaySfx(audioSource, descendSfx);
            }
            if (age >= impactTime && phase == 2)
            {
                phase = 3;
                target.enabled = false;
                rocket.transform.position = impact + Vector3.up * (0.25f * tileHeight);
                audioSource.Stop();
                audioSource.PlayOneShot(explosionSfx, 2f * GameAudioSettings.ApplySfxVolume(1f));
                // Resolve at impact: the player who launched it may have died during flight.
                BombController damageSource = FindAnyObjectByType<BombController>();
                if (damageSource != null)
                    damageSource.SpawnSingleTileExplosionDamageForEffect(impact, ExplosionSeconds);
            }
            if (phase == 3)
            {
                float explosionAge = age - impactTime;
                if (explosionAge >= ExplosionSeconds)
                {
                    Destroy(gameObject);
                    return;
                }
                rocket.sprite = explosionFrames[Mathf.Min(4, Mathf.FloorToInt(explosionAge / 0.1f))];
                return;
            }
            if (phase == 0)
            {
                float progress = Mathf.Clamp01(age / AscentSeconds);
                rocket.transform.position = origin + Vector3.up * ((0.25f + 10f * progress * progress) * tileHeight);
            }
            else if (phase == 2)
            {
                float progress = Mathf.Clamp01((age - descentStart) / DescentSeconds);
                rocket.transform.position = impact + Vector3.up * ((0.25f + 10f * (1f - progress)) * tileHeight);
            }
        }
    }
}
