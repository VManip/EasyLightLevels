using System;
using System.Collections.Generic;
using System.Threading;
using EasyLightLevels.Config;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace EasyLightLevels
{
    internal class EllCoreSystem : ModSystem
    {
        private const string ConfigFilename = "EasyLightLevelsConfig.json";
        private ICoreClientAPI _api;

        private EllConfig _config;
        
        private BlockPainter _painter;

        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Client;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            if (!ShouldLoad(api.Side)) return;

            _api = api;

            UpdateConfig();

            _painter = new BlockPainter(_api, _config);

            api.ChatCommands.Create("lightlvl")
                .WithDescription("See light levels.")
                .WithAlias("lightlevel")
                .HandleWith(ToggleCommand)
                .BeginSubCommand("help")
                .WithAlias("h")
                .HandleWith(HelpCommand)
                .EndSubCommand()
                .BeginSubCommand("abort")
                .WithAlias("a")
                .HandleWith(AbortCommand)
                .EndSubCommand()
                .BeginSubCommand("update")
                .WithAlias("u")
                .HandleWith(UpdateCommand)
                .EndSubCommand()
                .BeginSubCommand("radius")
                .WithAlias("r")
                .WithArgs(api.ChatCommands.Parsers.OptionalInt("radius", -1))
                .HandleWith(RadiusCommand)
                .EndSubCommand()
                .BeginSubCommand("colorAid")
                .WithAlias("ca")
                .HandleWith(UpdateColorAid)
                .EndSubCommand();


            _api.Input.RegisterHotKey(
                "togglelightlevels", // Unique code
                "Toggle Light Levels", // Friendly name
                GlKeys.F7 // Default key
            );

            _api.Input.SetHotKeyHandler("togglelightlevels", _ =>
            {
                _painter.Toggle();
                return true;
            });
        }

        private void UpdateConfig()
        {
            try
            {
                var fromDisk = _api.LoadModConfig<EllConfig>(ConfigFilename);
                _config = fromDisk ?? new EllConfig();
            }
            catch (Exception)
            {
                _api.Logger.Warning("Easy Light Levels Warning: Config file had errors, using default.");
                _config = new EllConfig();
            }

            //rewrite to config file to show defaults if ShowDefaults was changed
            _api.StoreModConfig(_config, ConfigFilename);
        }

        private TextCommandResult HelpCommand(TextCommandCallingArgs args)
        {
            _api.ShowChatMessage("ELL command help:");
            _api.ShowChatMessage("Use '.lightlvl' to toggle light levels on and off.");
            _api.ShowChatMessage(
                "Use '.lightlvl update' to update the configuration file and load new changes to it into the game.");
            _api.ShowChatMessage("Use '.lightlvl radius' to show or set the radius.");
            _api.ShowChatMessage("If the light levels won't update or go away, try '.lightlvl abort'.");
            return TextCommandResult.Success(null);
        }

        private TextCommandResult AbortCommand(TextCommandCallingArgs args)
        {
            _painter.Stop();
            return TextCommandResult.Success("Aborted ELL.");
        }

        private TextCommandResult UpdateCommand(TextCommandCallingArgs args)
        {
            UpdateConfig();
            return TextCommandResult.Success("Updated ELL configuration from file.");
        }

        private TextCommandResult UpdateColorAid(TextCommandCallingArgs args)
        {
            _config.ColorContrast.Value = !_config.ColorContrast.Value;
            _api.StoreModConfig(_config, ConfigFilename);
            return TextCommandResult.Success("Set colour aid mode to " + _config.ColorContrast.Value);
        }

        private TextCommandResult RadiusCommand(TextCommandCallingArgs args)
        {
            var rad = (int)args[0];
            if (rad == -1) return TextCommandResult.Success("Current Radius: " + _config.Radius.Value);
            if (rad < 1)
                return TextCommandResult.Error(rad + " is not a valid radius. It must be a number greater than 0.");

            _config.Radius.Value = rad;
            _api.StoreModConfig(_config, ConfigFilename);
            return TextCommandResult.Success("Radius Saved as " + _config.Radius.Value + ".");
        }

        private TextCommandResult ToggleCommand(TextCommandCallingArgs callArgs)
        {
            _painter.Toggle();
            return TextCommandResult.Success(null);
        }
        
    }
}