using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Numerics;
using System.Windows.Forms;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;

namespace FFXIVTweaks.Tweaks;

public class Gotify : ITweak
{
    public string description { get; set; } = "Notfiy duty pop via Gotify (for now)";

    public class Config : IConfig, INotifyPropertyChanged
    {
        public bool enabled { get; set; } = false;
        public Uri url { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public IConfig config
    {
        get => _config;
        set { _config = (Config)value; }
    }

    private Config _config
    {
        get => Services.PluginConfig.Gotify;
        set
        {
            Services.PluginConfig.Gotify = value;
            Services.PluginConfig.Save();
            SetState();
        }
    }

    private class Colour
    {
        public static uint Red = ImGui.ColorConvertFloat4ToU32(new Vector4(1, 0, 0, 0.5f));
        public static uint Green = ImGui.ColorConvertFloat4ToU32(new Vector4(0, 1, 0, 0.5f));
    }

    // ---

    private HttpClient client = new();

    public Gotify()
    {
        SetState();

        _config.PropertyChanged += new PropertyChangedEventHandler(
            (_, _) =>
            {
                Services.PluginConfig.Save();
                SetState();
            }
        );
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
        Services.PluginConfig.Gotify = cfg;
        Services.PluginConfig.Save();
        SetState(false);
        Services.PluginLog.Info($"{GetType().Name}: Reset");
    }

    public void SetState(bool? state = null)
    {
        state ??= _config.enabled;

        if ((bool)state)
        {
            Services.ClientState.CfPop -= Register;
            Services.ClientState.CfPop += Register;
        }
        else
            Services.ClientState.CfPop -= Register;

        Services.PluginLog.Info($"{GetType().Name}: State Set ({state})");
    }

    public void Update()
    {
        string _url;
        if (_config.url == null)
            _url = "";
        else
            _url = _config.url!.ToString();
        var text = "URL";
        ImGui.SetNextItemWidth(-1);
        if (
            ImGui.InputText($"##{text} {GetType().Name}", ref _url, 200) // memory of string is only held until textbox is unfocused
            && Uri.TryCreate(_url, UriKind.Absolute, out var result)
            && (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps)
        )
            _config.url = result;
        ImGui.TableNextColumn();
        ImGui.Text(text);
        ImGui.TableNextColumn();
    }

    public void Dispose()
    {
        client.Dispose();
    }

    private async void Register(ContentFinderCondition condition)
    {
        // https://flurl.dev/
        using var notification = new NotifyIcon();
        var title = "Duty Pop";
        var message = $"{condition.Name}";
        notification.Icon = Icon.ExtractAssociatedIcon(
            Path.Combine(Services.DataManager.GameData.DataPath.Parent!.FullName, "ffxiv_dx11.exe")
        );
        notification.Text = title;
        notification.Visible = true;
        notification.ShowBalloonTip(7500, title, message, ToolTipIcon.Info);

        if (_config.url != null)
            await client.PostAsJsonAsync(
                _config.url,
                new
                {
                    title,
                    message,
                    priority = 1000
                }
            );
    }
}
