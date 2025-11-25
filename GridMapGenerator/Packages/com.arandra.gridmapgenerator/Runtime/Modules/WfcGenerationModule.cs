using System.Collections.Generic;
using System.Linq;
using GridMapGenerator;
using GridMapGenerator.Data;
using GridMapGenerator.Modules;
using UnityEngine;

namespace GridMapGenerator.Modules
{
    /// <summary>
    /// 단순 WFC 스타일로 타일을 결정하고, 모순 시 조커 타일로 해소한다.
    /// </summary>
    public sealed class WfcGenerationModule : IGridGenerationModule
    {
        private readonly WfcTileRules rules;
        private readonly Seeds seeds;

        public WfcGenerationModule(WfcTileRules rules, Seeds seeds)
        {
            this.rules = rules;
            this.seeds = seeds;
        }

        public GridModuleStage Stage => GridModuleStage.Generation;

        public void Process(GridContext context)
        {
            if (rules == null || rules.Tiles == null || rules.Tiles.Count == 0)
            {
                Debug.LogError("WfcGenerationModule: WfcTileRules가 비어 있습니다.");
                return;
            }

            var tiles = rules.Tiles.Where(t => t != null && !string.IsNullOrWhiteSpace(t.TypeId)).ToList();
            if (tiles.Count == 0)
            {
                Debug.LogError("WfcGenerationModule: 사용할 수 있는 타일이 없습니다.");
                return;
            }

            var joker = tiles.FirstOrDefault(t => t.IsJoker);
            var normalTiles = tiles.Where(t => !t.IsJoker && t.Weight > 0f).ToList();
            if (normalTiles.Count == 0 && joker == null)
            {
                Debug.LogError("WfcGenerationModule: 조커를 포함해도 배정 가능한 타일이 없습니다.");
                return;
            }

            var random = seeds.CreateGlobalRandom();
            var candidates = CreateInitialCandidates(context, normalTiles, joker);

            while (true)
            {
                var cellIndex = FindLowestEntropyCell(candidates);
                if (cellIndex < 0)
                {
                    break; // 모든 셀 결정됨
                }

                var candidateList = candidates[cellIndex];
                var pick = Collapse(cellIndex, candidateList, random, joker);
                ApplyChoice(context, candidates, cellIndex, pick, joker);
            }
        }

        private static List<WfcTileRule>[] CreateInitialCandidates(GridContext context, List<WfcTileRule> normalTiles, WfcTileRule joker)
        {
            var all = new List<WfcTileRule>[context.CellCount];
            for (int i = 0; i < context.CellCount; i++)
            {
                // 초깃값: 일반 타일이 우선, 없으면 조커만
                var list = new List<WfcTileRule>();
                if (normalTiles.Count > 0)
                {
                    list.AddRange(normalTiles);
                }
                else if (joker != null)
                {
                    list.Add(joker);
                }

                all[i] = list;
            }

            return all;
        }

        private static int FindLowestEntropyCell(List<WfcTileRule>[] candidates)
        {
            int bestIndex = -1;
            int bestCount = int.MaxValue;

            for (int i = 0; i < candidates.Length; i++)
            {
                var count = candidates[i].Count;
                if (count <= 1)
                {
                    continue;
                }

                if (count < bestCount)
                {
                    bestCount = count;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private WfcTileRule Collapse(int cellIndex, List<WfcTileRule> candidateList, System.Random random, WfcTileRule joker)
        {
            // 조커는 모순 해소용: 비조커가 있으면 비조커만으로 뽑기
            var nonJoker = candidateList.Where(c => !c.IsJoker).ToList();
            var pool = nonJoker.Count > 0 ? nonJoker : candidateList;

            float totalWeight = pool.Sum(c => Mathf.Max(0f, c.Weight));
            if (totalWeight <= 0f)
            {
                return joker ?? pool.First();
            }

            var threshold = (float)(random.NextDouble() * totalWeight);
            float acc = 0f;
            foreach (var c in pool)
            {
                acc += Mathf.Max(0f, c.Weight);
                if (threshold <= acc)
                {
                    return c;
                }
            }

            return pool.Last();
        }

        private void ApplyChoice(
            GridContext context,
            List<WfcTileRule>[] candidates,
            int cellIndex,
            WfcTileRule choice,
            WfcTileRule joker)
        {
            var width = context.Meta.Width;
            int x = cellIndex % width;
            int y = cellIndex / width;

            candidates[cellIndex].Clear();
            candidates[cellIndex].Add(choice);
            context[x, y].Terrain.TypeId = choice.TypeId;

            // 이웃 제약 전파 (상하좌우 동일 규칙)
            var neighbors = GetNeighbors(context, x, y);
            foreach (var (nx, ny) in neighbors)
            {
                var idx = ny * width + nx;
                var list = candidates[idx];

                bool removed = list.RemoveAll(rule => !choice.AllowsNeighbor(rule.TypeId)) > 0;
                if (list.Count == 0)
                {
                    // 모순 해소: 조커만 남김
                    if (joker != null)
                    {
                        list.Add(joker);
                    }
                    else
                    {
                        Debug.LogError($"WFC: ({nx},{ny})에서 후보가 사라졌고 조커가 없습니다.");
                    }
                }

                // 조커만 남았을 때는 다음 루프에서 우선적으로 선택되도록 한다(엔트로피가 1이므로 건너뜀)
            }
        }

        private static IEnumerable<(int x, int y)> GetNeighbors(GridContext context, int x, int y)
        {
            if (x > 0) yield return (x - 1, y);
            if (x < context.Meta.Width - 1) yield return (x + 1, y);
            if (y > 0) yield return (x, y - 1);
            if (y < context.Meta.Height - 1) yield return (x, y + 1);
        }
    }
}
