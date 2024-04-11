using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Numerics;
using Dalamud.Interface.Utility;
using FFXIV.Tweaks;
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

    public class Config : IConfig, INotifyPropertyChanged
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

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public IConfig config
    {
        get => _config;
        set => _config = (Config)value;
    }

    private Config _config
    {
        get => Services.PluginConfig.MouseSonar;
        set
        {
            Services.PluginConfig.MouseSonar = value;
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
    private bool preview = true;

    public MouseSonar()
    {
        gameFramework = Framework.Instance();
        _config.PropertyChanged += new PropertyChangedEventHandler(
            (_, _) =>
            {
                Services.PluginConfig.Save();
                SetState();
            }
        );
        SetState();
    }

    public void Reset()
    {
        var cfg = new Config();
        cfg.PropertyChanged += new PropertyChangedEventHandler(
            (_, _) =>
            {
                Services.PluginConfig.Save();
                SetState();
            }
        );
        Services.PluginConfig.MouseSonar = cfg;
        Services.PluginConfig.Save();
        SetState(false);
        Services.PluginLog.Info($"{GetType().Name}: Reset");
    }

    public void SetState(bool? state = null)
    {
        state ??= _config.enabled;
        if ((bool)state)
        {
            Services.UiBuilder.Draw -= DrawUI;
            Services.UiBuilder.Draw += DrawUI;
        }
        else
            Services.UiBuilder.Draw -= DrawUI;
        Services.PluginLog.Info($"{GetType().Name}: State Set ({state})");
    }

    public void Update()
    {
        // TODO: check back when internals are available to implement row-wide hover
        // https://github.com/ImGuiNET/ImGui.NET/pull/364
        var _colour = _config.colour;
        var _size = _config.size;
        var _dotThreshold = (float)_config.dotThreshold;
        var _distanceThreshold = _config.distanceThreshold;
        var _timeDelta = (float)_config.timeDelta;
        var _count = _config.count;
        var _decay = (float)_config.decay;
        var _duration = (float)_config.duration;
        var _cooldown = (float)_config.cooldown;
        var _bgFade = _config.bgFade;
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
            _config.colour = _colour;
        ImGui.TableNextColumn();
        ImGui.Text(text);
        ImGui.TableNextColumn();

        text = "Size";
        ImGui.SetNextItemWidth(-1);
        if (ImGui.SliderInt($"##{text} {GetType().Name}", ref _size, 0, 200))
            _config.size = _size;
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
            _config.dotThreshold = _dotThreshold;
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
            _config.distanceThreshold = _distanceThreshold;
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
            _config.timeDelta = _timeDelta;
        ImGui.TableNextColumn();
        ImGui.Text(text);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Previous mouse position to compare against");
        ImGui.TableNextColumn();

        text = "Count";
        var beforePos = ImGui.GetCursorScreenPos();
        ImGui.SetNextItemWidth(-1);
        if (ImGui.SliderInt($"##{text} {GetType().Name}", ref _count, 0, 10))
            _config.count = _count;
        var afterPos = ImGui.GetCursorScreenPos();
        // SameLine doesn't work here, adjust for automatically added padding
        afterPos.Y = beforePos.Y + ImGui.GetTextLineHeight();
        var sliderSize = ImGui.GetItemRectSize();
        var scaleX = _config.count > 0 ? (double)(sonarCount) / _config.count : sonarCount;
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
            _config.decay = _decay;
        afterPos = ImGui.GetCursorScreenPos();
        afterPos.Y = beforePos.Y + ImGui.GetTextLineHeight();
        sliderSize = ImGui.GetItemRectSize();
        scaleX = _config.decay > 0 ? decayTime / _config.decay : 0;
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
            _config.duration = _duration;
        afterPos = ImGui.GetCursorScreenPos();
        afterPos.Y = beforePos.Y + ImGui.GetTextLineHeight();
        sliderSize = ImGui.GetItemRectSize();
        scaleX = _config.duration > 0 ? durationTime / _config.duration : 0;
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
            _config.cooldown = _cooldown;
        afterPos = ImGui.GetCursorScreenPos();
        afterPos.Y = beforePos.Y + ImGui.GetTextLineHeight();
        sliderSize = ImGui.GetItemRectSize();
        scaleX =
            (_config.cooldown + _config.duration) > 0
                ? cooldownTime / (_config.cooldown + _config.duration)
                : 0;
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
            _config.bgFade = _bgFade;
        ImGui.TableNextColumn();
        ImGui.TableNextColumn();

        ImGui.Checkbox($"Preview##{GetType().Name}", ref preview);
        ImGui.TableNextColumn();
        ImGui.TableNextColumn();

        if (preview)
        {
            fgDrawList.AddCircleFilled(
                p2,
                _config.size,
                ImGui.ColorConvertFloat4ToU32(_config.colour)
            );
            fgDrawList.AddCircle(p2, _config.distanceThreshold, Colour.Yellow, 0, 3);

            // dot sector
            var validSector = Math.PI - Math.Acos(_config.dotThreshold);
            var startRad = -validSector;
            var endRad = validSector;
            for (var i = 0; i <= 12; ++i)
            {
                var angle = startRad + i / 12f * (endRad - startRad);
                fgDrawList.PathLineTo(
                    new Vector2(
                        (float)(p2.X + Math.Cos(angle) * _config.distanceThreshold),
                        (float)(p2.Y + Math.Sin(angle) * _config.distanceThreshold)
                    )
                );
            }
            fgDrawList.PathLineTo(p2);
            fgDrawList.PathFillConvex(Colour.Red);
            fgDrawList.PathStroke(Colour.Red, ImDrawFlags.None, 0);
        }
    }

    public void Dispose() { }

    private void DrawUI()
    {
        // note: framerate is higher when game window is out of focus
        p2 = ImGui.GetMousePos();
        var v1 = p2 - p1;
        var dot = 0f;
        var frameNum = ImGui.GetIO().Framerate * _config.timeDelta;

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

            if (
                dot <= _config.dotThreshold
                && distance > _config.distanceThreshold
                && cooldownTime == 0
            )
            {
                mouseLog.Clear();
                if (sonarCount < _config.count)
                    sonarCount += 1;
                decayEnd = DateTime.UtcNow.AddSeconds(_config.decay);
                if (sonarCount == _config.count)
                {
                    durationEnd = DateTime.UtcNow.AddSeconds(_config.duration);
                    cooldownEnd = DateTime.UtcNow.AddSeconds(_config.cooldown + _config.duration);
                }
            }
        }

        if (decayTime == 0)
            sonarCount = 0;
        p1 = p2;

        ImGuiHelpers.ForceNextWindowMainViewport();
        ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Vector2(0, 0));
        ImGui.SetNextWindowSize(ImGui.GetIO().DisplaySize); // game window size
        var t = durationTime / _config.duration;
        var bgA = _config.bgFade ? Util.EaseInOut(t) * 0.5 : 0;
        var _colour = _config.colour;
        _colour.W *= (float)Util.EaseInOut(t);
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
                    (float)(Util.Linear(t) * _config.size),
                    ImGui.ColorConvertFloat4ToU32(_colour)
                );
        }
        ImGui.PopStyleColor();
        ImGui.End();
    }
}
