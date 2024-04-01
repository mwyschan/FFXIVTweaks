using System.Collections.Generic;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVTweaks.Tweaks;
using FFXIVTweaks.Windows;

namespace FFXIVTweaks
{
    public sealed class Plugin : IDalamudPlugin
    {
        // https://github.com/goatcorp/SamplePlugin/pull/33
        // assembly name, uniquely identifies plugin in plugin installer
        // as opposed to json manifest name, which is for display only
        public string Name => "FFXIVTweaks";
        private const string CommandName = "/ptweaks";

        public DalamudPluginInterface pluginInterface { get; init; }
        public ICommandManager commandManager { get; init; }
        public IPluginLog pluginLog { get; init; }
        public IAddonEventManager addonEventManager { get; init; }
        public IAddonLifecycle addonLifecycle { get; init; }
        public IGameGui gameGui { get; init; }
        public IFramework framework { get; init; }
        public IClientState clientState { get; init; }

        public Configuration configuration { get; init; }
        public WindowSystem windowSystem = new("FFXIVTweaks");

        private ConfigWindow configWindow { get; init; }

        public List<ITweaks> tweaksList;

        public Plugin(
            [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
            [RequiredVersion("1.0")] ICommandManager commandManager,
            [RequiredVersion("1.0")] IPluginLog pluginLog,
            [RequiredVersion("1.0")] IAddonEventManager addonEventManager,
            [RequiredVersion("1.0")] IAddonLifecycle addonLifecycle,
            [RequiredVersion("1.0")] IGameGui gameGui,
            [RequiredVersion("1.0")] IFramework framework,
            [RequiredVersion("1.0")] IClientState clientState
        )
        {
            this.pluginInterface = pluginInterface;
            this.commandManager = commandManager;
            this.pluginLog = pluginLog;
            this.addonEventManager = addonEventManager;
            this.addonLifecycle = addonLifecycle;
            this.gameGui = gameGui;
            this.framework = framework;
            this.clientState = clientState;

            configuration =
                this.pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            configuration.Initialize(this.pluginInterface);

            configWindow = new ConfigWindow(this);
            windowSystem.AddWindow(configWindow);

            this.commandManager.AddHandler(
                CommandName,
                new CommandInfo(OnCommand) { HelpMessage = "Opens the tweaks menu" }
            );

            this.pluginInterface.UiBuilder.Draw += DrawUI;
            this.pluginInterface.UiBuilder.OpenMainUi += DrawConfigUI;
            this.pluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;

            // TODO: think of a good way to gather all tweaks classes
            tweaksList = [new PartyListOvershield(this)];
        }

        public void Dispose()
        {
            windowSystem.RemoveAllWindows();
            configWindow.Dispose();
            commandManager.RemoveHandler(CommandName);
            foreach (ITweaks tweak in tweaksList)
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
}
