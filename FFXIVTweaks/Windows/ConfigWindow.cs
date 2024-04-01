using System;
using System.Linq;
using Dalamud.Interface.Windowing;
using FFXIVTweaks.Tweaks;
using ImGuiNET;

namespace FFXIVTweaks.Windows;

public class ConfigWindow : Window, IDisposable
{
    private Plugin plugin;

    public ConfigWindow(Plugin plugin)
        : base("FFXIVTweaks", ImGuiWindowFlags.AlwaysAutoResize)
    {
        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        ImGui.Text("Hover labels for extra information");
        var last = plugin.tweaksList.Last();
        if (ImGui.BeginTable("##Table", 2))
        {
            ImGui.TableNextColumn();
            foreach (ITweaks tweak in plugin.tweaksList)
            {
                {
                    var enabled = tweak.enabled; // can't ref a property, so use a local copy
                    if (ImGui.Checkbox(tweak.description, ref enabled)) // widget label = widget ID
                        tweak.enabled = enabled;

                    ImGui.TableNextColumn();
                    if (ImGui.Button($"Reset##{tweak.GetType().Name}"))
                        tweak.Reset();
                    ImGui.TableNextColumn();

                    if (enabled)
                        tweak.Update();

                    if (!tweak.Equals(last))
                    {
                        ImGui.Separator();
                        ImGui.TableNextColumn();
                        ImGui.Separator();
                        ImGui.TableNextColumn();
                    }
                }
            }
            ImGui.EndTable();
        }
    }
}
