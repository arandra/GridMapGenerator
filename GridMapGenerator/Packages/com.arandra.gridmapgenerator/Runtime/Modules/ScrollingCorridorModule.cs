using GridMapGenerator;
using GridMapGenerator.Core;
using GridMapGenerator.Modules;
using UnityEngine;

namespace GridMapGenerator.Modules
{
    /// <summary>
    /// 스크롤 방향(y축)을 따라 최소 통로 폭을 보장하면서 좌우 막힘 폭을 변동시켜 굴곡진 길을 만든다.
    /// </summary>
    public sealed class ScrollingCorridorModule : IGridGenerationModule
    {
        private readonly int minCorridorWidth;
        private readonly int holdRowsPerStep;
        private readonly int maxShiftPerStep;
        private readonly Seeds seeds;

        /// <param name="minCorridorWidth">통로 최소 폭 (타일 개수)</param>
        /// <param name="holdRowsPerStep">통로 시작 위치를 유지할 최소 행 수 (굴곡의 완만함)</param>
        /// <param name="maxShiftPerStep">좌우로 이동 가능한 최대 타일 수(1이면 한 칸씩)</param>
        /// <param name="seeds">결정적 생성용 시드</param>
        public ScrollingCorridorModule(
            int minCorridorWidth,
            int holdRowsPerStep,
            int maxShiftPerStep,
            Seeds seeds)
        {
            this.minCorridorWidth = Mathf.Max(1, minCorridorWidth);
            this.holdRowsPerStep = Mathf.Max(1, holdRowsPerStep);
            this.maxShiftPerStep = Mathf.Max(0, maxShiftPerStep);
            this.seeds = seeds;
        }

        public GridModuleStage Stage => GridModuleStage.Generation;

        public void Process(GridContext context)
        {
            int width = context.Meta.Width;
            int height = context.Meta.Height;
            int corridorWidth = Mathf.Clamp(minCorridorWidth, 1, width);

            int corridorStart = Mathf.Max(0, (width - corridorWidth) / 2);
            int holdCounter = 0;
            var random = seeds.CreateGlobalRandom();

            for (int y = 0; y < height; y++)
            {
                if (holdCounter <= 0)
                {
                    if (maxShiftPerStep > 0)
                    {
                        int delta = random.Next(-maxShiftPerStep, maxShiftPerStep + 1);
                        corridorStart = Mathf.Clamp(corridorStart + delta, 0, width - corridorWidth);
                    }

                    holdCounter = holdRowsPerStep;
                }
                else
                {
                    holdCounter--;
                }

                int corridorEnd = corridorStart + corridorWidth;

                for (int x = 0; x < width; x++)
                {
                    bool insideCorridor = x >= corridorStart && x < corridorEnd;
                    var cell = context[x, y];
                    cell.Usage.IsBlocked = !insideCorridor;

                    // 통로 폭 보장을 위해 Detail 태그로 힌트를 남겨 타일 배정 전략이 활용할 수 있게 한다.
                    cell.Detail.EnsureTags();
                    if (insideCorridor && !cell.Detail.Tags.Contains("corridor"))
                    {
                        cell.Detail.Tags.Add("corridor");
                    }
                }
            }
        }
    }
}
