using System.Collections.Generic;
using UnityEngine;

public class PlayerIdentity : MonoBehaviour
{
    private static readonly HashSet<PlayerIdentity> activePlayers = new();

    [Range(1, 6)]
    public int playerId = 1;

    public static IReadOnlyCollection<PlayerIdentity> ActivePlayers => activePlayers;

    private void OnEnable()
    {
        activePlayers.Add(this);
    }

    private void OnDisable()
    {
        activePlayers.Remove(this);
    }

    private void OnDestroy()
    {
        activePlayers.Remove(this);
    }

    public static void GetActivePlayers(List<PlayerIdentity> results)
    {
        if (results == null)
            return;

        results.Clear();

        foreach (var player in activePlayers)
        {
            if (player != null)
                results.Add(player);
        }
    }
}
