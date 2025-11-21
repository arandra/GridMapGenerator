using Codex.GridMapGenerator;
using UnityEngine;

namespace Codex.GridMapGenerator.Modules
{
    /// <summary>
    /// 지정된 GridMeta 정보를 기반으로 일방향 스트립 형태의 그리드를 만든다.
    /// </summary>
    public sealed class OneDirectionalStripGridModule : IGridShapeModule
    {
        private readonly GridMeta _meta;
        private readonly Seeds _seeds;
        private readonly ConstraintsInfo _constraints;

        public OneDirectionalStripGridModule(GridMeta meta, Seeds seeds, ConstraintsInfo constraints = null)
        {
            _meta = meta;
            _seeds = seeds;
            _constraints = constraints ?? new ConstraintsInfo();
        }

        public GridModuleStage Stage => GridModuleStage.Shape;

        public GridContext CreateContext()
        {
            return new GridContext(_meta, _seeds, _constraints);
        }

        public void Process(GridContext context)
        {
            // 1단계 모듈은 셀 초기화 이후 별도 로직이 필요 없을 수 있지만,
            // 기본값을 명시적으로 설정해 두면 이후 모듈 작성이 쉬워진다.
            foreach (var (coords, cell) in context.EnumerateCells())
            {
                cell.Terrain.TerrainType = TerrainType.Unknown;
                cell.Usage.Channels = UsageChannel.None;
                cell.Detail.VariantIndex = -1;
                cell.Detail.DetailNoise = 0f;
            }
        }
    }

    /// <summary>
    /// Perlin 노이즈를 활용해 Terrain 레이어를 채운다.
    /// </summary>
    public sealed class FlatTerrainModule : IGridGenerationModule
    {
        private readonly float _scale;

        public FlatTerrainModule(float scale = 0.1f)
        {
            _scale = Mathf.Max(0.001f, scale);
        }

        public GridModuleStage Stage => GridModuleStage.Generation;

        public void Process(GridContext context)
        {
            var random = context.Seeds.CreateGlobalRandom();
            var offset = new Vector2(random.Next(-1000, 1000), random.Next(-1000, 1000));

            foreach (var (coords, cell) in context.EnumerateCells())
            {
                var noise = Mathf.PerlinNoise(
                    (coords.x + offset.x) * _scale,
                    (coords.y + offset.y) * _scale);

                cell.Terrain.TerrainNoise = noise;
                cell.Terrain.TerrainType = noise switch
                {
                    < 0.3f => TerrainType.Water,
                    < 0.55f => TerrainType.Plain,
                    < 0.8f => TerrainType.Hill,
                    _ => TerrainType.Mountain
                };
            }
        }
    }

    /// <summary>
    /// Terrain 레이어를 기반으로 Usage 채널을 생성한다.
    /// </summary>
    public sealed class BasicUsageModule : IGridGenerationModule
    {
        public GridModuleStage Stage => GridModuleStage.Generation;

        public void Process(GridContext context)
        {
            foreach (var (_, cell) in context.EnumerateCells())
            {
                var channels = UsageChannel.None;
                switch (cell.Terrain.TerrainType)
                {
                    case TerrainType.Plain:
                    case TerrainType.Hill:
                        channels |= UsageChannel.Walkable;
                        break;
                    case TerrainType.Mountain:
                        channels |= UsageChannel.Blocking;
                        break;
                    case TerrainType.Water:
                        channels |= UsageChannel.Blocking;
                        break;
                    default:
                        channels = UsageChannel.None;
                        break;
                }

                cell.Usage.Channels = channels;
            }
        }
    }

    /// <summary>
    /// Variant 인덱스와 태그를 채워 간단한 디테일 레이어를 만든다.
    /// </summary>
    public sealed class SimpleVariantModule : IGridGenerationModule
    {
        public GridModuleStage Stage => GridModuleStage.Generation;

        public void Process(GridContext context)
        {
            var random = context.Seeds.CreateLocalRandom();

            foreach (var (_, cell) in context.EnumerateCells())
            {
                cell.Detail.VariantIndex = random.Next(0, 4);
                cell.Detail.DetailNoise = (float)random.NextDouble();

                cell.Detail.EnsureTags();
                cell.Detail.Tags.Clear();
                if (cell.Terrain.TerrainType == TerrainType.Water)
                {
                    cell.Detail.Tags.Add("water");
                }
                else if (cell.Terrain.TerrainType == TerrainType.Mountain)
                {
                    cell.Detail.Tags.Add("cliff");
                }
                else
                {
                    cell.Detail.Tags.Add("ground");
                }
            }
        }
    }

    /// <summary>
    /// Entry/Exit 포인트 사이를 따라 walkable 경로를 강제한다.
    /// </summary>
    public sealed class ConnectivityConstraintModule : IGridConstraintModule
    {
        public GridModuleStage Stage => GridModuleStage.Constraint;

        public void Process(GridContext context)
        {
            if (!context.Constraints.RequireConnectivity)
            {
                return;
            }

            var start = ClampToGrid(context, context.Constraints.EntryPoint);
            var end = ClampToGrid(context, context.Constraints.ExitPoint);

            var current = start;
            while (current != end)
            {
                ForceWalkable(context, current);

                if (current.x < end.x) current.x++;
                else if (current.x > end.x) current.x--;
                else if (current.y < end.y) current.y++;
                else current.y--;
            }

            ForceWalkable(context, end);

            foreach (var checkpoint in context.Constraints.Checkpoints)
            {
                ForceWalkable(context, ClampToGrid(context, checkpoint));
            }
        }

        private static Vector2Int ClampToGrid(GridContext context, Vector2Int point)
        {
            var clamped = new Vector2Int(
                Mathf.Clamp(point.x, 0, context.Meta.Width - 1),
                Mathf.Clamp(point.y, 0, context.Meta.Height - 1));

            return clamped;
        }

        private static void ForceWalkable(GridContext context, Vector2Int position)
        {
            var cell = context[position.x, position.y];
            cell.Terrain.TerrainType = TerrainType.Plain;
            cell.Usage.Channels &= ~UsageChannel.Blocking;
            cell.Usage.AddChannel(UsageChannel.Walkable);
            cell.Detail.EnsureTags();
            if (!cell.Detail.Tags.Contains("path"))
            {
                cell.Detail.Tags.Add("path");
            }
        }
    }
}
