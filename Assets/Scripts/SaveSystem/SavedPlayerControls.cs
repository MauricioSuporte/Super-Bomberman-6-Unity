using System;
using System.Collections.Generic;

namespace Assets.Scripts.SaveSystem
{
    [Serializable]
    public sealed class SavedPlayerControls
    {
        public int playerId = 1;
        public int joyIndex = 1;
        public int gamepadDeviceId = -1;
        public string gamepadProduct = "";
        public List<SavedBinding> bindings = new();
    }

    [Serializable]
    public sealed class SavedBinding
    {
        public int action;
        public int kind;
        public int key;
        public int dpadDir;
        public int joyIndex;
        public int joyButton;
    }
}