using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EasyLightLevels.Config;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace EasyLightLevels
{
    public class BlockPainter
    {
        private readonly ICoreClientAPI _api;

        private readonly EllConfig _config;

        private Task _paintTask;

        private CancellationTokenSource _tokenSource;

        public BlockPainter(ICoreClientAPI api, EllConfig config)
        {
            _api = api;
            _config = config;
        }

        public void Toggle()
        {
            if (IsActive())
                Stop();
            else
                Start();
        }

        private void Start()
        {
            if (IsActive()) return;

            _tokenSource = new CancellationTokenSource();
            _paintTask = Task.Run(() => PaintLoop(_tokenSource.Token));
        }

        public void Stop()
        {
            if (!IsActive()) return;
            _tokenSource?.Cancel();
            _paintTask.Wait();
            _paintTask = null;

            _api.Event.EnqueueMainThreadTask(
                () => _api.World.HighlightBlocks(_api.World.Player, 5229, new List<BlockPos>()),
                "EasyLightLevels");
        }

        private bool IsActive()
        {
            return _paintTask != null && !_paintTask.IsCompleted;
        }

        private async Task PaintLoop(CancellationToken token)
        {
            var posList = new List<BlockPos>();
            var colorList = new List<int>();
            var rad = GetRadius();
            var radSquared = rad * rad;
            var player = _api.World.Player;
            while (!token.IsCancellationRequested)
                try
                {
                    await Task.Delay(100, token);
                    if (rad != GetRadius())
                    {
                        rad = GetRadius();
                        radSquared = rad * rad;
                    }

                    var pPos = player.Entity.Pos.AsBlockPos;

                    posList.Clear();
                    colorList.Clear();

                    int minX = pPos.X - rad, maxX = pPos.X + rad;
                    int minY = pPos.Y - rad, maxY = pPos.Y + rad;
                    int minZ = pPos.Z - rad, maxZ = pPos.Z + rad;


                    for (var x = minX; x <= maxX; x++)
                    {
                        var dx = x - pPos.X;
                        var dx2 = dx * dx;

                        for (var y = minY; y <= maxY; y++)
                        {
                            var dy = y - pPos.Y;
                            var dy2 = dy * dy;

                            for (var z = minZ; z <= maxZ; z++)
                            {
                                var dz = z - pPos.Z;
                                var dz2 = dz * dz;

                                if (_config.AsSphere.Value && dx2 + dy2 + dz2 > radSquared) continue;

                                var bPos = new BlockPos(x, y, z);

                                if (IsInvalidBlock(bPos)) continue;

                                posList.Add(bPos);
                                colorList.Add(GetColor(bPos.UpCopy()));
                            }
                        }
                    }


                    _api.Event.EnqueueMainThreadTask(
                        () => _api.World.HighlightBlocks(player, 5229, posList, colorList),
                        "EasyLightLevels");
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception)
                {
                    //this is running a lot, so instead of errors stopping the game let's just cut our losses and do it later. worst case it won't update
                }
        }

        private bool IsInvalidBlock(BlockPos pos)
        {
            var block = _api.World.BlockAccessor.GetBlock(pos);
            if (block.Id == 0) return true; // is air
            if (IsNotSolid(block)) return true;
            var blockUpCopy = _api.World.BlockAccessor.GetBlock(pos.UpCopy());
            return !IsNotSolid(blockUpCopy); // block above is solid
        }

        private static bool IsNotSolid(Block block)
        {
            return block.CollisionBoxes == null || block.CollisionBoxes.Length == 0;
        }

        private int GetRadius()
        {
            return _config.AsSphere.Value ? _config.Radius.Value + 1 : _config.Radius.Value;
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