using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlueLouiePunchAnimator : MonoBehaviour, IBombPunchExternalAnimator
{
    [Header("Punch Sprites (AnimatedSpriteRenderer)")]
    public AnimatedSpriteRenderer punchUp;
    public AnimatedSpriteRenderer punchDown;
    public AnimatedSpriteRenderer punchLeft;
    public AnimatedSpriteRenderer punchRight;

    AnimatedSpriteRenderer activePunch;
    bool playing;

    readonly List<GameObject> movementGOs = new();
    readonly Dictionary<GameObject, bool> movementPrevActive = new();

    void Awake()
    {
        CacheMovementObjects();
        DisableAllPunch();
    }

    public IEnumerator Play(Vector2 dir, float punchLockTime)
    {
        ForceStop();

        playing = true;

        HideMovementSprites();

        var target = GetPunchSprite(dir);
        if (target != null)
        {
            DisableAllPunch();

            activePunch = target;

            if (activePunch.TryGetComponent<SpriteRenderer>(out var sr))
                sr.flipX = (dir == Vector2.right);

            activePunch.gameObject.SetActive(true);
            activePunch.enabled = true;

            activePunch.idle = false;
            activePunch.loop = false;
            activePunch.CurrentFrame = 0;
            activePunch.RefreshFrame();
        }

        yield return new WaitForSeconds(punchLockTime);

        ForceStop();
    }

    public void ForceStop()
    {
        if (!playing && activePunch == null)
        {
            RestoreMovementSprites();
            DisableAllPunch();
            return;
        }

        playing = false;

        DisableAllPunch();
        activePunch = null;

        RestoreMovementSprites();
    }

    AnimatedSpriteRenderer GetPunchSprite(Vector2 dir)
    {
        if (dir == Vector2.up) return punchUp;
        if (dir == Vector2.down) return punchDown;
        if (dir == Vector2.left) return punchLeft;
        if (dir == Vector2.right) return punchRight;
        return punchDown;
    }

    void DisableAllPunch()
    {
        DisablePunch(punchUp);
        DisablePunch(punchDown);
        DisablePunch(punchLeft);
        DisablePunch(punchRight);
    }

    static void DisablePunch(AnimatedSpriteRenderer anim)
    {
        if (anim == null) return;
        anim.enabled = false;
        if (anim.gameObject.activeSelf)
            anim.gameObject.SetActive(false);
    }

    void CacheMovementObjects()
    {
        movementGOs.Clear();
        movementPrevActive.Clear();

        var allAnim = GetComponentsInChildren<AnimatedSpriteRenderer>(true);

        for (int i = 0; i < allAnim.Length; i++)
        {
            var a = allAnim[i];
            if (a == null) continue;

            if (a == punchUp || a == punchDown || a == punchLeft || a == punchRight)
                continue;

            var go = a.gameObject;
            if (!movementPrevActive.ContainsKey(go))
            {
                movementGOs.Add(go);
                movementPrevActive.Add(go, go.activeSelf);
            }
        }

        var allSR = GetComponentsInChildren<SpriteRenderer>(true);

        for (int i = 0; i < allSR.Length; i++)
        {
            var sr = allSR[i];
            if (sr == null) continue;

            if (IsPunchSpriteRenderer(sr))
                continue;

            var go = sr.gameObject;
            if (!movementPrevActive.ContainsKey(go))
            {
                movementGOs.Add(go);
                movementPrevActive.Add(go, go.activeSelf);
            }
        }
    }

    bool IsPunchSpriteRenderer(SpriteRenderer sr)
    {
        if (sr == null) return false;

        if (punchUp != null && sr.gameObject == punchUp.gameObject) return true;
        if (punchDown != null && sr.gameObject == punchDown.gameObject) return true;
        if (punchLeft != null && sr.gameObject == punchLeft.gameObject) return true;
        if (punchRight != null && sr.gameObject == punchRight.gameObject) return true;

        return false;
    }

    void HideMovementSprites()
    {
        movementPrevActive.Clear();

        for (int i = 0; i < movementGOs.Count; i++)
        {
            var go = movementGOs[i];
            if (go == null) continue;

            movementPrevActive[go] = go.activeSelf;

            if (go.activeSelf)
                go.SetActive(false);
        }
    }

    void RestoreMovementSprites()
    {
        foreach (var kv in movementPrevActive)
        {
            var go = kv.Key;
            if (go == null) continue;

            var shouldBeActive = kv.Value;
            if (go.activeSelf != shouldBeActive)
                go.SetActive(shouldBeActive);
        }
    }
}
