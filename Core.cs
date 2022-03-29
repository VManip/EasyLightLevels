using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Vintagestory.API.Common;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace easylightlevels
{
    class ELLCoreSystem : ModSystem
    {
        public override bool ShouldLoad(EnumAppSide side) => side == EnumAppSide.Client;

        public override void StartClientSide(ICoreClientAPI api)
        {

            if (!ShouldLoad(api.Side)) return;

            base.Start(api);

            this.api = api;

            UpdateConfig();

            api.RegisterCommand("lightlvl", "See light levels.", ".lightlvl", new ClientChatCommandDelegate((id, args) => Command(args)));
        }

        Thread opThread;
        bool isOn;
        ICoreClientAPI api;

        ELLConfig config;
        const string configFilename = "EasyLightLevelsConfig.json";
        

        private void UpdateConfig()
		{
            try
			{
                ELLConfig fromDisk = api.LoadModConfig<ELLConfig>(configFilename);
                if (fromDisk == null)
				{
                    //no config file
                    config = new ELLConfig();
				}
                else config = fromDisk;
			}
            catch (Exception)
			{
                api.Logger.Warning("Easy Light Levels Warning: Config file had errors, using default.");
                config = new ELLConfig();
			}
            //rewrite to config file to show defaults if ShowDefaults was changed
            api.StoreModConfig(config, configFilename);
		}

        private void Command(CmdArgs args)
		{

            string arg = args.PopWord();

            if (arg == null)
			{
                ToggleRun();
                return;
			}

            arg = arg.ToLowerInvariant();

            if (arg == "h" || arg == "help")
			{
                api.ShowChatMessage("ELL command help:");
                api.ShowChatMessage("Use '.lightlvl' to toggle light levels on and off.");
                api.ShowChatMessage("Use '.lightlvl update' to update the configuration file and load new changes to it into the game.");
                api.ShowChatMessage("Use '.lightlvl radius' to show or set the radius.");
                api.ShowChatMessage("If the light levels won't update or go away, try '.lightlvl abort'.");
			}
            else if (arg == "abort" || arg == "a")
			{
                if (isOn)
				{
                    ToggleRun();
                    if (opThread.IsAlive) opThread.Abort();
                    api.World.HighlightBlocks(api.World.Player, 5229, new List<BlockPos>());
                }
			}
            else if (arg == "update" || arg == "u") UpdateConfig();

            else if (arg == "r" || arg == "rad" || arg == "radius")
			{
                if (args.PeekWord() == null)
				{
                    api.ShowChatMessage("Current Radius: " + config.Radius.Value.ToString());
                    return;
				}

                int? rad = args.PopInt();
                if (rad == null) api.ShowChatMessage(rad + " is not a valid radius. It must be a number.");
                else
                {
                    config.Radius.Value = (int)rad;
                    api.StoreModConfig(config, configFilename);
                    api.ShowChatMessage("Current Radius: " + config.Radius.Value.ToString());
                }
            }
            else api.ShowChatMessage("ELL command '" + arg + "' unknown.");
		}

        private void ToggleRun()
        {
            if (!isOn)
			{
                isOn = true;
                opThread = new Thread(Run)
                {
                    IsBackground = true,
                    Name = "EasyLightLevelsOperator"
                };
                opThread.Start();
			} 
            else isOn = false;
        }

        private bool IsAir(int x, int y, int z) => api.World.BlockAccessor.GetBlock(x, y, z).Id == 0;

        private bool IsSolid(int x, int y, int z)
		{
            Cuboidf[] cb = api.World.BlockAccessor.GetBlock(x, y, z).CollisionBoxes;
            if (cb == null || cb.Length == 0) return false;
            else return true;
        }

        private int GetRadius() => config.AsSphere.Value ? config.Radius.Value + 1 : config.Radius.Value;

        private void Run()
        {
            while (isOn)
            {
                Thread.Sleep(100);
                try
                {
                    int rad = GetRadius();

                    IClientPlayer player = api.World.Player;

                    BlockPos pPos = player.Entity.Pos.AsBlockPos;

                    List<BlockPos> posList = new List<BlockPos>();
                    List<int> colorList = new List<int>();


                    for (int x = pPos.X - rad; x < pPos.X + rad + 1; x++)
                    {
                        for (int y = pPos.Y - rad; y < pPos.Y + rad + 1; y++)
                        {
                            for (int z = pPos.Z - rad; z < pPos.Z + rad + 1; z++)
                            {
                                if (!IsAir(x, y, z)) //is the block not air?
                                {
                                    if (!IsSolid(x, y, z)) continue; // is the block solid?

                                    if (config.AsSphere.Value)
                                    {
                                        int dx = x - pPos.X, dy = y - pPos.Y, dz = z - pPos.Z;
                                        if ((dx * dx + dy * dy + dz * dz) > (rad * rad)) continue; //is the block in a sphere around us?
                                    }

                                    if (IsSolid(x, y + 1, z)) continue; //does the block have a free top?

                                    //they can spawn on upper horizontal half slabs
                                    //if (!IsFullBlock(x, y, z)) continue; //is the block a full block? creatures can't spawn on e.g. slabs

                                    //if all are true, add it to be highlighted

                                    BlockPos bPos = new BlockPos(x, y, z);

                                    posList.Add(bPos);
                                    colorList.Add(GetColor(bPos.UpCopy()));

                                }
                            }
                        }
                    }

                    api.Event.EnqueueMainThreadTask(new Action(() => api.World.HighlightBlocks(player, 5229, posList, colorList)), "EasyLightLevels");
                }
                catch (ThreadAbortException)
				{
                    Thread.ResetAbort();
                    break;
				}
                catch { } //this is running a lot, so instead of errors stopping the game let's just cut our losses and do it later. worst case it won't update
            }
            api.Event.EnqueueMainThreadTask(new Action(() => api.World.HighlightBlocks(api.World.Player, 5229, new List<BlockPos>())), "EasyLightLevels");
        }

        private int GetColor(BlockPos bPos)
		{
            for (int i = 0; i < config.Rules.Value.Length; i++)
			{
                ELLConfig.Rule rule = config.Rules.Value[i];

                bool matches = false;

                EnumLightLevelType lightType;

                if (rule.LightType.Equals("Block", StringComparison.OrdinalIgnoreCase)) lightType = EnumLightLevelType.OnlyBlockLight;
                else if (rule.LightType.Equals("Sun", StringComparison.OrdinalIgnoreCase)) lightType = EnumLightLevelType.OnlySunLight;
                else throw new Exception("Easy Light Levels: Invalid rule! Light type of rule " + i + " is not 'Block' or 'Sun'!");

                if (rule.OverOrUnder.Equals("Over", StringComparison.OrdinalIgnoreCase))
                {
                    if (api.World.BlockAccessor.GetLightLevel(bPos, lightType) >= rule.LightLevel) matches = true;
                }
                else if (rule.OverOrUnder.Equals("Under", StringComparison.OrdinalIgnoreCase))
                {
                    if (api.World.BlockAccessor.GetLightLevel(bPos, lightType) < rule.LightLevel) matches = true;
                }
                else throw new Exception("Easy Light Levels: Invalid rule! OverOrUnder of rule " + i + " is not 'Over' or 'Under'!");
                
                if (matches)
				{
                    return ColorUtil.ToRgba(
                        rule.Opacity,
                        rule.Blue,
                        rule.Green,
                        rule.Red
                        );
				}
            }

            return ColorUtil.ToRgba(0, 0, 0, 0);

        }

    }

    public class ELLConfig
    {
        public class ConfigItem<T>
        {
            public readonly string Description;

            private T val;
            public T Value
            {
                get => val != null ? val : Default;
                set => val = (value != null) ? value : Default;
            }

            public bool ShowDefault;

            private readonly T TrueDefault;
            public T Default
            {
                get => ShowDefault ? TrueDefault : default;
            }

            public ConfigItem(T Default, string Description, bool ShowDefault = true)
            {
                TrueDefault = Default;
                Value = Default;
                this.Description = Description;
                this.ShowDefault = ShowDefault;
            }
        }

        public class Rule
        {
            public int Red;
            public int Green;
            public int Blue;
            public int Opacity;

            public string LightType;
            public string OverOrUnder;
            public int LightLevel;

            public Rule(int red, int green, int blue, int opacity, string lightType, string overorunder, int level)
            {
                Red = red;
                Green = green;
                Blue = blue;
                Opacity = opacity;
                LightType = lightType;
                LightLevel = level;
                OverOrUnder = overorunder;
            }
        }

        public ConfigItem<int> Radius = new ConfigItem<int>(15,
            "Radius of the shown light levels in blocks, not including your own position. " +
            "At high values, FPS is not impacted, but the light levels will not be updated smoothly."
            );

        public ConfigItem<bool> AsSphere = new ConfigItem<bool>(true,
            "Whether to show light levels in a sphere or in a cube. " + 
            "Set to true to show light levels in a sphere around you. " + 
            "Set to false to show light levels in a cube around you." 
            );

        public ConfigItem<Rule[]> Rules = new ConfigItem<Rule[]>(
            new Rule[] {
                new Rule(0, 255, 0, 32, "Block", "Over", 8),
                new Rule(255, 255, 0, 32, "Sun", "Over", 8),
                new Rule(255, 0, 0, 32, "Sun", "Under", 8)
            }
            , "These are the rules used when highlighting blocks. " + 
            "They are evaluated from top to bottom, meaning a higher rule will override a lower one. " +
            "Red, Green, Blue, and Opacity are color values from 0 to 255. " +
            "LightType is the type of the light you want to check against, 'Block' or 'Sun'. " +
            "LightLevel is the level of light you want to check against. " +
            "OverOrUnder is whether you want the highlight to be applied to blocks over this light level or below this light level. Set to 'Over' or 'Under'. " +
            "Set ShowDefault to true and load the mod (or use '.lightlvl update') to show defaults. " + 
            "If a block does not match any rules it will not be highlighted."
            , false);

	}
}