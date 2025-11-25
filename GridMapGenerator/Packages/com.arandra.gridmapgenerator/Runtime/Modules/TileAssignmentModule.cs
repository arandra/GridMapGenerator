using System.Collections.Generic;
using System.Linq;
using GridMapGenerator.Core;
using GridMapGenerator.Data;
using UnityEngine;

namespace GridMapGenerator.Modules
{
    /// <summary>
    /// TileAssignmentRules와 TileSetData를 사용해 비어 있는 Terrain.TypeId를 채우는 Generation 모듈.
    /// </summary>
    public sealed class TileAssignmentModule : IGridGenerationModule
    {
        private readonly TileSetData tileSet;
        private readonly ITileAssignmentStrategyProvider strategyProvider;
        private readonly GenerationModuleOption activeModules;
        private readonly Seeds seeds;
        private ITileAssignmentStrategy strategy;
        private GridContext context;

        public TileAssignmentModule(
            TileSetData tileSet,
            ITileAssignmentStrategyProvider strategyProvider,
            GenerationModuleOption activeModules,
            Seeds seeds)
        {
            this.tileSet = tileSet;
            this.strategyProvider = strategyProvider;
            this.activeModules = activeModules;
            this.seeds = seeds;
        }

        public GridModuleStage Stage => GridModuleStage.Generation;

        public void Process(GridContext context)
        {
            if (strategy == null)
            {
                string error = null;
                strategy = strategyProvider?.CreateStrategy(activeModules, tileSet, out error);
                if (strategy == null)
                {
                    Debug.LogError($"TileAssignmentModule: {error ?? "타일 배정 전략을 만들 수 없습니다."}");
                    return;
                }
            }

            foreach (var (coords, cell) in context.EnumerateCells())
            {
                if (!string.IsNullOrWhiteSpace(cell.Terrain.TypeId))
                {
                    continue;
                }

                if (strategy is ConditionalWeightedStrategy cw)
                {
                    cw.SetContext(context, coords);
                }

                if (strategy.TryPickTile(out var typeId))
                {
                    cell.Terrain.TypeId = typeId;
                }
                else
                {
                    cell.Terrain.TypeId = string.Empty;
                }
            }
        }

    }
}
