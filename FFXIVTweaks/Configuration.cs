using System;
using Dalamud.Configuration;
using Dalamud.Plugin;
using FFXIVTweaks.Tweaks;

namespace FFXIVTweaks;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public PartyListOvershield.Config PartyListOvershield { get; set; } = new();
    public MouseSonar.Config MouseSonar { get; set; } = new();
    public Gotify.Config Gotify { get; set; } = new();

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
