using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class CoreMechanismsDestructible : Destructible
{
    public static event Action AllCoreMechanismsDestroyed;

    [SerializeField] private AnimatedSpriteRenderer animationRenderer;
    [SerializeField] private AnimatedSpriteRenderer deathRenderer;
    [SerializeField, Min(0.01f)] private float deathDurationSeconds = 0.5f;
    [SerializeField] private bool playDeathOnStart;
    [SerializeField] private AudioClip allDestroyedSfx;
    [SerializeField, Min(0f)] private float allDestroyedSfxVolume = 3f;

    private bool dying;
    static bool allDestroyedSfxPlayed;
    static ulong sceneHandleRaw = ulong.MaxValue;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticStateOnSubsystemRegistration()
    {
        AllCoreMechanismsDestroyed = null;
        allDestroyedSfxPlayed = false;
        sceneHandleRaw = ulong.MaxValue;
    }

    void Awake()
    {
        Scene currentScene = SceneManager.GetActiveScene();
        ulong currentSceneHandle = currentScene.handle.GetRawData();
        if (sceneHandleRaw != currentSceneHandle)
        {
            sceneHandleRaw = currentSceneHandle;
            allDestroyedSfxPlayed = false;
        }

        ResolveRenderers();
        ShowAlive();
    }

    private void Start()
    {
        if (playDeathOnStart)
            PlayDeath();
    }

    public void PlayDeath()
    {
        if (dying)
            return;

        dying = true;

        bool allDestroyed = !HasRemainingAliveCoreMechanisms();
        if (allDestroyed)
        {
            PlayAllDestroyedSfx();
            AllCoreMechanismsDestroyed?.Invoke();
        }

        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        SetColliders(false);
        SetRenderer(animationRenderer, false);

        if (deathRenderer != null)
        {
            deathRenderer.gameObject.SetActive(true);
            deathRenderer.enabled = true;
            deathRenderer.idle = false;
            deathRenderer.loop = false;
            deathRenderer.useSequenceDuration = true;
            deathRenderer.sequenceDuration = Mathf.Max(0.01f, deathDurationSeconds);
            deathRenderer.CurrentFrame = 0;
            deathRenderer.RestartAnimation();
        }

        yield return new WaitForSeconds(Mathf.Max(0.01f, deathDurationSeconds));
        Destroy(gameObject);
    }

    private void ShowAlive()
    {
        SetColliders(true);

        if (animationRenderer != null)
        {
            animationRenderer.gameObject.SetActive(true);
            animationRenderer.enabled = true;
            animationRenderer.idle = false;
            animationRenderer.loop = true;
        }

        SetRenderer(deathRenderer, false);
    }

    private void ResolveRenderers()
    {
        if (animationRenderer == null)
            animationRenderer = FindRenderer("Animation");

        if (deathRenderer == null)
            deathRenderer = FindRenderer("Death");
    }

    private AnimatedSpriteRenderer FindRenderer(string childName)
    {
        Transform child = transform.Find(childName);
        if (child != null && child.TryGetComponent(out AnimatedSpriteRenderer renderer))
            return renderer;

        return null;
    }

    private static void SetRenderer(AnimatedSpriteRenderer renderer, bool enabled)
    {
        if (renderer == null)
            return;

        renderer.enabled = enabled;

        if (renderer.TryGetComponent(out SpriteRenderer spriteRenderer))
            spriteRenderer.enabled = enabled;
    }

    private void SetColliders(bool enabled)
    {
        Collider2D[] colliders = GetComponents<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = enabled;
    }

    bool HasRemainingAliveCoreMechanisms()
    {
        CoreMechanismsDestructible[] mechanisms = FindObjectsByType<CoreMechanismsDestructible>(
            FindObjectsInactive.Exclude);

        int remaining = 0;
        for (int i = 0; i < mechanisms.Length; i++)
        {
            CoreMechanismsDestructible mechanism = mechanisms[i];
            if (mechanism == null || mechanism == this || mechanism.dying)
                continue;

            remaining++;
        }

        return remaining > 0;
    }

    void PlayAllDestroyedSfx()
    {
        if (allDestroyedSfxPlayed || allDestroyedSfx == null)
            return;

        allDestroyedSfxPlayed = true;

        GameObject temp = new("CoreMechanisms_AllDestroyedSfx");
        AudioSource source = temp.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;

        PlayBoostedSfx(source, allDestroyedSfx, allDestroyedSfxVolume);
        Destroy(temp, Mathf.Max(0.1f, allDestroyedSfx.length + 0.1f));
    }

    static void PlayBoostedSfx(AudioSource source, AudioClip clip, float volume)
    {
        if (source == null || clip == null)
            return;

        source.PlayOneShot(clip, GetBoostedSfxVolume(volume));
    }

    static float GetBoostedSfxVolume(float volume)
    {
        return Mathf.Max(0f, volume) * Mathf.Clamp01(GameAudioSettings.SfxVolume);
    }
}
