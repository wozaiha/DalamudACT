using System;
using System.Numerics;
using Dalamud.Configuration;
using Dalamud.Plugin;

namespace CEHelper
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public bool Enabled;
        public bool Locked;
        public Vector2 WindowPos;
        public bool LevelEnabled;
        public int FateLevel;

        // the below exist just to make saving less cumbersome

        [NonSerialized]
        private DalamudPluginInterface? pluginInterface;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;
        }

        public void Save()
        {
            this.pluginInterface!.SavePluginConfig(this);
        }

        public int Version { get; set; }
    }
}
