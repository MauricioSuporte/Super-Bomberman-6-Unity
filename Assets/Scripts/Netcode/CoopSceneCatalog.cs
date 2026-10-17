using UnityEngine;
using UnityEngine.Tilemaps;

namespace Assets.Scripts.Netcode
{
    // Generated per scene so entering a co-op stage does not load every world's art.
    public sealed class CoopSceneCatalog : ScriptableObject
    {
        public string contentHash;
        public Sprite[] sprites;
        public TileBase[] tiles;
        public Material[] materials;
        public AudioClip[] sounds;
        public string[] resourceSoundPaths;
    }
}
