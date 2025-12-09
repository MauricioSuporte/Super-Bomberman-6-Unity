using System;
using UnityEngine;

public class MechaBossSequence : MonoBehaviour
{
    public MovementController whiteMecha;
    public MovementController blackMecha;
    public MovementController redMecha;

    MovementController[] mechas;
    GameManager gameManager;

    void Awake()
    {
        mechas = new[] { whiteMecha, blackMecha, redMecha };
        gameManager = FindFirstObjectByType<GameManager>();

        foreach (var m in mechas)
        {
            if (m == null)
                continue;

            m.Died += OnMechaDied;
        }
    }

    void Start()
    {
        for (int i = 0; i < mechas.Length; i++)
        {
            if (mechas[i] == null)
                continue;

            mechas[i].gameObject.SetActive(false);
        }

        ActivateMecha(0);
    }

    void ActivateMecha(int index)
    {
        if (index < 0 || index >= mechas.Length)
            return;

        var m = mechas[index];
        if (m == null)
            return;

        m.gameObject.SetActive(true);

        if (m.Rigidbody != null)
        {
            m.Rigidbody.simulated = true;
            m.Rigidbody.linearVelocity = Vector2.zero;
        }
    }

    void OnMechaDied(MovementController sender)
    {
        int currentIndex = Array.IndexOf(mechas, sender);
        int nextIndex = currentIndex + 1;

        if (nextIndex < mechas.Length && mechas[nextIndex] != null)
        {
            ActivateMecha(nextIndex);
            return;
        }

        if (gameManager != null)
            gameManager.CheckWinState();
    }
}
