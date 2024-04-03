using System;
using System.Linq;
using Dalamud.Interface.Windowing;
using FFXIVTweaks.Tweaks;
using ImGuiNET;

namespace FFXIVTweaks.Windows;

public class ConfigWindow : Window, IDisposable
{
    public ConfigWindow()
        : base("FFXIVTweaks", ImGuiWindowFlags.AlwaysAutoResize) { }

    public void Dispose() { }

    public override void Draw()
    {
        ImGui.Text("Hover labels for extra information");
        var last = Services.Tweaks.Last();
        if (ImGui.BeginTable("##Table", 2))
        {
            ImGui.TableNextColumn();
            foreach (ITweak tweak in Services.Tweaks)
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
