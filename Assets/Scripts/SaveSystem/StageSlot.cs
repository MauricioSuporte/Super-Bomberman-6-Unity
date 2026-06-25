using System;
using System.Collections.Generic;

namespace Assets.Scripts.SaveSystem
{
    public enum NormalGameDifficulty
    {
        Normal = 0,
        Hard = 1,
        Hardcore = 2
    }

    [Serializable]
    public class StageSlot
    {
        public bool started = false;
        public int difficulty = (int)NormalGameDifficulty.Normal;
        public List<string> unlockedStages = new();
        public List<string> clearedStages = new();
        public List<string> normalClearedStages = new();
        public List<string> hardClearedStages = new();
        public List<string> hardcoreClearedStages = new();
        public List<string> perfectStages = new();
        public List<string> stageOrder = new();
    }
}
