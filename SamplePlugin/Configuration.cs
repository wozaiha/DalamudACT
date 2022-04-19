﻿using System;
using System.Numerics;
using Dalamud.Configuration;
using Dalamud.Plugin;

namespace ACT
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public bool Enabled;
        public bool Locked;
        public Vector2 WindowSize = Vector2.One;
        public bool LevelEnabled;
        public int FateLevel;
        public bool HideName;
        public bool Mini;

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