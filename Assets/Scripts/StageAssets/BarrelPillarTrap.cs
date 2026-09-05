using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StageAssets
{
    /// <summary>
    /// Bounces a barrel downward after both supporting pillars are destroyed
    /// by BombExplosion.Start. Intended for the Room 2 barrel in Stage 3-4.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BarrelPillarTrap : MonoBehaviour
    {
        [Header("Drop")]
        [SerializeField, Min(0.01f)] private float fallDistance = 13f;
        [SerializeField, Min(0.01f)] private float bounceIntervalSeconds = 0.7f;
        [SerializeField, Min(0.01f)] private float bounceDropDistance = 3f;
        [SerializeField, Min(0f)] private float bounceHeight = 0.75f;

        [Header("Audio")]
        [SerializeField] private AudioClip bounceSfx;

        [Header("Falling Visual")]
        [SerializeField] private Sprite fallingAlternateSprite;
        [SerializeField, Min(0.01f)] private float fallingSpriteFrameSeconds = 0.1f;

        [Header("Damage")]
        [SerializeField, Min(0.01f)] private float damageHalfWidth = 1.25f;
        [SerializeField, Min(0.01f)] private float damageHalfHeight = 0.75f;

        [Header("Player Crush")]
        [SerializeField, Min(0.01f)] private float playerStunSeconds = 2f;
        private readonly HashSet<MovementController> crushedPlayers = new();

        private readonly HashSet<BarrelPillarTrapPillar> destroyedPillars = new();
        private SpriteRenderer barrelRenderer;
        private Sprite fallingBaseSprite;
        private AudioSource audioSource;
        private bool fallStarted;

        public bool AreBothPillarsDestroyed => destroyedPillars.Count >= 2;

        private void Awake()
        {
            barrelRenderer = GetComponent<SpriteRenderer>();
            fallingBaseSprite = barrelRenderer != null ? barrelRenderer.sprite : null;
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;
            EnsurePillarReceiver("PillarBarrel1");
            EnsurePillarReceiver("PillarBarrel2");
        }

        private void EnsurePillarReceiver(string pillarName)
        {
            Transform pillar = transform.Find(pillarName);
            if (pillar == null)
                return;

            if (!pillar.TryGetComponent(out BarrelPillarTrapPillar _))
                pillar.gameObject.AddComponent<BarrelPillarTrapPillar>();
        }

        internal void NotifyPillarDestroyed(BarrelPillarTrapPillar pillar)
        {
            if (fallStarted)
                return;

            if (!destroyedPillars.Add(pillar))
                return;

            const int requiredPillars = 2;
            if (destroyedPillars.Count < requiredPillars)
                return;

            StartCoroutine(FallRoutine());
        }

        private IEnumerator FallRoutine()
        {
            fallStarted = true;

            if (barrelRenderer != null)
                barrelRenderer.sortingOrder = 10;

            Vector3 start = transform.position;
            Vector3 end = start + Vector3.down * fallDistance;
            float fallElapsedSeconds = 0f;
            float bounceElapsedSeconds = 0f;
            float interval = Mathf.Max(0.01f, bounceIntervalSeconds);
            float dropDistance = Mathf.Max(0.01f, bounceDropDistance);
            Vector3 bounceStart = start;
            Vector3 damagePosition = start;
            while (bounceStart.y > end.y)
            {
                Vector3 bounceEnd = new(start.x, Mathf.Max(end.y, bounceStart.y - dropDistance), start.z);
                GameAudioSettings.PlaySfx(audioSource, bounceSfx);

                while (true)
                {
                    float progress = Mathf.Clamp01(bounceElapsedSeconds / interval);
                    Vector3 previousDamagePosition = damagePosition;
                    damagePosition = Vector3.Lerp(bounceStart, bounceEnd, progress);
                    transform.position = damagePosition
                        + Vector3.up * (4f * bounceHeight * progress * (1f - progress));
                    AnimateFallingSprite(fallElapsedSeconds);

                    // Bounce height is visual only: the damage footprint stays on
                    // the ground path and sweeps continuously, including in midair.
                    DamageCharactersInSweep(previousDamagePosition, damagePosition);
                    DestroyCoreMechanismsInSweep(previousDamagePosition, damagePosition);
                    if (progress >= 1f)
                        break;

                    yield return null;
                    bounceElapsedSeconds += Time.deltaTime;
                    fallElapsedSeconds += Time.deltaTime;
                }

                bounceElapsedSeconds -= interval;
                bounceStart = bounceEnd;
            }

            Destroy(gameObject);
        }

        private void AnimateFallingSprite(float elapsedSeconds)
        {
            if (barrelRenderer == null || fallingBaseSprite == null || fallingAlternateSprite == null)
                return;

            int frame = Mathf.FloorToInt(elapsedSeconds / fallingSpriteFrameSeconds);
            barrelRenderer.sprite = (frame & 1) == 0 ? fallingBaseSprite : fallingAlternateSprite;
        }

        private void DestroyCoreMechanismsInSweep(Vector3 previous, Vector3 current)
        {
            float minY = Mathf.Min(previous.y, current.y) - damageHalfHeight;
            float maxY = Mathf.Max(previous.y, current.y) + damageHalfHeight;
            CoreMechanismsDestructible[] mechanisms = FindObjectsByType<CoreMechanismsDestructible>(
                FindObjectsInactive.Exclude);

            for (int i = 0; i < mechanisms.Length; i++)
            {
                CoreMechanismsDestructible mechanism = mechanisms[i];
                if (mechanism == null)
                    continue;

                Vector3 position = mechanism.transform.position;
                if (Mathf.Abs(position.x - current.x) > damageHalfWidth || position.y < minY || position.y > maxY)
                    continue;

                // Use the normal destruction flow for animation, audio and room
                // progression. PlayDeath ignores mechanisms already dying.
                mechanism.PlayDeath();
            }
        }

        private void DamageCharactersInSweep(Vector3 previous, Vector3 current)
        {
            float minY = Mathf.Min(previous.y, current.y) - damageHalfHeight;
            float maxY = Mathf.Max(previous.y, current.y) + damageHalfHeight;
            MovementController[] movementControllers = FindObjectsByType<MovementController>(FindObjectsInactive.Exclude);

            for (int i = 0; i < movementControllers.Length; i++)
            {
                MovementController movement = movementControllers[i];
                if (movement == null || movement.isDead)
                    continue;

                Vector2 position = movement.Rigidbody != null ? movement.Rigidbody.position : movement.transform.position;
                if (Mathf.Abs(position.x - current.x) > damageHalfWidth || position.y < minY || position.y > maxY)
                    continue;

                if (movement.CompareTag("Player"))
                {
                    if (crushedPlayers.Contains(movement))
                        continue;

                    StunReceiver stun = movement.GetComponent<StunReceiver>();
                    if (stun == null)
                        stun = movement.gameObject.AddComponent<StunReceiver>();
                    if (stun.TryCrushStun(playerStunSeconds))
                        crushedPlayers.Add(movement);
                    continue;
                }

                CharacterHealth health = movement.GetComponent<CharacterHealth>() ?? movement.GetComponentInParent<CharacterHealth>();
                if (health == null)
                    continue;

                // Retry while overlapping; CharacterHealth handles invulnerability.
                // A blocked hit must not exempt the character for the whole fall.
                health.TakeDamage(1, fromExplosion: true);
            }
        }
    }

    /// <summary>
    /// Local explosion target for a BarrelPillarTrap support. It plays the
    /// existing CoreMechanisms death animation without registering as a room
    /// core, then tells its barrel to fall.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BarrelPillarTrapPillar : MonoBehaviour, IExplosionDestructible
    {
        [SerializeField] private BarrelPillarTrap barrel;
        [SerializeField, Min(0.01f)] private float destructionDuration = 0.5f;

        private SpriteRenderer spriteRenderer;
        private BoxCollider2D blockingCollider;
        private bool destroyed;

        private void Awake()
        {
            barrel ??= GetComponentInParent<BarrelPillarTrap>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            blockingCollider = GetComponent<BoxCollider2D>();
            if (blockingCollider == null)
                blockingCollider = gameObject.AddComponent<BoxCollider2D>();

            // The support is an explosion target only. Players must be able to
            // cross the tile while it is still visible.
            blockingCollider.isTrigger = true;
            blockingCollider.size = spriteRenderer != null ? spriteRenderer.bounds.size : Vector2.one;

            int stageLayer = LayerMask.NameToLayer("Stage");
            if (stageLayer < 0)
                return;

            gameObject.layer = stageLayer;
        }

        public bool TryDestroyByExplosion(BombExplosion.ExplosionPart explosionPart)
        {
            if (explosionPart != BombExplosion.ExplosionPart.Start)
                return false;

            if (destroyed)
                return true;

            destroyed = true;
            if (blockingCollider != null)
                blockingCollider.enabled = false;
            if (spriteRenderer != null)
                spriteRenderer.enabled = false;

            PlayCoreMechanismsDeathVfx();
            if (barrel != null)
                barrel.NotifyPillarDestroyed(this);

            Destroy(gameObject, destructionDuration);
            return true;
        }

        private void PlayCoreMechanismsDeathVfx()
        {
            CoreMechanismsDestructible source = FindAnyObjectByType<CoreMechanismsDestructible>();
            Transform deathTransform = source != null ? source.transform.Find("Death") : null;
            if (deathTransform == null)
                return;

            GameObject deathVfx = Instantiate(deathTransform.gameObject, transform.position, Quaternion.identity);
            deathVfx.name = "Stage34BarrelPillar_CoreMechanismsDeath";
            deathVfx.SetActive(true);

            if (deathVfx.TryGetComponent(out AnimatedSpriteRenderer animation))
            {
                animation.enabled = true;
                animation.idle = false;
                animation.loop = false;
                animation.useSequenceDuration = true;
                animation.sequenceDuration = destructionDuration;
                animation.CurrentFrame = 0;
                animation.RestartAnimation();
            }

            Destroy(deathVfx, destructionDuration);
        }
    }
}
