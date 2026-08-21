using UnityEngine;

namespace StageAssets
{
    [System.Serializable]
    public sealed class Stage33BubbleAnimation
    {
        public Sprite[] sprites;
    }

    public sealed class Stage33BubbleAnimationCatalog : ScriptableObject
    {
        public Stage33BubbleAnimation[] animations;
    }
}
