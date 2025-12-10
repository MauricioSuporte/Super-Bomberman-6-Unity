using System;
using System.Collections;
using UnityEngine;

public class MechaBossSequence : MonoBehaviour
{
    private static readonly WaitForSeconds _waitForSeconds2 = new(2f);
    public MovementController whiteMecha;
    public MovementController blackMecha;
    public MovementController redMecha;

    public MovementController player;

    MovementController[] mechas;
    GameManager gameManager;
    BombController playerBomb;

    bool initialized;
    bool sequenceStarted;

    void Awake()
    {
        mechas = new[] { whiteMecha, blackMecha, redMecha };
        gameManager = FindFirstObjectByType<GameManager>();

        foreach (var m in mechas)
        {
            if (m == null) continue;
            m.Died += OnMechaDied;
        }

        if (player == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) player = go.GetComponent<MovementController>();
        }

        if (player != null)
            playerBomb = player.GetComponent<BombController>();
    }

    void Start()
    {
        initialized = true;

        LockPlayer(true);

        for (int i = 0; i < mechas.Length; i++)
            if (mechas[i] != null)
                mechas[i].gameObject.SetActive(false);
    }

    void OnEnable()
    {
        if (initialized && !sequenceStarted)
        {
            sequenceStarted = true;
            StartCoroutine(SpawnFirstMechaAfterStageStart());
        }
    }

    IEnumerator SpawnFirstMechaAfterStageStart()
    {
        if (StageIntroTransition.Instance != null)
            while (StageIntroTransition.Instance.IntroRunning)
                yield return null;

        while (GamePauseController.IsPaused)
            yield return null;

        LockPlayer(true);

        yield return _waitForSeconds2;

        StartMechaIntro(0);
    }

    void StartMechaIntro(int index)
    {
        if (index < 0 || index >= mechas.Length) return;
        if (mechas[index] == null) return;

        StartCoroutine(MechaIntroRoutine(mechas[index]));
    }

    IEnumerator MechaIntroRoutine(MovementController mecha)
    {
        LockPlayer(true);

        var bossAI = mecha.GetComponent<BossBomberAI>();
        var aiMove = mecha.GetComponent<AIMovementController>();

        if (bossAI != null) bossAI.enabled = false;
        if (aiMove != null) aiMove.enabled = true;

        mecha.gameObject.SetActive(true);

        Vector2 startPos = new(-1f, 5f);
        Vector2 endPos = new(-1f, 0f);

        if (mecha.Rigidbody != null)
        {
            mecha.Rigidbody.simulated = true;
            mecha.Rigidbody.linearVelocity = Vector2.zero;
            mecha.Rigidbody.position = startPos;
        }
        else
        {
            mecha.transform.position = startPos;
        }

        if (aiMove != null)
            aiMove.SetAIDirection(Vector2.down);

        while (true)
        {
            if (mecha == null) yield break;

            Vector2 pos = mecha.Rigidbody != null
                ? mecha.Rigidbody.position
                : (Vector2)mecha.transform.position;

            if (pos.y <= endPos.y + 0.05f)
                break;

            if (aiMove != null)
                aiMove.SetAIDirection(Vector2.down);

            yield return null;
        }

        if (mecha.Rigidbody != null)
        {
            mecha.Rigidbody.position = endPos;
            mecha.Rigidbody.linearVelocity = Vector2.zero;
        }
        else
        {
            mecha.transform.position = endPos;
        }

        if (aiMove != null)
            aiMove.SetAIDirection(Vector2.zero);

        yield return _waitForSeconds2;

        if (bossAI != null) bossAI.enabled = true;

        LockPlayer(false);
    }

    void LockPlayer(bool locked)
    {
        if (player != null)
            player.SetInputLocked(locked);

        if (playerBomb != null)
            playerBomb.enabled = !locked;
    }

    void OnMechaDied(MovementController sender)
    {
        int currentIndex = Array.IndexOf(mechas, sender);
        int nextIndex = currentIndex + 1;

        if (nextIndex < mechas.Length && mechas[nextIndex] != null)
        {
            StartMechaIntro(nextIndex);
            return;
        }

        if (gameManager != null)
            gameManager.CheckWinState();
    }
}
