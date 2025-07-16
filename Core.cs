using System;
using System.Collections.Generic;
using System.Threading;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace easylightlevels
{
    internal class EllCoreSystem : ModSystem
    {
        private const string ConfigFilename = "EasyLightLevelsConfig.json";
        private ICoreClientAPI _api;

        private EllConfig _config;
        private bool _isOn;

        private Thread _opThread;

        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Client;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            if (!ShouldLoad(api.Side)) return;

            _api = api;

            UpdateConfig();

            api.ChatCommands.Create("lightlvl")
                .WithDescription("See light levels.")
                .WithAlias("lightlevel")
                .HandleWith(Command)
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
                ToggleRun();
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
            if (_isOn)
            {
                ToggleRun();
                if (_opThread.IsAlive) _opThread.Abort();
                _api.World.HighlightBlocks(_api.World.Player, 5229, new List<BlockPos>());
            }

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

        private TextCommandResult Command(TextCommandCallingArgs callArgs)
        {
            ToggleRun();
            return TextCommandResult.Success(null);
        }

        private void ToggleRun()
        {
            if (!_isOn)
            {
                _isOn = true;
                _opThread = new Thread(Run)
                {
                    IsBackground = true,
                    Name = "EasyLightLevelsOperator"
                };
                _opThread.Start();
            }
            else
            {
                _isOn = false;
            }
        }

        private bool IsAir(int x, int y, int z)
        {
            return 0.Equals(_api.World.BlockAccessor.GetBlockId(new BlockPos(x, y, z)));
        }

        private bool IsSolid(int x, int y, int z)
        {
            var cb = _api.World.BlockAccessor.GetBlock(new BlockPos(x, y, z)).CollisionBoxes;
            return !(cb == null || cb.Length == 0);
        }

        private int GetRadius()
        {
            return _config.AsSphere.Value ? _config.Radius.Value + 1 : _config.Radius.Value;
        }

        private void Run()
        {
            var posList = new List<BlockPos>();
            var colorList = new List<int>();

            while (_isOn)
            {
                Thread.Sleep(100);
                try
                {
                    var rad = GetRadius();
                    var player = _api.World.Player;
                    var pPos = player.Entity.Pos.AsBlockPos;

                    posList.Clear();
                    colorList.Clear();

                    var minX = pPos.X - rad;
                    var maxX = pPos.X + rad;
                    var minY = pPos.Y - rad;
                    var maxY = pPos.Y + rad;
                    var minZ = pPos.Z - rad;
                    var maxZ = pPos.Z + rad;

                    var asSphere = _config.AsSphere.Value;
                    var radSquared = rad * rad;

                    for (var x = minX; x <= maxX; x++)
                    for (var y = minY; y <= maxY; y++)
                    for (var z = minZ; z <= maxZ; z++)
                    {
                        if (IsAir(x, y, z)) continue;
                        if (!IsSolid(x, y, z)) continue;

                        if (asSphere)
                        {
                            var dx = x - pPos.X;
                            var dy = y - pPos.Y;
                            var dz = z - pPos.Z;
                            if (dx * dx + dy * dy + dz * dz > radSquared) continue;
                        }

                        if (IsSolid(x, y + 1, z)) continue;

                        var bPos = new BlockPos(x, y, z);
                        posList.Add(bPos);
                        colorList.Add(GetColor(bPos.UpCopy()));
                    }

                    _api.Event.EnqueueMainThreadTask(
                        () => _api.World.HighlightBlocks(player, 5229, posList, colorList),
                        "EasyLightLevels");
                }
                catch (ThreadAbortException)
                {
                    Thread.ResetAbort();
                    break;
                }
                catch
                {
                }
            }

            _api.Event.EnqueueMainThreadTask(
                () => _api.World.HighlightBlocks(_api.World.Player, 5229, new List<BlockPos>()),
                "EasyLightLevels");
        }

        private int GetColor(BlockPos bPos)
        {
            var blockLightType = _api.World.BlockAccessor.GetLightLevel(bPos, EnumLightLevelType.OnlyBlockLight);
            var sunLightType = _api.World.BlockAccessor.GetLightLevel(bPos, EnumLightLevelType.OnlySunLight);

            var isColourAid = _config.ColorContrast.Value;

            if (blockLightType >= 8 && sunLightType >= 8)
                //no colour
                return ColorUtil.ToRgba(0, 0, 0, 0);

            if (blockLightType < 8 && sunLightType >= 8)
                //cyan(colourAid) or yellow

                return isColourAid ? ColorUtil.ToRgba(32, 255, 255, 0) : ColorUtil.ToRgba(32, 0, 255, 255);

            if (blockLightType < 8 && sunLightType < 8)
                //blue(colourAid) or red
                return isColourAid ? ColorUtil.ToRgba(32, 255, 0, 0) : ColorUtil.ToRgba(32, 0, 0, 255);


            // not reachable
            return ColorUtil.ToRgba(0, 0, 0, 0);
        }
    }
}