using UnityEngine;
using UnityEngine.SceneManagement;
using System;

public class GameManager : MonoBehaviour
{
    public GameObject[] players;

    public int EnemiesAlive { get; private set; }

    public event Action OnAllEnemiesDefeated;

    private void Start()
    {
        EnemiesAlive = FindObjectsOfType<EnemyMovementController>().Length;
    }

    public void NotifyEnemyDied()
    {
        EnemiesAlive--;
        if (EnemiesAlive <= 0)
        {
            EnemiesAlive = 0;

            OnAllEnemiesDefeated?.Invoke();
        }
    }

    public void CheckWinState()
    {
        int aliveCount = 0;

        foreach (GameObject player in players)
        {
            if (player.activeSelf)
            {
                aliveCount++;
            }
        }

        if (aliveCount <= 1)
        {
            EndStage();
        }
    }

    public void EndStage()
    {
        Invoke(nameof(NewRound), 4f);
    }

    private void NewRound()
    {
        if (GameMusicController.Instance != null &&
            GameMusicController.Instance.defaultMusic != null)
        {
            GameMusicController.Instance.PlayMusic(
                GameMusicController.Instance.defaultMusic
            );
        }

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
