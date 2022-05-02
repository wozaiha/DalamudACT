using System;
using System.Numerics;
using Dalamud.Configuration;
using Dalamud.Plugin;

namespace DalamudACT
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public bool Lock;
        public bool NoResize;
        public Vector2 WindowSize = Vector2.One;
        public bool HideName;
        public bool Mini;
        public int BGColor = 100;
        public bool delta = false;

        // the below exist just to make saving less cumbersome

        [NonSerialized] private DalamudPluginInterface? pluginInterface;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;
        }

        public void Save()
        {
            pluginInterface!.SavePluginConfig(this);
        }

        public int Version { get; set; }
    }
}