using UnityEngine;
using UnityEngine.SceneManagement;

namespace StageAssets
{
    [DisallowMultipleComponent]
    public sealed class World3HallPortal : MonoBehaviour
    {
        [SerializeField] private string destinationScene = "Stage_3-1";

        public int DestinationBuildIndex =>
            SceneUtility.GetBuildIndexByScenePath($"Assets/Scenes/{destinationScene}.unity");
    }
}
