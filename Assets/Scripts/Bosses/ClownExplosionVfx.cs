using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class ClownExplosionVfx : MonoBehaviour
{
    public Sprite[] frames;
    public float duration = 0.1f;

    SpriteRenderer sr;
    float timer;
    int index;
    float frameTime;
    bool started;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr == null)
            sr = GetComponentInChildren<SpriteRenderer>(true);
    }

    void OnEnable()
    {
        if (frames == null || frames.Length == 0)
        {
            if (!TryGetComponent<AnimatedSpriteRenderer>(out var anim))
                anim = GetComponentInChildren<AnimatedSpriteRenderer>(true);

            if (anim != null && anim.animationSprite != null && anim.animationSprite.Length > 0)
                frames = anim.animationSprite;
        }

        timer = 0f;
        index = 0;
        started = true;

        if (sr == null || frames == null || frames.Length == 0)
        {
            Destroy(gameObject);
            return;
        }

        frameTime = duration / frames.Length;
        if (frameTime <= 0f) frameTime = 0.01f;

        sr.enabled = true;
        sr.sprite = frames[0];
    }

    void Update()
    {
        if (!started) return;

        timer += Time.deltaTime;

        while (timer >= frameTime)
        {
            timer -= frameTime;
            index++;

            if (index >= frames.Length)
            {
                Destroy(gameObject);
                return;
            }

            sr.sprite = frames[index];
        }
    }
}
