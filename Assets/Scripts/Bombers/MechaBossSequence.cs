using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

public class MechaBossSequence : MonoBehaviour
{
    private static readonly WaitForSeconds _waitForSeconds1 = new(1f);
    private static readonly WaitForSeconds _waitForSeconds2 = new(2f);

    public MovementController whiteMecha;
    public MovementController blackMecha;
    public MovementController redMecha;

    public MovementController player;

    [Header("Music")]
    public AudioClip bossCheeringMusic;

    [Header("Gate")]
    public Tilemap indestructibleTilemap;
    public Vector3Int gateCell;
    public float gateStepDelay = 0.1f;

    MovementController[] mechas;
    GameManager gameManager;
    BombController playerBomb;

    bool initialized;
    bool sequenceStarted;
    bool finalSequenceStarted;

    TileBase gateCenterTile;
    TileBase gateLeftTile;
    TileBase gateRightTile;

    Vector3Int gateLeftCell;
    Vector3Int gateRightCell;

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

        if (indestructibleTilemap != null)
        {
            gateCenterTile = indestructibleTilemap.GetTile(gateCell);

            gateLeftCell = new Vector3Int(gateCell.x - 1, gateCell.y, gateCell.z);
            gateRightCell = new Vector3Int(gateCell.x + 1, gateCell.y, gateCell.z);

            gateLeftTile = indestructibleTilemap.GetTile(gateLeftCell);
            gateRightTile = indestructibleTilemap.GetTile(gateRightCell);
        }
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

        yield return StartCoroutine(OpenGateRoutine());

        mecha.SetExplosionInvulnerable(true);

        var bossAI = mecha.GetComponent<BossBomberAI>();
        var aiMove = mecha.GetComponent<AIMovementController>();

        if (bossAI != null) bossAI.enabled = false;
        if (aiMove != null) aiMove.enabled = true;

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

        mecha.gameObject.SetActive(true);

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

        mecha.SetExplosionInvulnerable(false);

        yield return StartCoroutine(CloseGateRoutine());

        LockPlayer(false);
    }

    IEnumerator OpenGateRoutine()
    {
        if (indestructibleTilemap == null)
            yield break;

        indestructibleTilemap.SetTile(gateCell, null);

        if (gateStepDelay > 0f)
            yield return new WaitForSeconds(gateStepDelay);

        indestructibleTilemap.SetTile(gateLeftCell, null);
        indestructibleTilemap.SetTile(gateRightCell, null);
    }

    IEnumerator CloseGateRoutine()
    {
        if (indestructibleTilemap == null)
            yield break;

        indestructibleTilemap.SetTile(gateLeftCell, gateLeftTile);
        indestructibleTilemap.SetTile(gateRightCell, gateRightTile);

        if (gateStepDelay > 0f)
            yield return new WaitForSeconds(gateStepDelay);

        indestructibleTilemap.SetTile(gateCell, gateCenterTile);
    }

    void LockPlayer(bool locked)
    {
        if (player != null)
        {
            player.SetInputLocked(locked);
            player.SetExplosionInvulnerable(locked);
        }

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

        if (sender == redMecha && !finalSequenceStarted)
        {
            finalSequenceStarted = true;
            StartCoroutine(FinalBossDefeatedRoutine());
            return;
        }

        if (gameManager != null)
            gameManager.CheckWinState();
    }

    IEnumerator FinalBossDefeatedRoutine()
    {
        yield return _waitForSeconds1;

        if (player == null || player.isDead)
            yield break;

        player.StartCheering();

        if (GameMusicController.Instance != null && bossCheeringMusic != null)
            GameMusicController.Instance.PlayMusic(bossCheeringMusic, 1f, false);

        float cheeringDuration = 4f;
        float fadeDuration = 1f;
        float timeBeforeFade = Mathf.Max(0f, cheeringDuration - fadeDuration);

        if (timeBeforeFade > 0f)
            yield return new WaitForSeconds(timeBeforeFade);

        if (StageIntroTransition.Instance != null)
            StageIntroTransition.Instance.StartFadeOut(fadeDuration);

        if (fadeDuration > 0f)
            yield return new WaitForSeconds(fadeDuration);

        if (gameManager != null)
            gameManager.EndStage();
    }
}
