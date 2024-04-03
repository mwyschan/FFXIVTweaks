using System.Collections.Generic;
using Dalamud.Game.Command;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVTweaks.Tweaks;
using FFXIVTweaks.Windows;

namespace FFXIVTweaks;

public static class Services
{
    public static Configuration PluginConfig;
    public static ICommandManager CommandManager;
    public static IPluginLog PluginLog;
    public static IAddonEventManager AddonEventManager;
    public static IAddonLifecycle AddonLifecycle;
    public static IGameGui GameGui;
    public static IFramework Framework;
    public static IClientState ClientState;
    public static UiBuilder UiBuilder;
    public static List<ITweak> Tweaks;
}

public sealed class Plugin : IDalamudPlugin
{
    // https://github.com/goatcorp/SamplePlugin/pull/33
    // assembly name, uniquely identifies plugin in plugin installer
    // as opposed to json manifest name, which is for display only
    public string Name => "FFXIVTweaks";
    private const string CommandName = "/ptweaks";

    public DalamudPluginInterface pluginInterface { get; init; }
    public Configuration configuration { get; init; }
    public WindowSystem windowSystem = new("FFXIVTweaks");
    private ConfigWindow configWindow { get; init; }

    public Plugin(
        [RequiredVersion("9.0")] DalamudPluginInterface pluginInterface,
        [RequiredVersion("9.0")] ICommandManager commandManager,
        [RequiredVersion("9.0")] IPluginLog pluginLog,
        [RequiredVersion("9.0")] IAddonEventManager addonEventManager,
        [RequiredVersion("9.0")] IAddonLifecycle addonLifecycle,
        [RequiredVersion("9.0")] IGameGui gameGui,
        [RequiredVersion("9.0")] IFramework framework,
        [RequiredVersion("9.0")] IClientState clientState
    )
    {
        this.pluginInterface = pluginInterface;
        Services.CommandManager = commandManager;
        Services.PluginLog = pluginLog;
        Services.AddonEventManager = addonEventManager;
        Services.AddonLifecycle = addonLifecycle;
        Services.GameGui = gameGui;
        Services.Framework = framework;
        Services.ClientState = clientState;
        Services.UiBuilder = this.pluginInterface.UiBuilder;

        Services.PluginConfig =
            this.pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Services.PluginConfig.Initialize(this.pluginInterface);

        configWindow = new ConfigWindow();
        windowSystem.AddWindow(configWindow);

        Services.CommandManager.AddHandler(
            CommandName,
            new CommandInfo(OnCommand) { HelpMessage = "Opens the tweaks menu" }
        );

        Services.UiBuilder.Draw += DrawUI;
        Services.UiBuilder.OpenMainUi += DrawConfigUI;
        Services.UiBuilder.OpenConfigUi += DrawConfigUI;

        // TODO: think of a good way to gather all tweaks classes
        Services.Tweaks = [new PartyListOvershield(), new MouseSonar()];
    }

    public void Dispose()
    {
        windowSystem.RemoveAllWindows();
        configWindow.Dispose();
        Services.CommandManager.RemoveHandler(CommandName);
        foreach (ITweak tweak in Services.Tweaks)
            tweak.SetState(false);
    }

    private void OnCommand(string command, string args)
    {
        configWindow.IsOpen = true;
    }

    private void DrawUI()
    {
        windowSystem.Draw();
    }

    public void DrawConfigUI()
    {
        configWindow.IsOpen = true;
    }
}
