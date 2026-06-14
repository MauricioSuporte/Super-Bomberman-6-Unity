using System.Collections.Generic;
using UnityEngine;

public interface IBattleModeComPlannedBombDangerProvider
{
    bool TryAppendPlannedBombDangerTiles(
        Vector2Int plantTile,
        List<Vector2Int> plannedDangerTiles);
}
