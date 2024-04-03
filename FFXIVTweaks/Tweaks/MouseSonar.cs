using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using ImGuiNET;

namespace FFXIVTweaks.Tweaks;

public static class Colour
{
    public static uint Yellow = ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 0, 1f));
    public static uint Red = ImGui.ColorConvertFloat4ToU32(new Vector4(1, 0, 0, 1f));
    public static uint White = ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, 0.5f));
}

public unsafe class MouseSonar : ITweak
{
    public string description { get; set; } = "Shake to find mouse cursor";
    public bool enabled
    {
        get => Services.PluginConfig.MouseSonar.enabled;
        set
        {
            Services.PluginConfig.MouseSonar.enabled = value;
            Services.PluginConfig.Save();
            SetState();
        }
    }
    private Framework* gameFramework;
    private Vector2 p1; // mouse position at frame n-1
    private Vector2 p2; // mouse position at frame n
    private Queue<Tuple<Vector2, Vector2>> mouseLog = new();
    private int sonarCount = 0;
    private DateTime decayEnd = DateTime.UtcNow;
    private double decayTime
    {
        get
        {
            var t = decayEnd - DateTime.UtcNow;
            return t >= TimeSpan.Zero ? t.TotalSeconds : 0;
        }
    }
    private DateTime durationEnd = DateTime.UtcNow;
    private double durationTime
    {
        get
        {
            var t = durationEnd - DateTime.UtcNow;
            return t >= TimeSpan.Zero ? t.TotalSeconds : 0;
        }
    }
    private DateTime cooldownEnd = DateTime.UtcNow;
    private double cooldownTime
    {
        get
        {
            var t = cooldownEnd - DateTime.UtcNow;
            return t >= TimeSpan.Zero ? t.TotalSeconds : 0;
        }
    }

    private Vector4 colour
    {
        get => Services.PluginConfig.MouseSonar.colour;
        set
        {
            Services.PluginConfig.MouseSonar.colour = value;
            Services.PluginConfig.Save();
        }
    }
    private int size
    {
        get => Services.PluginConfig.MouseSonar.size;
        set
        {
            Services.PluginConfig.MouseSonar.size = value;
            Services.PluginConfig.Save();
        }
    }
    private double dotThreshold
    {
        get => Services.PluginConfig.MouseSonar.dotThreshold;
        set
        {
            Services.PluginConfig.MouseSonar.dotThreshold = value;
            Services.PluginConfig.Save();
        }
    }
    private int distanceThreshold
    {
        get => Services.PluginConfig.MouseSonar.distanceThreshold;
        set
        {
            Services.PluginConfig.MouseSonar.distanceThreshold = value;
            Services.PluginConfig.Save();
        }
    }
    private double timeDelta
    {
        get => Services.PluginConfig.MouseSonar.timeDelta;
        set
        {
            Services.PluginConfig.MouseSonar.timeDelta = value;
            Services.PluginConfig.Save();
        }
    }
    private int count
    {
        get => Services.PluginConfig.MouseSonar.count;
        set
        {
            Services.PluginConfig.MouseSonar.count = value;
            Services.PluginConfig.Save();
        }
    }
    private double decay
    {
        get => Services.PluginConfig.MouseSonar.decay;
        set
        {
            Services.PluginConfig.MouseSonar.decay = value;
            Services.PluginConfig.Save();
        }
    }
    private double duration
    {
        get => Services.PluginConfig.MouseSonar.duration;
        set
        {
            Services.PluginConfig.MouseSonar.duration = value;
            Services.PluginConfig.Save();
        }
    }
    private double cooldown
    {
        get => Services.PluginConfig.MouseSonar.cooldown;
        set
        {
            Services.PluginConfig.MouseSonar.cooldown = value;
            Services.PluginConfig.Save();
        }
    }
    private bool bgFade
    {
        get => Services.PluginConfig.MouseSonar.bgFade;
        set
        {
            Services.PluginConfig.MouseSonar.bgFade = value;
            Services.PluginConfig.Save();
        }
    }
    private bool preview = true;

    public MouseSonar()
    {
        gameFramework = Framework.Instance();
        SetState();
    }

    public void Reset()
    {
        Services.PluginConfig.MouseSonar = new MouseSonarConfig();
        Services.PluginConfig.Save();
        SetState(false);
        Services.PluginLog.Info($"{GetType().Name}: Reset");
    }

    public void SetState(bool? state = null)
    {
        state ??= enabled;
        if ((bool)state)
            Services.UiBuilder.Draw += DrawUI;
        else
            Services.UiBuilder.Draw -= DrawUI;
        Services.PluginLog.Info($"{GetType().Name}: State Set ({state})");
    }

    public void Update()
    {
        // TODO: check back when internals are available to implement row-wide hover
        // https://github.com/ImGuiNET/ImGui.NET/pull/364
        var _colour = colour;
        var _size = size;
        var _dotThreshold = (float)dotThreshold;
        var _distanceThreshold = distanceThreshold;
        var _timeDelta = (float)timeDelta;
        var _count = count;
        var _decay = (float)decay;
        var _duration = (float)duration;
        var _cooldown = (float)cooldown;
        var _bgFade = bgFade;
        var wDrawList = ImGui.GetWindowDrawList();
        var fgDrawList = ImGui.GetForegroundDrawList();

        var text = ImGui.GetIO().Framerate.ToString("0.0");
        var padding = (ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(text).X) * 0.5f;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + padding);
        ImGui.TextUnformatted(text);
        ImGui.TableNextColumn();
        ImGui.Text("Framerate");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(
                "Tracking mouse position is framerate dependent\n\n"
                    + "When the game window is in focus, moving the mouse drops framerate\n"
                    + "significantly compared to when out-of-focus\n\n"
                    + "This tweak compensates for this by tracking mouse positions based\n"
                    + "on time"
            );
        ImGui.TableNextColumn();

        text = "Colour";
        ImGui.SetNextItemWidth(-1);
        if (ImGui.ColorEdit4($"##{GetType().Name}", ref _colour))
            colour = _colour;
        ImGui.TableNextColumn();
        ImGui.Text(text);
        ImGui.TableNextColumn();

        text = "Size";
        ImGui.SetNextItemWidth(-1);
        if (ImGui.SliderInt($"##{text} {GetType().Name}", ref _size, 0, 200))
            size = _size;
        ImGui.TableNextColumn();
        ImGui.Text(text);
        ImGui.TableNextColumn();

        text = "Dot Threshold";
        ImGui.SetNextItemWidth(-1);
        if (
            ImGui.SliderFloat(
                $"##{text} {GetType().Name}",
                ref _dotThreshold,
                0,
                -1,
                null,
                ImGuiSliderFlags.AlwaysClamp
            )
        )
            dotThreshold = _dotThreshold;
        ImGui.TableNextColumn();
        ImGui.Text(text);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(
                "How tight should the mouse turn to be detected as \"shake\"\n\n"
                    + "More negative = tighter angle"
            );
        ImGui.TableNextColumn();

        text = "Distance Threshold";
        ImGui.SetNextItemWidth(-1);
        if (ImGui.SliderInt($"##{text} {GetType().Name}", ref _distanceThreshold, 0, 200))
            distanceThreshold = _distanceThreshold;
        ImGui.TableNextColumn();
        ImGui.Text(text);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(
                "Minimum distance for mouse to move for shake to be detected\n\n"
                    + "Prevents small jitters from triggering sonar"
            );
        ImGui.TableNextColumn();

        text = "Time Delta (s)";
        ImGui.SetNextItemWidth(-1);
        if (ImGui.SliderFloat($"##{text} {GetType().Name}", ref _timeDelta, 0, 0.25f))
            timeDelta = _timeDelta;
        ImGui.TableNextColumn();
        ImGui.Text(text);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Previous mouse position to compare against");
        ImGui.TableNextColumn();

        text = "Count";
        var beforePos = ImGui.GetCursorScreenPos();
        ImGui.SetNextItemWidth(-1);
        if (ImGui.SliderInt($"##{text} {GetType().Name}", ref _count, 0, 10))
            count = _count;
        var afterPos = ImGui.GetCursorScreenPos();
        // SameLine doesn't work here, adjust for automatically added padding
        afterPos.Y = beforePos.Y + ImGui.GetTextLineHeight();
        var sliderSize = ImGui.GetItemRectSize();
        var scaleX = count > 0 ? (double)(sonarCount) / count : sonarCount;
        sliderSize.X *= (float)scaleX;
        ImGui.SetCursorScreenPos(beforePos);
        wDrawList.AddRectFilled(beforePos, beforePos + sliderSize, Colour.White);
        ImGui.SetCursorScreenPos(afterPos);
        ImGui.TableNextColumn();
        ImGui.Text("Count");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Minimum number of shakes to trigger sonar");
        ImGui.TableNextColumn();

        text = "Decay (s)";
        beforePos = ImGui.GetCursorScreenPos();
        ImGui.SetNextItemWidth(-1);
        if (ImGui.SliderFloat($"##{text} {GetType().Name}", ref _decay, 0, 2))
            decay = _decay;
        afterPos = ImGui.GetCursorScreenPos();
        afterPos.Y = beforePos.Y + ImGui.GetTextLineHeight();
        sliderSize = ImGui.GetItemRectSize();
        scaleX = decay > 0 ? decayTime / decay : 0;
        sliderSize.X *= (float)scaleX;
        ImGui.SetCursorScreenPos(beforePos);
        wDrawList.AddRectFilled(beforePos, beforePos + sliderSize, Colour.White);
        ImGui.SetCursorScreenPos(afterPos);
        ImGui.TableNextColumn();
        ImGui.Text(text);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Time before counter resets");
        ImGui.TableNextColumn();

        text = "Duration (s)";
        beforePos = ImGui.GetCursorScreenPos();
        ImGui.SetNextItemWidth(-1);
        if (ImGui.SliderFloat($"##{text} {GetType().Name}", ref _duration, 0, 2))
            duration = _duration;
        afterPos = ImGui.GetCursorScreenPos();
        afterPos.Y = beforePos.Y + ImGui.GetTextLineHeight();
        sliderSize = ImGui.GetItemRectSize();
        scaleX = duration > 0 ? durationTime / duration : 0;
        sliderSize.X *= (float)scaleX;
        ImGui.SetCursorScreenPos(beforePos);
        wDrawList.AddRectFilled(beforePos, beforePos + sliderSize, Colour.White);
        ImGui.SetCursorScreenPos(afterPos);
        ImGui.TableNextColumn();
        ImGui.Text(text);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Sonar duration");
        ImGui.TableNextColumn();

        text = "Cooldown (s)";
        beforePos = ImGui.GetCursorScreenPos();
        ImGui.SetNextItemWidth(-1);
        if (ImGui.SliderFloat($"##{text} {GetType().Name}", ref _cooldown, 0, 2))
            cooldown = _cooldown;
        afterPos = ImGui.GetCursorScreenPos();
        afterPos.Y = beforePos.Y + ImGui.GetTextLineHeight();
        sliderSize = ImGui.GetItemRectSize();
        scaleX = (cooldown + duration) > 0 ? cooldownTime / (cooldown + duration) : 0;
        sliderSize.X *= (float)scaleX;
        ImGui.SetCursorScreenPos(beforePos);
        wDrawList.AddRectFilled(beforePos, beforePos + sliderSize, Colour.White);
        ImGui.SetCursorScreenPos(afterPos);
        ImGui.TableNextColumn();
        ImGui.Text(text);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(
                "Time before sonar can be triggered again after previous sonar is finished"
            );
        ImGui.TableNextColumn();

        if (ImGui.Checkbox($"Background Fade##{GetType().Name}", ref _bgFade))
            bgFade = _bgFade;
        ImGui.TableNextColumn();
        ImGui.TableNextColumn();

        ImGui.Checkbox($"Preview##{GetType().Name}", ref preview);
        ImGui.TableNextColumn();
        ImGui.TableNextColumn();

        if (preview)
        {
            fgDrawList.AddCircleFilled(p2, size, ImGui.ColorConvertFloat4ToU32(colour));
            fgDrawList.AddCircle(p2, distanceThreshold, Colour.Yellow, 0, 3);

            // dot sector
            var validSector = Math.PI - Math.Acos(dotThreshold);
            var startRad = -validSector;
            var endRad = validSector;
            for (var i = 0; i <= 12; ++i)
            {
                var angle = startRad + i / 12f * (endRad - startRad);
                fgDrawList.PathLineTo(
                    new Vector2(
                        (float)(p2.X + Math.Cos(angle) * distanceThreshold),
                        (float)(p2.Y + Math.Sin(angle) * distanceThreshold)
                    )
                );
            }
            fgDrawList.PathLineTo(p2);
            fgDrawList.PathFillConvex(Colour.Red);
            fgDrawList.PathStroke(Colour.Red, ImDrawFlags.None, 0);
        }
    }

    private double EaseInOut(double x, double a = 0.25)
    {
        //   a=turning point
        //          v   v
        // y=1       ___
        //          /   \
        //          |   |
        // y=0 x=1 _/   \_ x=0
        if (x >= 1 - a)
            x = (1 - x) / a;
        else if (x <= a)
            x /= a;
        else
            x = 1;
        // parametric function
        // https://stackoverflow.com/questions/13462001/ease-in-and-ease-out-animation-formula
        var sqr = x * x;
        return sqr / (2 * (sqr - x) + 1);
    }

    private double Linear(double x, double a = 0.25, double initScale = 2)
    {
        //             a=turning point
        //                    v   v
        // y=initScale   x=1 \
        // y=1                \___
        //                        \
        // y=0                     \ x=0
        if (x >= 1 - a)
            x = (x - 1 + a) / a * (initScale - 1) + 1;
        else if (x <= a)
            x /= a;
        else
            x = 1;
        return x;
    }

    private void DrawUI()
    {
        // note: framerate is higher when game window is out of focus
        p2 = ImGui.GetMousePos();
        var v1 = p2 - p1;
        var dot = 0f;
        var frameNum = ImGui.GetIO().Framerate * timeDelta;

        mouseLog.Enqueue(new Tuple<Vector2, Vector2>(p2, v1));
        while (mouseLog.Count > Math.Max(frameNum, 2))
            mouseLog.Dequeue();

        if (
            !gameFramework->Cursor->IsCursorOutsideViewPort
            && gameFramework->Cursor->IsCursorVisible
        )
        {
            var v1norm = Vector2.Normalize(v1);
            var p0 = mouseLog.Peek();
            var v0norm = Vector2.Normalize(p0.Item2);
            dot = v1norm.X * v0norm.X + v1norm.Y * v0norm.Y;
            var distance = (p2 - p0.Item1).Length();

            if (dot <= dotThreshold && distance > distanceThreshold && cooldownTime == 0)
            {
                mouseLog.Clear();
                if (sonarCount < count)
                    sonarCount += 1;
                decayEnd = DateTime.UtcNow.AddSeconds(decay);
                if (sonarCount == count)
                {
                    durationEnd = DateTime.UtcNow.AddSeconds(duration);
                    cooldownEnd = DateTime.UtcNow.AddSeconds(cooldown + duration);
                }
            }
        }

        if (decayTime == 0)
            sonarCount = 0;
        p1 = p2;

        ImGuiHelpers.ForceNextWindowMainViewport();
        ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Vector2(0, 0));
        ImGui.SetNextWindowSize(ImGui.GetIO().DisplaySize); // game window size
        var t = durationTime / duration;
        var bgA = bgFade ? EaseInOut(t) * 0.5 : 0;
        var _colour = colour;
        _colour.W *= (float)EaseInOut(t);
        ImGui.PushStyleColor(
            ImGuiCol.WindowBg,
            ImGui.ColorConvertFloat4ToU32(new Vector4(0, 0, 0, (float)bgA))
        );
        ImGui.Begin(
            "MouseSonar",
            ImGuiWindowFlags.NoDecoration
                | ImGuiWindowFlags.NoSavedSettings
                | ImGuiWindowFlags.NoInputs
                | ImGuiWindowFlags.NoBringToFrontOnFocus
        );
        if (durationTime > 0)
        {
            ImGui
                .GetWindowDrawList()
                .AddCircleFilled(
                    p2,
                    (float)(Linear(t) * size),
                    ImGui.ColorConvertFloat4ToU32(_colour)
                );
        }
        ImGui.PopStyleColor();
        ImGui.End();
    }
}
