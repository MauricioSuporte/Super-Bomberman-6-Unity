using UnityEngine;
using System.Collections;

/// <summary>Keeps the title cursor's usual eyes selected until the menu is idle.</summary>
public sealed class TitleScreenCursorEyeIdle : MonoBehaviour
{
    const string ResetCompleteEyeName = "TitleScreenCursor_Eye_Row3_1";
    [SerializeField] AnimatedSpriteRenderer cursorRenderer;
    [SerializeField] Sprite defaultEye;
    [SerializeField] Sprite briefEye;
    [SerializeField] Sprite[] alternateEyes;
    [SerializeField] Sprite resetCompleteEye;
    [SerializeField, Min(0.01f)] float idleDelay = 3f;
    [SerializeField, Min(0.01f)] float alternateDuration = 0.5f;
    [SerializeField, Min(0.01f)] float briefEyeDuration = 0.1f;

    float idleElapsed;
    float alternateElapsed;
    bool showingAlternate;
    Coroutine temporaryEyeRoutine;

    void OnEnable() => ShowDefaultEye();

    void Update()
    {
        if (cursorRenderer == null)
            return;

        if (temporaryEyeRoutine != null)
            return;

        if (showingAlternate)
        {
            idleElapsed += Time.unscaledDeltaTime;
            alternateElapsed += Time.unscaledDeltaTime;
            if (alternateElapsed >= (cursorRenderer.idleSprite == briefEye ? briefEyeDuration : alternateDuration))
                StopAlternateAndShowDefault();
            return;
        }

        idleElapsed += Time.unscaledDeltaTime;
        if (idleElapsed >= idleDelay)
            ShowRandomAlternate();
    }

    public void NotifyInput()
    {
        bool wasShowingTemporaryEye = temporaryEyeRoutine != null;
        CancelTemporaryEye();

        if (wasShowingTemporaryEye)
            ShowDefaultEye();
    }

    public void ShowResetCompleteEye()
    {
        if (resetCompleteEye == null)
            resetCompleteEye = FindAlternateEyeByName(ResetCompleteEyeName);

        if (cursorRenderer == null || resetCompleteEye == null)
            return;

        CancelTemporaryEye();
        showingAlternate = false;
        alternateElapsed = 0f;
        cursorRenderer.idleSprite = resetCompleteEye;
        cursorRenderer.RefreshFrame();
        temporaryEyeRoutine = StartCoroutine(ShowTemporaryEyeForSeconds(0.5f));
    }

    Sprite FindAlternateEyeByName(string spriteName)
    {
        if (alternateEyes == null)
            return null;

        for (int i = 0; i < alternateEyes.Length; i++)
        {
            Sprite eye = alternateEyes[i];
            if (eye != null && eye.name == spriteName)
                return eye;
        }

        return null;
    }

    IEnumerator ShowTemporaryEyeForSeconds(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        temporaryEyeRoutine = null;
        ShowDefaultEye();
    }

    void CancelTemporaryEye()
    {
        if (temporaryEyeRoutine == null)
            return;

        StopCoroutine(temporaryEyeRoutine);
        temporaryEyeRoutine = null;
    }

    void ShowRandomAlternate()
    {
        int regularEyeCount = alternateEyes == null ? 0 : alternateEyes.Length;
        if (regularEyeCount == 0 && briefEye == null)
        {
            idleElapsed = 0f;
            ShowDefaultEye();
            return;
        }

        bool chooseBriefEye = briefEye != null &&
                              (regularEyeCount == 0 || Random.Range(0, regularEyeCount + 1) == regularEyeCount);
        Sprite eye = chooseBriefEye ? briefEye : alternateEyes[Random.Range(0, regularEyeCount)];
        if (eye == null)
        {
            idleElapsed = 0f;
            ShowDefaultEye();
            return;
        }

        cursorRenderer.idleSprite = eye;
        cursorRenderer.RefreshFrame();
        showingAlternate = true;
        alternateElapsed = 0f;
        idleElapsed = 0f;
    }

    void StopAlternateAndShowDefault()
    {
        alternateElapsed = 0f;
        showingAlternate = false;
        ShowDefaultEye();
    }

    void ShowDefaultEye()
    {
        if (cursorRenderer == null || defaultEye == null) return;
        cursorRenderer.idleSprite = defaultEye;
        cursorRenderer.RefreshFrame();
    }
}
