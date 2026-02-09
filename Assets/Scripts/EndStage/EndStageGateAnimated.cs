using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(AudioSource))]
public sealed class EndStageGateAnimated : EndStage
{
    [Header("SFX - Unlock Sequence")]
    [SerializeField] private AudioClip unlockGateSfx;
    [SerializeField] private AudioClip openGateSfx;

    [Header("Unlock Sequence Timing")]
    [SerializeField, Min(0f)] private float unlockSfxDelay = 1f;
    [SerializeField, Min(0f)] private float openSfxDelayAfterUnlock = 1f;

    [Header("Gate Visuals")]
    [SerializeField] private GameObject closedRoot;
    [SerializeField] private GameObject openRoot;

    [Header("Open Animation")]
    [SerializeField] private AnimatedSpriteRenderer openAnimatedSprite;
    [SerializeField, Min(0.01f)] private float openAnimSeconds = 0.5f;

    [Header("Colliders")]
    [SerializeField] private BoxCollider2D blockingCollider;
    [SerializeField] private BoxCollider2D finishTrigger;

    [Header("Finish Trigger Shape")]
    [SerializeField] private Vector2 finishTriggerSize = new(0.1f, 0.1f);

    [Header("Finish Trigger Position")]
    [SerializeField] private Tilemap finishTriggerTilemap;
    [SerializeField] private Vector3Int finishTriggerCell;

    [Header("Fallback Offset")]
    [SerializeField] private Vector2 finishTriggerOffset = Vector2.zero;

    [Header("Visual Fix")]
    [SerializeField] private bool snapOpenToClosedOnUnlock = true;

    AudioSource audioSource;
    Coroutine openRoutine;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();

        if (blockingCollider != null)
        {
            blockingCollider.enabled = true;
            blockingCollider.isTrigger = false;
        }

        if (finishTrigger != null)
        {
            finishTrigger.isTrigger = true;
            finishTrigger.enabled = false;
            finishTrigger.size = finishTriggerSize;
            finishTrigger.offset = finishTriggerOffset;
        }

        if (openRoot != null)
            openRoot.SetActive(false);
    }

    protected override void OnUnlocked()
    {
        if (openRoutine != null)
            StopCoroutine(openRoutine);

        openRoutine = StartCoroutine(OpenRoutine());
    }

    IEnumerator OpenRoutine()
    {
        if (unlockSfxDelay > 0f)
            yield return new WaitForSeconds(unlockSfxDelay);

        if (unlockGateSfx != null && audioSource != null)
            audioSource.PlayOneShot(unlockGateSfx);

        if (openSfxDelayAfterUnlock > 0f)
            yield return new WaitForSeconds(openSfxDelayAfterUnlock);

        if (openGateSfx != null && audioSource != null)
            audioSource.PlayOneShot(openGateSfx);

        if (snapOpenToClosedOnUnlock && openRoot != null && closedRoot != null)
        {
            openRoot.transform.position = closedRoot.transform.position;
            openRoot.transform.rotation = closedRoot.transform.rotation;
            openRoot.transform.localScale = closedRoot.transform.localScale;
        }

        if (closedRoot != null) closedRoot.SetActive(false);
        if (openRoot != null) openRoot.SetActive(true);

        if (openAnimatedSprite != null)
        {
            openAnimatedSprite.enabled = false;
            openAnimatedSprite.enabled = true;
        }

        if (openAnimSeconds > 0f)
            yield return new WaitForSeconds(openAnimSeconds);

        if (blockingCollider != null)
            blockingCollider.enabled = false;

        PositionFinishTrigger();

        if (finishTrigger != null)
            finishTrigger.enabled = true;

        openRoutine = null;
    }

    void PositionFinishTrigger()
    {
        if (finishTrigger == null)
            return;

        if (finishTriggerTilemap != null)
        {
            var wc = finishTriggerTilemap.GetCellCenterWorld(finishTriggerCell);
            finishTrigger.offset = new Vector2(
                wc.x - transform.position.x,
                wc.y - transform.position.y
            );
            return;
        }

        finishTrigger.offset = finishTriggerOffset;
    }

    protected override Vector2 GetPortalCenterWorld(Collider2D triggeredBy)
    {
        if (finishTriggerTilemap != null)
        {
            var wc = finishTriggerTilemap.GetCellCenterWorld(finishTriggerCell);
            return new Vector2(Mathf.Round(wc.x), Mathf.Round(wc.y));
        }

        if (finishTrigger != null)
        {
            var c = finishTrigger.bounds.center;
            return new Vector2(Mathf.Round(c.x), Mathf.Round(c.y));
        }

        return base.GetPortalCenterWorld(triggeredBy);
    }
}
