using UnityEngine;

public class PinkLouieShadowAnimator : MonoBehaviour
{
    public AnimatedSpriteRenderer walkShadow;
    public AnimatedSpriteRenderer jumpShadow;

    [Header("Walk Sprites")]
    public Sprite walkIdleSmall;
    public Sprite walkMoveMedium;

    [Header("Jump Sprite")]
    public Sprite jumpLarge;

    [Header("Walk Timing")]
    public float walkFrameSeconds = 0.1f;

    [Header("Debug")]
    [SerializeField] private bool enableSurgicalLogs = true;

    AnimatedSpriteRenderer active;

    SpriteRenderer walkSr;
    SpriteRenderer jumpSr;
    SpriteRenderer activeSr;

    bool moving;
    bool lastMoving;

    float walkTimer;
    int walkFrameIndex = -1;

    void Awake()
    {
        CacheRefs();

        LogShadowAnim(
            $"Awake | walkShadow={(walkShadow != null ? walkShadow.name : "<null>")} " +
            $"jumpShadow={(jumpShadow != null ? jumpShadow.name : "<null>")} " +
            $"walkSr={(walkSr != null ? walkSr.name : "<null>")} " +
            $"jumpSr={(jumpSr != null ? jumpSr.name : "<null>")}");

        RestoreDefaultVisualState();
    }

    void OnEnable()
    {
        CacheRefs();
        LogShadowAnim("OnEnable | restoring default visual state");
        RestoreDefaultVisualState();
    }

    void Update()
    {
        if (active != walkShadow)
            return;

        if (walkSr == null)
            return;

        if (!moving)
            return;

        float ft = Mathf.Max(0.01f, walkFrameSeconds);
        walkTimer += Time.deltaTime;

        int newFrame = (int)(walkTimer / ft) % 4;
        if (newFrame == walkFrameIndex)
            return;

        walkFrameIndex = newFrame;

        Sprite s = (walkFrameIndex == 2) ? walkMoveMedium : walkIdleSmall;
        if (s != null)
            walkSr.sprite = s;
    }

    void OnDisable()
    {
        LogShadowAnim("OnDisable | ForceOff");
        ForceOff();
    }

    void OnDestroy()
    {
        LogShadowAnim("OnDestroy | ForceOff");
        ForceOff();
    }

    public void SetJumping(bool jumping)
    {
        LogShadowAnim($"SetJumping | jumping={jumping}");

        if (jumping)
        {
            SetExclusive(jumpShadow);

            if (jumpSr != null && walkSr != null)
                jumpSr.transform.localPosition = walkSr.transform.localPosition;

            if (activeSr != null && jumpLarge != null)
                activeSr.sprite = jumpLarge;

            if (active != null)
            {
                active.idle = true;
                active.loop = false;
                active.RefreshFrame();
            }

            return;
        }

        SetExclusive(walkShadow);
        ApplyIdleSmall();
        ForceRefreshWalkRenderer();
        SetMoving(false);
    }

    public void SetMoving(bool isMoving)
    {
        LogShadowAnim($"SetMoving | isMoving={isMoving} lastMoving={lastMoving}");

        moving = isMoving;

        if (active != walkShadow)
            return;

        walkTimer = 0f;
        walkFrameIndex = -1;

        if (!moving)
        {
            ApplyIdleSmall();
            ForceRefreshWalkRenderer();
        }
        else
        {
            ApplyIdleSmall();
            ForceRefreshWalkRenderer();
        }

        lastMoving = moving;
    }

    void CacheRefs()
    {
        if (walkShadow != null && walkSr == null)
            walkShadow.TryGetComponent(out walkSr);

        if (jumpShadow != null && jumpSr == null)
            jumpShadow.TryGetComponent(out jumpSr);
    }

    void RestoreDefaultVisualState()
    {
        moving = false;
        lastMoving = false;
        walkTimer = 0f;
        walkFrameIndex = -1;

        SetExclusive(walkShadow);
        ApplyIdleSmall();
        ForceRefreshWalkRenderer();
    }

    void ForceRefreshWalkRenderer()
    {
        if (walkShadow != null)
        {
            walkShadow.enabled = true;
            walkShadow.idle = true;
            walkShadow.loop = false;
            walkShadow.RefreshFrame();
        }

        if (walkSr != null)
            walkSr.enabled = true;
    }

    void ApplyIdleSmall()
    {
        if (walkSr != null && walkIdleSmall != null)
            walkSr.sprite = walkIdleSmall;
    }

    void SetExclusive(AnimatedSpriteRenderer keep)
    {
        LogShadowAnim($"SetExclusive | keep={(keep != null ? keep.name : "<null>")}");

        if (keep == null)
        {
            ForceOff();
            return;
        }

        SetRendererEnabled(walkShadow, keep == walkShadow);
        SetRendererEnabled(jumpShadow, keep == jumpShadow);

        active = keep;

        if (active.TryGetComponent<SpriteRenderer>(out var sr))
        {
            sr.enabled = true;
            activeSr = sr;
        }
        else
        {
            activeSr = null;
        }

        active.enabled = true;
    }

    void SetRendererEnabled(AnimatedSpriteRenderer r, bool on)
    {
        if (r == null)
            return;

        r.enabled = on;

        if (r.TryGetComponent<SpriteRenderer>(out var sr))
            sr.enabled = on;
    }

    void ForceOff()
    {
        SetRendererEnabled(walkShadow, false);
        SetRendererEnabled(jumpShadow, false);

        active = null;
        activeSr = null;

        moving = false;
        lastMoving = false;
        walkTimer = 0f;
        walkFrameIndex = -1;
    }

    void LogShadowAnim(string message)
    {
        if (!enableSurgicalLogs)
            return;

        string who = gameObject != null ? gameObject.name : "<null>";
        Debug.Log($"[PinkLouieShadowAnimator][{who}] {message}", this);
    }
}