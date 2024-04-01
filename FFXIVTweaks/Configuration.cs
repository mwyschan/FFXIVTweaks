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

    public class MouseSonarConfig
    {
        public bool enabled { get; set; } = false;
        public Vector4 colour { get; set; } = new(1, 1, 1, 0.5f);
        public int size { get; set; } = 100;
        public double dotThreshold { get; set; } = -0.8;
        public int distanceThreshold { get; set; } = 100;
        public double timeDelta { get; set; } = 0.05;
        public int count { get; set; } = 3;
        public double decay { get; set; } = 0.2;
        public double duration { get; set; } = 0.75;
        public double cooldown { get; set; } = 0.5;
        public bool bgFade { get; set; } = true;
    }

    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;

        public PartyListOvershieldConfig PartyListOvershield { get; set; } =
            new PartyListOvershieldConfig();
        public MouseSonarConfig MouseSonar { get; set; } = new MouseSonarConfig();

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
