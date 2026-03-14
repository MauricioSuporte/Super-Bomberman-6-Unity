using System;
using System.Collections.Generic;

namespace Assets.Scripts.SaveSystem
{
    [Serializable]
    public sealed class BossRushDifficultyTimesSave
    {
        public int difficulty;
        public List<float> topTimes = new();

        public float targetUnlockTime = -1f;
    }
}