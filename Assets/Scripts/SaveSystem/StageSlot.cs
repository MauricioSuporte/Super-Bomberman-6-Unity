using System;
using System.Collections.Generic;

namespace Assets.Scripts.SaveSystem
{
    [Serializable]
    public class StageSlot
    {
        public bool started = false;
        public List<string> unlockedStages = new();
        public List<string> clearedStages = new();
        public List<string> perfectStages = new();
        public List<string> stageOrder = new();
    }
}
