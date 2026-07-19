using UnityEngine;
using UnityEngine.SceneManagement;

namespace StageAssets
{
    /// <summary>
    /// Identifies the water cells covered by the Room 2 bridge in Stage 3-1.
    /// </summary>
    public static class World3BridgeWaterTiles
    {
        private const string StageSceneName = "Stage_3-1";

        public static bool IsBridgeCell(Vector3Int cell)
        {
            return SceneManager.GetActiveScene().name == StageSceneName &&
                   cell.y == -1 &&
                   cell.x >= 20 &&
                   cell.x <= 22;
        }

        public static bool IsBridgeAtWorldPosition(Vector3 worldPosition)
        {
            return IsBridgeCell(new Vector3Int(
                Mathf.FloorToInt(worldPosition.x),
                Mathf.FloorToInt(worldPosition.y),
                0));
        }
    }
}
