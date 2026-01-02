using UnityEngine;
using System.Collections.Generic;

public class PlayerInputManager : MonoBehaviour
{
    public static PlayerInputManager Instance { get; private set; }

    private readonly Dictionary<int, PlayerInputProfile> players = new();

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Inicializa Player 1
        players[1] = new PlayerInputProfile(1);
    }

    public PlayerInputProfile GetPlayer(int playerId)
    {
        return players[playerId];
    }

    public bool Get(PlayerAction action, int playerId = 1)
    {
        return Input.GetKey(
            players[playerId].GetKey(action)
        );
    }

    public bool GetDown(PlayerAction action, int playerId = 1)
    {
        return Input.GetKeyDown(
            players[playerId].GetKey(action)
        );
    }
}
