using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterHealth), typeof(MovementControllerAI))]
public sealed class PrettyBomberDefeatSequence : MonoBehaviour
{
    [SerializeField] private AnimatedSpriteRenderer damagedAnimation = null;
    [SerializeField] private AudioClip endStageMusic = null;
    private CharacterHealth health;
    private MovementControllerAI movement;
    private bool started;
    private PrettyBomberMagicEffect magic;
    private void Awake()
    {
        health = GetComponent<CharacterHealth>();
        movement = GetComponent<MovementControllerAI>();
        if (endStageMusic != null)
            endStageMusic.LoadAudioData();
    }

    private void OnEnable()
    {
        health.Damaged += OnDamaged;
        health.CancelDeathRequest += CancelImmediateDeath;
    }

    private void OnDisable()
    {
        health.Damaged -= OnDamaged;
        health.CancelDeathRequest -= CancelImmediateDeath;
        StopAllCoroutines();
        if (magic != null)
            Destroy(magic.gameObject);
    }

    private bool CancelImmediateDeath() => started;

    private void OnDamaged(int amount)
    {
        // Bomber duels finish at the last life, matching MagnetBomber's duel.
        // Intercept lethal hits too, so they cannot bypass the outro.
        if (started || health.life > 1)
            return;
        started = true;
        var duel = FindAnyObjectByType<StageAssets.World3HallStageSevenSequence>();
        if (duel != null)
            duel.FinishDuel();
        StartCoroutine(DefeatRoutine());
    }

    private IEnumerator DefeatRoutine()
    {
        GetComponent<BrainIA>().enabled = false;
        GetComponent<BombController>().enabled = false;
        movement.SetAIDirection(Vector2.zero);
        movement.SetInputLocked(true, true);
        movement.SetExplosionInvulnerable(true);
        foreach (var collider in GetComponentsInChildren<Collider2D>())
            collider.enabled = false;

        // Allow TakeDamage to finish before taking ownership of the visuals.
        yield return null;
        health.SetExternalInvulnerability(true);
        movement.enabled = false;
        movement.SetVisualOverrideActive(true);
        ShowAnimation(damagedAnimation, true);
        yield return WaitUnpaused(health.hitInvulnerableDuration);

        var death = movement.spriteRendererDeath;
        death.useSequenceDuration = true;
        death.sequenceDuration = 0.5f;
        death.frameDurations = System.Array.Empty<float>();
        ShowAnimation(death, false);
        yield return WaitUnpaused(0.5f);
        death.SetFrozen(true);

        magic = PrettyBomberMagicEffect.Create(transform.position + Vector3.down * 0.5f, movement.tileSize,
            death.GetComponent<SpriteRenderer>());
        // Grow from a small pixel ellipse, then pull her through the floor.
        yield return magic.Grow(0.5f);
        yield return WaitUnpaused(0.5f);
        yield return magic.Reveal(gameObject, movement, false, 0.5f);
        // Hide the character before removing the clipping mask at frame end.
        death.enabled = false;
        Destroy(magic.gameObject);
        magic = null;
        yield return WaitUnpaused(1f);
        var gameManager = FindAnyObjectByType<GameManager>();
        if (gameManager != null)
            gameManager.nextStageSceneName = "Stage_3-8";
        var ending = gameObject.AddComponent<BossEndStageSequence>();
        ending.endStageMusic = endStageMusic;
        ending.completedStageSceneName = "Stage_3-7";
        ending.delayBeforeStart = 0f;
        ending.celebrationSeconds = 0f;
        ending.fadeDuration = 3f;
        ending.StartBossDefeatedSequence();
        gameObject.SetActive(false);
    }

    private void ShowAnimation(AnimatedSpriteRenderer animation, bool loop)
    {
        foreach (var renderer in GetComponentsInChildren<AnimatedSpriteRenderer>(true))
            renderer.enabled = false;
        foreach (var renderer in GetComponentsInChildren<SpriteRenderer>(true))
            renderer.enabled = false;
        animation.idle = false;
        animation.loop = loop;
        animation.pingPong = false;
        animation.SetFrozen(false);
        animation.enabled = true;
        animation.RefreshFrame();
    }

    private static IEnumerator WaitUnpaused(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            yield return null;
            if (!GamePauseController.IsPaused)
                elapsed += Time.deltaTime;
        }
    }

}
