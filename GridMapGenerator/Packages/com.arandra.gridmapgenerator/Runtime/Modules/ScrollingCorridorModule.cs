using GridMapGenerator;
using GridMapGenerator.Core;
using GridMapGenerator.Modules;
using UnityEngine;

namespace GridMapGenerator.Modules
{
    /// <summary>
    /// 스크롤 방향(y축)을 따라 코어 통로를 생성한 뒤 좌우 대칭 여유 폭을 적용해 45도 이하 곡률의 길을 만든다.
    /// </summary>
    public sealed class ScrollingCorridorModule : IGridGenerationModule
    {
        private readonly Vector2Int coreObjectSize;
        private readonly int minCoreWidth;
        private readonly int minHoldRows;
        private readonly int maxLateralStep;
        private readonly Vector2Int symmetricMarginRange;
        private readonly int marginChangeLimit;
        private readonly float difficulty;
        private readonly int initialCenterOffset;
        private readonly bool debugLog;
        private readonly Seeds seeds;

        /// <param name="coreObjectSize">통과해야 할 물체 크기(N=폭, M=높이)</param>
        /// <param name="minCoreWidth">코어(필수 통로) 최소 폭</param>
        /// <param name="minHoldRows">코어 위치를 유지해야 하는 최소 행 수</param>
        /// <param name="maxLateralStep">행당 좌우 최대 이동 칸수(0 또는 1, 45도 제한)</param>
        /// <param name="symmetricMarginRange">코어 좌우에 붙는 대칭 여유 폭의 최소/최대</param>
        /// <param name="marginChangeLimit">행마다 여유 폭이 변할 수 있는 최대 증감량</param>
        /// <param name="difficulty">코스 난이도(0=직선/완만, 1=자주 굽이침)</param>
        /// <param name="initialCenterOffset">코어 시작 위치 오프셋(그리드 중앙 기준 +우측/-좌측)</param>
        /// <param name="debugLog">코어/마진 2패스 결과를 Debug.Log로 출력</param>
        /// <param name="seeds">결정적 생성용 시드</param>
        public ScrollingCorridorModule(
            Vector2Int coreObjectSize,
            int minCoreWidth,
            int minHoldRows,
            int maxLateralStep,
            Vector2Int symmetricMarginRange,
            int marginChangeLimit,
            float difficulty,
            int initialCenterOffset,
            bool debugLog,
            Seeds seeds)
        {
            this.coreObjectSize = new Vector2Int(
                Mathf.Max(1, coreObjectSize.x),
                Mathf.Max(1, coreObjectSize.y));
            this.minCoreWidth = Mathf.Max(1, minCoreWidth);
            this.minHoldRows = Mathf.Max(1, minHoldRows);
            this.maxLateralStep = Mathf.Clamp(maxLateralStep, 0, 1);
            this.symmetricMarginRange = SanitizeRange(symmetricMarginRange);
            this.marginChangeLimit = Mathf.Max(0, marginChangeLimit);
            this.difficulty = Mathf.Clamp01(difficulty);
            this.initialCenterOffset = initialCenterOffset;
            this.debugLog = debugLog;
            this.seeds = seeds;
        }

        public GridModuleStage Stage => GridModuleStage.Generation;

        public void Process(GridContext context)
        {
            int width = context.Meta.Width;
            int height = context.Meta.Height;
            int coreWidth = Mathf.Clamp(
                Mathf.Max(coreObjectSize.x, minCoreWidth),
                1,
                width);
            int holdRows = Mathf.Max(coreObjectSize.y, minHoldRows);

            int maxMarginAllowed = Mathf.Max(0, (width - coreWidth) / 2);
            int marginMin = Mathf.Clamp(symmetricMarginRange.x, 0, maxMarginAllowed);
            int marginMax = Mathf.Clamp(symmetricMarginRange.y, marginMin, maxMarginAllowed);

            var random = seeds.CreateGlobalRandom();
            System.Text.StringBuilder logBuilder = debugLog ? new System.Text.StringBuilder(4096) : null;

            // Pass A: 방향/마진 시퀀스 생성(난수 기반, 확률 의존 제거)
            var dirSeq = new int[height];
            var marginNoise = new float[height];
            for (int y = 0; y < height; y++)
            {
                dirSeq[y] = SampleDirection(random);
                marginNoise[y] = (float)random.NextDouble() * 2f - 1f; // [-1,1]
            }

            // Pass B: 누적 경로와 마진(난이도 1 기준)
            var baseCenters = new float[height];
            float centerMid = Mathf.Clamp((width - coreWidth) / 2f + initialCenterOffset, 0, width - coreWidth);
            float cumulative = 0f;
            for (int y = 0; y < height; y++)
            {
                cumulative += dirSeq[y];
                baseCenters[y] = centerMid + cumulative;
            }

            var baseMargins = new float[height];
            float halfRange = (marginMax - marginMin) * 0.5f;
            for (int y = 0; y < height; y++)
            {
                float target = marginMin + halfRange + marginNoise[y] * halfRange;
                baseMargins[y] = Mathf.Clamp(target, marginMin, marginMax);
            }

            // Pass C: 진폭 정규화 후 난이도 스케일 (1=원본, 0=평탄)
            float diffT = Mathf.Clamp01(difficulty);
            NormalizeAndScale(baseCenters, centerMid, width - coreWidth, diffT);
            NormalizeAndScale(baseMargins, marginMin + halfRange, marginMax, diffT);

            // Pass D: 스냅 & 45도 제한 적용 + 타일 설정
            int snappedCenter = Mathf.RoundToInt(centerMid);
            int snappedMargin = Mathf.RoundToInt(baseMargins[0]);

            for (int y = 0; y < height; y++)
            {
                snappedCenter = StepTowardsDesired(snappedCenter, baseCenters[y], diffT);
                snappedCenter = Mathf.Clamp(snappedCenter, 0, width - coreWidth);

                snappedMargin = MoveMarginTowards(snappedMargin, Mathf.RoundToInt(baseMargins[y]), Mathf.Max(1, marginChangeLimit));
                int adjustedMaxMargin = Mathf.Max(0, (width - coreWidth) / 2);
                snappedMargin = Mathf.Clamp(snappedMargin, marginMin, Mathf.Min(marginMax, adjustedMaxMargin));

                int corridorStart = Mathf.Clamp(snappedCenter - snappedMargin, 0, width - 1);
                int corridorWidth = Mathf.Clamp(coreWidth + snappedMargin * 2, coreWidth, width);
                corridorWidth = Mathf.Max(corridorWidth, coreObjectSize.x);
                corridorWidth = Mathf.Clamp(corridorWidth, coreWidth, width);
                corridorStart = Mathf.Clamp(corridorStart, 0, width - corridorWidth);
                int corridorEnd = corridorStart + corridorWidth;

                for (int x = 0; x < width; x++)
                {
                    bool insideCorridor = x >= corridorStart && x < corridorEnd;
                    var cell = context[x, y];
                    cell.Usage.IsBlocked = !insideCorridor;

                    cell.Detail.EnsureTags();
                    if (insideCorridor && !cell.Detail.Tags.Contains("corridor"))
                    {
                        cell.Detail.Tags.Add("corridor");
                    }
                }

                if (logBuilder != null && logBuilder.Length < 3600)
                {
                    logBuilder
                        .Append("P y=").Append(y)
                        .Append(" dir=").Append(dirSeq[y])
                        .Append(" baseC=").Append(baseCenters[y].ToString("F2"))
                        .Append(" baseM=").Append(baseMargins[y].ToString("F2"))
                        .Append(" snapC=").Append(snappedCenter)
                        .Append(" snapM=").Append(snappedMargin)
                        .Append(" width=").Append(corridorWidth)
                        .AppendLine();
                }
            }

            if (logBuilder != null && logBuilder.Length > 0)
            {
                if (logBuilder.Length > 3900)
                {
                    logBuilder.Length = 3900;
                    logBuilder.AppendLine("...(truncated)");
                }

                Debug.Log($"[ScrollingCorridor Debug] W:{width} H:{height} Diff:{difficulty:F2}\n{logBuilder}");
            }
        }

        private int SampleDirection(System.Random random)
        {
            if (maxLateralStep <= 0)
            {
                return 0;
            }

            // 난이도 1 기준 큰 굴곡을 위해 -1/0/1 비율을 넓게 배분
            double r = random.NextDouble();
            if (r < 0.35) return -1;
            if (r < 0.65) return 0;
            return 1;
        }

        private int SampleHoldLength(System.Random random, int baseHold)
        {
            // 방향 시퀀스가 직접 굴곡을 결정하므로 hold는 1로 고정
            return 1;
        }

        private static int StepTowardsDesired(int current, float desired, float difficulty)
        {
            int target = Mathf.RoundToInt(desired);
            int delta = Mathf.Clamp(target - current, -1, 1); // 45도 제한: 한 행에 최대 ±1
            return current + delta;
        }

        private static int MoveMarginTowards(int current, int target, int maxStep)
        {
            if (maxStep <= 0)
            {
                return current;
            }

            int delta = Mathf.Clamp(target - current, -maxStep, maxStep);
            return current + delta;
        }

        private static Vector2Int SanitizeRange(Vector2Int range)
        {
            return new Vector2Int(
                Mathf.Max(0, Mathf.Min(range.x, range.y)),
                Mathf.Max(0, Mathf.Max(range.x, range.y)));
        }

        private static void NormalizeAndScale(float[] values, float mid, float maxSpan, float difficulty)
        {
            if (values == null || values.Length == 0)
            {
                return;
            }

            float min = float.MaxValue;
            float max = float.MinValue;
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] < min) min = values[i];
                if (values[i] > max) max = values[i];
            }

            float halfSpan = Mathf.Max(0.0001f, (max - min) * 0.5f);
            float scale = Mathf.Clamp(maxSpan * 0.5f / halfSpan, 0f, maxSpan);
            float targetScale = Mathf.Lerp(0f, scale, difficulty);

            float center = (min + max) * 0.5f;
            for (int i = 0; i < values.Length; i++)
            {
                float offset = values[i] - center;
                values[i] = mid + offset * targetScale;
            }
        }
    }
}
