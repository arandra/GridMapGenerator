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
        private readonly Vector2Int leftBlockedRange;
        private readonly Vector2Int rightBlockedRange;
        private readonly bool lockSides;
        private readonly Vector2 minMaxWidthRatio;
        private readonly float widthChangeProbability;
        private readonly float widthSmoothing;
        private readonly float widthJitterPercent;
        private readonly float curvatureLevel;
        private readonly Seeds seeds;

        /// <param name="minCorridorWidth">통로 최소 폭 (타일 개수)</param>
        /// <param name="holdRowsPerStep">통로 시작 위치를 유지할 최소 행 수 (굴곡의 완만함)</param>
        /// <param name="maxShiftPerStep">좌우로 이동 가능한 최대 타일 수(1이면 한 칸씩)</param>
        /// <param name="leftBlockedRange">좌측 막힘 최소/최대 폭</param>
        /// <param name="rightBlockedRange">우측 막힘 최소/최대 폭</param>
        /// <param name="lockSides">좌우 막힘 폭을 동일하게 적용할지 여부</param>
        /// <param name="minMaxWidthRatio">통로 최소/최대 폭 비율(0~1)</param>
        /// <param name="widthChangeProbability">행마다 폭 변경 시도 확률</param>
        /// <param name="widthSmoothing">폭 변경을 서서히 적용하는 정도(0=즉시)</param>
        /// <param name="widthJitterPercent">폭 변경 시 추가 변동 비율</param>
        /// <param name="curvatureLevel">곡률 수준(0=직선, 1=많이 휨)</param>
        /// <param name="seeds">결정적 생성용 시드</param>
        public ScrollingCorridorModule(
            int minCorridorWidth,
            int holdRowsPerStep,
            int maxShiftPerStep,
            Vector2Int leftBlockedRange,
            Vector2Int rightBlockedRange,
            bool lockSides,
            Vector2 minMaxWidthRatio,
            float widthChangeProbability,
            float widthSmoothing,
            float widthJitterPercent,
            float curvatureLevel,
            Seeds seeds)
        {
            this.minCorridorWidth = Mathf.Max(1, minCorridorWidth);
            this.holdRowsPerStep = Mathf.Max(1, holdRowsPerStep);
            this.maxShiftPerStep = Mathf.Max(0, maxShiftPerStep);
            this.leftBlockedRange = SanitizeRange(leftBlockedRange);
            this.rightBlockedRange = SanitizeRange(rightBlockedRange);
            this.lockSides = lockSides;
            this.minMaxWidthRatio = new Vector2(
                Mathf.Clamp01(Mathf.Min(minMaxWidthRatio.x, minMaxWidthRatio.y)),
                Mathf.Clamp01(Mathf.Max(minMaxWidthRatio.x, minMaxWidthRatio.y)));
            this.widthChangeProbability = Mathf.Clamp01(widthChangeProbability);
            this.widthSmoothing = Mathf.Clamp01(widthSmoothing);
            this.widthJitterPercent = Mathf.Clamp01(widthJitterPercent);
            this.curvatureLevel = Mathf.Clamp01(curvatureLevel);
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
                var (leftBlocked, rightBlocked) = SampleBlocked(random, width);
                int maxAvailableWidth = Mathf.Max(1, width - leftBlocked - rightBlocked);

                if (random.NextDouble() <= widthChangeProbability)
                {
                    corridorWidth = PickTargetWidth(random, width, maxAvailableWidth, corridorWidth);
                }

                corridorWidth = Mathf.Clamp(corridorWidth, 1, maxAvailableWidth);

                int maxShift = GetEffectiveMaxShift();
                if (holdCounter <= 0)
                {
                    if (maxShift > 0 && random.NextDouble() <= curvatureLevel)
                    {
                        int delta = random.Next(-maxShift, maxShift + 1);
                        corridorStart = Mathf.Clamp(
                            corridorStart + delta,
                            leftBlocked,
                            Mathf.Max(leftBlocked, width - corridorWidth - rightBlocked));
                    }

                    holdCounter = holdRowsPerStep;
                }
                else
                {
                    holdCounter--;
                }

                corridorStart = Mathf.Clamp(
                    corridorStart,
                    leftBlocked,
                    Mathf.Max(leftBlocked, width - corridorWidth - rightBlocked));

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

        private (int left, int right) SampleBlocked(System.Random random, int gridWidth)
        {
            int left = RandomInRange(random, leftBlockedRange);
            int right = lockSides ? left : RandomInRange(random, rightBlockedRange);

            int maxAllowed = Mathf.Max(0, gridWidth - 1);
            left = Mathf.Clamp(left, 0, maxAllowed);
            right = Mathf.Clamp(right, 0, maxAllowed - left);
            return (left, right);
        }

        private int PickTargetWidth(System.Random random, int gridWidth, int maxAvailableWidth, int currentWidth)
        {
            float ratio = Mathf.Lerp(minMaxWidthRatio.x, minMaxWidthRatio.y, (float)random.NextDouble());
            int targetWidth = Mathf.Max(1, Mathf.RoundToInt(gridWidth * ratio));
            int minAllowed = Mathf.Min(minCorridorWidth, maxAvailableWidth);
            targetWidth = Mathf.Clamp(targetWidth, minAllowed, maxAvailableWidth);

            float jitter = 1f + (((float)random.NextDouble() * 2f - 1f) * widthJitterPercent);
            targetWidth = Mathf.Clamp(Mathf.RoundToInt(targetWidth * jitter), 1, maxAvailableWidth);

            float smoothingStep = Mathf.Lerp(1f, 0.15f, widthSmoothing);
            targetWidth = Mathf.RoundToInt(Mathf.Lerp(currentWidth, targetWidth, smoothingStep));
            return Mathf.Clamp(targetWidth, 1, maxAvailableWidth);
        }

        private int GetEffectiveMaxShift()
        {
            if (maxShiftPerStep <= 0 || curvatureLevel <= 0f)
            {
                return 0;
            }

            // 곡률이 높을수록 이동량을 최대치에 가깝게 허용
            int scaled = Mathf.RoundToInt(Mathf.Lerp(1f, maxShiftPerStep, curvatureLevel));
            return Mathf.Clamp(scaled, 1, maxShiftPerStep);
        }

        private static int RandomInRange(System.Random random, Vector2Int range)
        {
            int min = Mathf.Min(range.x, range.y);
            int max = Mathf.Max(range.x, range.y);
            return random.Next(min, max + 1);
        }

        private static Vector2Int SanitizeRange(Vector2Int range)
        {
            return new Vector2Int(
                Mathf.Max(0, Mathf.Min(range.x, range.y)),
                Mathf.Max(0, Mathf.Max(range.x, range.y)));
        }
    }
}
