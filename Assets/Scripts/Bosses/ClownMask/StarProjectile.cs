using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class StarProjectile : MonoBehaviour
{
    public float speed = 6f;
    public float lifeTime = 3f;
    public int damage = 1;
    public LayerMask obstacleMask;

    Rigidbody2D rb;
    Vector2 direction = Vector2.zero;
    float curvedAngularSpeedDegrees;
    bool initialized;
    bool orbiting;
    Transform orbitCenter;
    float orbitRadius;
    float orbitTargetRadius;
    float orbitFirstRadius;
    float orbitLinkSpacing;
    float orbitAngleDegrees;
    float orbitAngularSpeedDegrees;
    float orbitElapsed;
    float orbitMovementDelay;
    float orbitDeployRetractDuration;
    float orbitLinkTransitionDuration;
    float orbitBirthDelay;
    int orbitLinkIndex;
    bool orbitVisible = true;
    SpriteRenderer[] orbitVisuals;
    Collider2D[] orbitColliders;
    Coroutine lifeRoutine;

    public void Initialize(Vector2 dir, float customSpeed, float customLifetime)
    {
        direction = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;

        if (customSpeed > 0f)
            speed = customSpeed;

        if (customLifetime > 0f)
            lifeTime = customLifetime;

        initialized = true;
        RestartLifetime();
    }

    public void InitializeCurved(Vector2 dir, float customSpeed, float customLifetime, float angularSpeedDegrees)
    {
        curvedAngularSpeedDegrees = angularSpeedDegrees;
        Initialize(dir, customSpeed, customLifetime);
    }

    public void InitializeOrbit(
        Transform center,
        float radius,
        float initialAngleDegrees,
        float angularSpeedDegrees,
        float customLifetime,
        float orbitStartDelay,
        float firstRadius,
        float linkSpacing,
        int linkIndex,
        int linksPerArm,
        float deployRetractDuration)
    {
        orbiting = true;
        orbitCenter = center;
        orbitRadius = 0f;
        orbitTargetRadius = Mathf.Max(0f, radius);
        orbitFirstRadius = Mathf.Max(0f, firstRadius);
        orbitLinkSpacing = Mathf.Max(0f, linkSpacing);
        orbitAngleDegrees = initialAngleDegrees;
        orbitAngularSpeedDegrees = angularSpeedDegrees;
        orbitElapsed = 0f;
        orbitMovementDelay = Mathf.Max(0f, orbitStartDelay);
        orbitLinkIndex = Mathf.Max(0, linkIndex);
        int safeLinksPerArm = Mathf.Max(1, linksPerArm);
        orbitDeployRetractDuration = Mathf.Max(0.01f, deployRetractDuration);
        orbitLinkTransitionDuration = orbitDeployRetractDuration / safeLinksPerArm;
        orbitBirthDelay = orbitLinkTransitionDuration * (safeLinksPerArm - 1 - orbitLinkIndex);

        if (customLifetime > 0f)
            lifeTime = customLifetime;

        initialized = true;
        UpdateOrbitRadiusAndVisibility();
        UpdateOrbitPosition();
        RestartLifetime();
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        orbitVisuals = GetComponentsInChildren<SpriteRenderer>(true);
        orbitColliders = GetComponentsInChildren<Collider2D>(true);
        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
        }
    }

    void OnEnable()
    {
        RestartLifetime();
    }

    void OnDisable()
    {
        if (lifeRoutine != null)
        {
            StopCoroutine(lifeRoutine);
            lifeRoutine = null;
        }
    }

    void Update()
    {
        if (!initialized)
            return;

        if (orbiting)
        {
            if (orbitCenter == null)
            {
                Destroy(gameObject);
                return;
            }

            orbitElapsed += Time.deltaTime;

            if (orbitElapsed >= orbitMovementDelay)
                orbitAngleDegrees += orbitAngularSpeedDegrees * Time.deltaTime;

            UpdateOrbitRadiusAndVisibility();
            UpdateOrbitPosition();
            return;
        }

        if (Mathf.Abs(curvedAngularSpeedDegrees) > 0.001f)
            direction = Quaternion.Euler(0f, 0f, curvedAngularSpeedDegrees * Time.deltaTime) * direction;

        transform.position += (Vector3)(direction * speed * Time.deltaTime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        int layer = other.gameObject.layer;

        if (layer == LayerMask.NameToLayer("Player") || other.CompareTag("Player"))
        {
            if (TryApplyPlayerHit(other))
            {
                if (!orbiting)
                    Destroy(gameObject);
            }

            return;
        }

        if (!orbiting && ((1 << layer) & obstacleMask.value) != 0)
            Destroy(gameObject);
    }

    void RestartLifetime()
    {
        if (!isActiveAndEnabled || lifeTime <= 0f)
            return;

        if (lifeRoutine != null)
            StopCoroutine(lifeRoutine);

        lifeRoutine = StartCoroutine(LifetimeRoutine(lifeTime));
    }

    IEnumerator LifetimeRoutine(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        lifeRoutine = null;
        Destroy(gameObject);
    }

    void UpdateOrbitPosition()
    {
        if (orbitCenter == null)
            return;

        float radians = orbitAngleDegrees * Mathf.Deg2Rad;
        Vector2 radial = new(Mathf.Cos(radians), Mathf.Sin(radians));
        transform.position = (Vector2)orbitCenter.position + radial * orbitRadius;
    }

    void UpdateOrbitRadiusAndVisibility()
    {
        if (orbitElapsed < orbitBirthDelay)
        {
            SetOrbitVisible(false);
            return;
        }

        float retractStart = Mathf.Max(orbitDeployRetractDuration, lifeTime - orbitDeployRetractDuration);
        if (orbitElapsed < retractStart)
        {
            SetOrbitVisible(true);

            if (orbitElapsed >= orbitDeployRetractDuration)
            {
                orbitRadius = orbitTargetRadius;
                return;
            }

            float deploySteps = Mathf.Max(
                (orbitElapsed - orbitBirthDelay) / orbitLinkTransitionDuration,
                0f);
            if (deploySteps <= 1f)
            {
                orbitRadius = Mathf.Lerp(0f, orbitFirstRadius, deploySteps);
                return;
            }

            float outwardSteps = Mathf.Clamp(deploySteps - 1f, 0f, orbitLinkIndex);
            orbitRadius = orbitFirstRadius + orbitLinkSpacing * outwardSteps;
            return;
        }

        float retractSteps = (orbitElapsed - retractStart) / orbitLinkTransitionDuration;
        if (retractSteps <= orbitLinkIndex)
        {
            SetOrbitVisible(true);
            orbitRadius = orbitFirstRadius + orbitLinkSpacing * (orbitLinkIndex - retractSteps);
            return;
        }

        float enterBossT = retractSteps - orbitLinkIndex;
        if (enterBossT >= 1f)
        {
            orbitRadius = 0f;
            SetOrbitVisible(false);
            return;
        }

        SetOrbitVisible(true);
        orbitRadius = Mathf.Lerp(orbitFirstRadius, 0f, enterBossT);
    }

    void SetOrbitVisible(bool visible)
    {
        if (orbitVisible == visible)
            return;

        orbitVisible = visible;

        for (int i = 0; i < orbitVisuals.Length; i++)
        {
            if (orbitVisuals[i] != null)
                orbitVisuals[i].enabled = visible;
        }

        for (int i = 0; i < orbitColliders.Length; i++)
        {
            if (orbitColliders[i] != null)
                orbitColliders[i].enabled = visible;
        }
    }

    bool TryApplyPlayerHit(Collider2D other)
    {
        if (!other.TryGetComponent<MovementController>(out var movement) || movement == null)
            return false;

        if (movement.isDead || movement.IsEndingStage)
            return false;

        CharacterHealth playerHealth = null;
        if (movement.TryGetComponent<CharacterHealth>(out var h) && h != null)
            playerHealth = h;

        if (playerHealth != null && playerHealth.IsInvulnerable)
            return false;

        if (movement.IsMounted)
        {
            if (movement.TryGetComponent<PlayerMountCompanion>(out var companion) && companion != null)
            {
                companion.OnMountedLouieHit(damage, false);
                return true;
            }

            if (playerHealth != null)
            {
                playerHealth.TakeDamage(damage);
                return true;
            }

            movement.Kill();
            return true;
        }

        if (playerHealth != null)
        {
            playerHealth.TakeDamage(damage);
            return true;
        }

        movement.Kill();
        return true;
    }
}
