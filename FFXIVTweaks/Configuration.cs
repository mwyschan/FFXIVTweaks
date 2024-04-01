using System;
using System.Numerics;
using Dalamud.Configuration;
using Dalamud.Plugin;

namespace FFXIVTweaks
{
    public class PartyListOvershieldConfig
    {
        public bool enabled { get; set; } = false;
        public Vector3 colour { get; set; } = new(1, 211f / 255f, 0); // default yellow
    }

    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;

        public PartyListOvershieldConfig PartyListOvershield { get; set; } =
            new PartyListOvershieldConfig();

        // the below exist just to make saving less cumbersome
        [NonSerialized]
        private DalamudPluginInterface? pluginInterface;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;
        }

        public void Save()
        {
            pluginInterface!.SavePluginConfig(this);
        }
    }
}
