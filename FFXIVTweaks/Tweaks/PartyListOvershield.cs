using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;

namespace FFXIVTweaks.Tweaks;

/*
node 10-17:     players
node 19:        SCH eos, SMN carbuncle
node 180007:    chocobo
subnode 12: textures
subnode 11: hpGauge                 (y=16, AtkNineGridNode)
subnode 10: ?
subnode 9:  hpHeal                  (y= 0, AtkNineGridNode)
subnode 8:  shieldGauge             (y=16, AtkNineGridNode)
subnode 7:  shieldGaugeFadeOut      (y= 0, AtkNineGridNode)
subnode 6:  shieldGaugeFadeIn       (y= 0, AtkNineGridNode)
subnode 5:  overshieldGauge         (y= 8, AtkNineGridNode)
subnode 4:  overshieldGaugeFadeOut  (y=-8, AtkNineGridNode)
subnode 3:  overshieldGaugeFadeIn   (y=-8, AtkNineGridNode)
subnode 2:  overshieldGaugeExcess   (y= 9, AtkImageNode   )
*/
public unsafe class PartyListOvershield : ITweak
{
    public string description { get; set; } = "Move overshield gauge on HP in party list";
    public bool enabled
    {
        get => Services.PluginConfig.PartyListOvershield.enabled;
        set
        {
            Services.PluginConfig.PartyListOvershield.enabled = value;
            Services.PluginConfig.Save();
            SetState();
        }
    }
    private AtkUnitBase* partyList;
    private List<int> targetGaugeBarIds = [10, 11, 12, 13, 14, 15, 16, 17, 180007];
    private Vector3 colour
    {
        get => Services.PluginConfig.PartyListOvershield.colour;
        set
        {
            Services.PluginConfig.PartyListOvershield.colour = value;
            Services.PluginConfig.Save();
            SetState();
        }
    }

    public PartyListOvershield()
    {
        // starts on game launch, player list not yet available, listen for setup
        // using draw events to ensure init always triggers (incl. new installs)
        Services.AddonLifecycle.RegisterListener(AddonEvent.PreDraw, "_PartyList", GetPartyList);
    }

    public void Reset()
    {
        Services.PluginConfig.PartyListOvershield = new PartyListOvershieldConfig();
        Services.PluginConfig.Save();
        SetState(false);
        Services.PluginLog.Info($"{GetType().Name}: Reset");
    }

    public void Update()
    {
        var _colour = colour;
        var currentText = "Colour";
        ImGui.SetNextItemWidth(-1);
        if (ImGui.ColorEdit3($"##{currentText} {GetType().Name}", ref _colour))
            colour = _colour;
        ImGui.TableNextColumn();
        ImGui.Text(currentText);
        ImGui.TableNextColumn();
    }

    private void GetPartyList(AddonEvent type, AddonArgs args)
    {
        partyList = (AtkUnitBase*)args.Addon;
        SetState();
        Services.AddonLifecycle.UnregisterListener(GetPartyList);
    }

    public void SetState(bool? state = null)
    {
        state ??= enabled;
        if (partyList == null)
            return;
        foreach (var i in targetGaugeBarIds)
        {
            var gaugeBarNode = partyList
                ->GetNodeById((uint)i) // player node
                ->GetComponent()
                ->UldManager.SearchNodeById(12) // hp node
                ->GetComponent()
                ->UldManager.SearchNodeById(4); // gauge bar node

            for (var j = 5; j >= 2; j--)
            {
                var node = gaugeBarNode->GetComponent()->UldManager.SearchNodeById((uint)j);

                if ((bool)state)
                {
                    node->AddRed = (short)(255 * colour.X - 255);
                    node->AddGreen = (short)(255 * colour.Y - 211);
                    node->AddBlue = (short)(255 * colour.Z);

                    node->Y = j switch
                    {
                        5 => 16,
                        2 => 17,
                        _ => 0,
                    };
                }
                else
                {
                    node->AddRed = 0;
                    node->AddGreen = 0;
                    node->AddBlue = 0;

                    node->Y = j switch
                    {
                        5 => 8,
                        2 => 9,
                        _ => -8,
                    };
                }
                node->ScreenY = gaugeBarNode->ScreenY + node->Y * *node->Transform.Matrix;
            }
        }
        Services.PluginLog.Info($"{GetType().Name}: State Set ({state})");
    }

    private void AddressDebug(AtkResNode* node)
    {
        var ptr = new IntPtr(node);
        Services.PluginLog.Warning(ptr.ToString("X8"));
    }
}
