using System;
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
        private readonly bool respectUsageBlocked;
        private readonly HashSet<string> blockedTypeIds;
        private readonly HashSet<string> unblockedTypeIds;
        private readonly bool restartOnFailure;
        private readonly int maxRetries;
        private readonly bool useNewSeedOnRetry;
        private readonly bool verboseLogging;

        public WfcGenerationModule(
            WfcTileRules rules,
            Seeds seeds,
            bool respectUsageBlocked = false,
            HashSet<string> blockedTypeIds = null,
            HashSet<string> unblockedTypeIds = null,
            bool restartOnFailure = false,
            int maxRetries = 0,
            bool useNewSeedOnRetry = true,
            bool verboseLogging = false)
        {
            this.rules = rules;
            this.seeds = seeds;
            this.respectUsageBlocked = respectUsageBlocked;
            this.blockedTypeIds = blockedTypeIds;
            this.unblockedTypeIds = unblockedTypeIds;
            this.restartOnFailure = restartOnFailure;
            this.maxRetries = Mathf.Max(0, maxRetries);
            this.useNewSeedOnRetry = useNewSeedOnRetry;
            this.verboseLogging = verboseLogging;
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

            var initialTypes = SnapshotTypes(context);
            var attempts = restartOnFailure ? Mathf.Max(1, maxRetries + 1) : 1;
            FailureInfo lastFailure = default;
            var history = verboseLogging ? new List<string>(256) : null;

            for (int attempt = 0; attempt < attempts; attempt++)
            {
                RestoreTypes(context, initialTypes);

                var randomSeed = useNewSeedOnRetry ? seeds.GlobalSeed + attempt : seeds.GlobalSeed;
                var random = new System.Random(randomSeed);
                var candidates = CreateInitialCandidates(context, normalTiles, joker);

                history?.Clear();

                if (RunOnce(context, candidates, random, joker, history, out lastFailure))
                {
                    if (verboseLogging && history != null && history.Count > 0)
                    {
                        Debug.Log(string.Join("\n", history));
                    }
                    return; // 성공
                }

                if (!restartOnFailure)
                {
                    break;
                }

                if (attempt < attempts - 1)
                {
                    Debug.LogWarning($"WFC 실패 재시도 {attempt + 1}/{attempts - 1}: {FormatFailure(lastFailure, attempt, randomSeed)}\n{FormatHistory(history)}");
                }
            }

            Debug.LogError($"WFC 실패: {FormatFailure(lastFailure, attempts - 1, useNewSeedOnRetry ? seeds.GlobalSeed + attempts - 1 : seeds.GlobalSeed)}\n{FormatHistory(history)}");
        }

        private bool RunOnce(
            GridContext context,
            List<WfcTileRule>[] candidates,
            System.Random random,
            WfcTileRule joker,
            List<string> history,
            out FailureInfo failure)
        {
            failure = default;

            while (true)
            {
                var cellIndex = FindLowestEntropyCell(candidates);
                if (cellIndex == -1)
                {
                    return true; // 모든 셀 결정됨
                }
                if (cellIndex == -2)
                {
                    failure = new FailureInfo
                    {
                        CellIndex = -1,
                        SelectedCellIndex = -1,
                        SelectedCoordinates = Vector2Int.zero,
                        SelectedIsBlocked = false,
                        SelectedTypeId = string.Empty,
                        Message = "후보가 0인 셀이 존재합니다.",
                        NeighborType = string.Empty,
                        ChosenType = string.Empty,
                        Coordinates = Vector2Int.zero,
                        CandidateCountBeforeRemoval = 0,
                        InfluenceTypeId = string.Empty,
                        InfluenceIsBlocked = false,
                        InfluenceCandidatesBefore = Array.Empty<string>(),
                        InfluenceCandidatesAfter = Array.Empty<string>(),
                        RemovedCandidates = Array.Empty<string>()
                    };
                    return false;
                }

                var candidateList = candidates[cellIndex];
                if (candidateList == null || candidateList.Count == 0)
                {
                    failure = new FailureInfo
                    {
                        CellIndex = cellIndex,
                        SelectedCellIndex = cellIndex,
                        SelectedCoordinates = ToCoords(context, cellIndex),
                        SelectedIsBlocked = context[cellIndex].Usage.IsBlocked,
                        SelectedTypeId = context[cellIndex].Terrain.TypeId,
                        Message = "후보가 없습니다.",
                        NeighborType = string.Empty,
                        ChosenType = string.Empty,
                        Coordinates = ToCoords(context, cellIndex),
                        CandidateCountBeforeRemoval = 0,
                        InfluenceTypeId = context[cellIndex].Terrain.TypeId,
                        InfluenceIsBlocked = context[cellIndex].Usage.IsBlocked,
                        InfluenceCandidatesBefore = Array.Empty<string>(),
                        InfluenceCandidatesAfter = Array.Empty<string>(),
                        RemovedCandidates = Array.Empty<string>(),
                        SelectedNeighborSummaries = SnapshotNeighborSummaries(context, candidates, cellIndex)
                    };
                    return false;
                }

                var pick = Collapse(cellIndex, candidateList, random, joker);
                var coords = ToCoords(context, cellIndex);
                history?.Add($"선택 {coords}(blocked:{context[cellIndex].Usage.IsBlocked}) => {pick.TypeId}, 후보:{string.Join(",", candidateList.Select(c => c.TypeId))}");
                if (candidateList.Count == 1 && !verboseLogging)
                {
                    Debug.Log($"WFC 자동 확정(엔트로피1) {coords}(blocked:{context[cellIndex].Usage.IsBlocked}) => {pick.TypeId}");
                }

                if (!TryApplyChoice(context, candidates, cellIndex, pick, joker, history, out failure))
                {
                    return false;
                }

                // 결정된 셀은 더 이상 엔트로피 계산 대상에서 제외한다.
                candidates[cellIndex] = null;
            }
        }

        private List<WfcTileRule>[] CreateInitialCandidates(GridContext context, List<WfcTileRule> normalTiles, WfcTileRule joker)
        {
            List<WfcTileRule> blockedPool = null;
            List<WfcTileRule> unblockedPool = null;

            if (respectUsageBlocked)
            {
                blockedPool = normalTiles.Where(t => IsAllowedForBlocked(t.TypeId)).ToList();
                unblockedPool = normalTiles.Where(t => IsAllowedForUnblocked(t.TypeId)).ToList();

                if (blockedPool.Count == 0)
                {
                    Debug.LogWarning("WFC: Usage.IsBlocked=true 셀에 사용할 수 있는 후보가 없습니다. blockedTypeIds를 확인하세요.");
                }

                if (unblockedPool.Count == 0)
                {
                    Debug.LogWarning("WFC: Usage.IsBlocked=false 셀에 사용할 수 있는 후보가 없습니다. unblockedTypeIds를 확인하세요.");
                }

                if (verboseLogging)
                {
                    var blockedList = blockedPool?.Select(t => t.TypeId).ToList() ?? new List<string>();
                    var unblockedList = unblockedPool?.Select(t => t.TypeId).ToList() ?? new List<string>();
                    Debug.Log($"WFC 후보 분리: blocked=[{string.Join(",", blockedList)}], unblocked=[{string.Join(",", unblockedList)}]");
                }
            }

            var all = new List<WfcTileRule>[context.CellCount];
            for (int i = 0; i < context.CellCount; i++)
            {
                var list = new List<WfcTileRule>();

                var source = normalTiles;
                if (respectUsageBlocked)
                {
                    var isBlocked = context[i].Usage.IsBlocked;
                    source = isBlocked ? blockedPool : unblockedPool;
                }

                if (source != null && source.Count > 0)
                {
                    list.AddRange(source);
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
                if (candidates[i] == null)
                {
                    continue;
                }

                var count = candidates[i].Count;
                if (count == 0)
                {
                    // 이미 모순 상태
                    return -2;
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

        private bool TryApplyChoice(
            GridContext context,
            List<WfcTileRule>[] candidates,
            int cellIndex,
            WfcTileRule choice,
            WfcTileRule joker,
            List<string> history,
            out FailureInfo failure)
        {
            failure = default;

            var width = context.Meta.Width;
            int x = cellIndex % width;
            int y = cellIndex / width;

            candidates[cellIndex].Clear();
            candidates[cellIndex].Add(choice);
            context[x, y].Terrain.TypeId = choice.TypeId;

            // 이웃 제약 전파 (방향별 규칙)
            var neighbors = GetNeighbors(context, x, y);
            foreach (var (nx, ny, direction) in neighbors)
            {
                var idx = ny * width + nx;
                var list = candidates[idx];
                if (list == null || list.Count == 0)
                {
                    continue;
                }

                var beforeCount = list.Count;
                var beforeTypesSnapshot = list.Select(r => r.TypeId).ToList();
                bool removed = list.RemoveAll(rule =>
                    !(choice.AllowsNeighbor(rule.TypeId, direction) &&
                      rule.AllowsNeighbor(choice.TypeId, Opposite(direction)))) > 0;
                if (removed && history != null)
                {
                    var afterTypes = string.Join(",", list.Select(r => r.TypeId));
                    history.Add($"전파 {ToCoords(context, cellIndex)}->{(nx, ny)} dir:{direction} removeNotAllowed:{choice.TypeId} beforeCount:{beforeCount} afterCount:{list.Count} after:{afterTypes}");
                }

                if (list.Count == 0)
                {
                    // 모순 해소: 조커만 남김
                    if (joker != null)
                    {
                        list.Add(joker);
                    }
                    else
                    {
                        var removedTypesSnapshot = beforeTypesSnapshot.ToArray();
                        failure = new FailureInfo
                        {
                            CellIndex = idx,
                            SelectedCellIndex = cellIndex,
                            SelectedCoordinates = new Vector2Int(x, y),
                            SelectedIsBlocked = context[cellIndex].Usage.IsBlocked,
                            SelectedTypeId = choice.TypeId,
                            Message = $"이웃 {choice.TypeId} 제약으로 후보가 모두 제거됨",
                            NeighborType = choice.TypeId,
                            ChosenType = choice.TypeId,
                            Coordinates = new Vector2Int(nx, ny),
                            CandidateCountBeforeRemoval = beforeCount,
                            Direction = direction,
                            InfluenceTypeId = context[idx].Terrain.TypeId,
                            InfluenceIsBlocked = context[idx].Usage.IsBlocked,
                            InfluenceCandidatesBefore = removedTypesSnapshot,
                            InfluenceCandidatesAfter = Array.Empty<string>(),
                            RemovedCandidates = removedTypesSnapshot,
                            SelectedNeighborSummaries = SnapshotNeighborSummaries(context, candidates, cellIndex),
                            InfluenceNeighborSummaries = SnapshotNeighborSummaries(context, candidates, idx)
                        };
                        return false;
                    }
                }

                // 조커만 남았을 때는 다음 루프에서 우선적으로 선택되도록 한다(엔트로피가 1이므로 건너뜀)
            }

            return true;
        }

        private static IEnumerable<(int x, int y, WfcDirection direction)> GetNeighbors(GridContext context, int x, int y)
        {
            if (x > 0) yield return (x - 1, y, WfcDirection.Left);
            if (x < context.Meta.Width - 1) yield return (x + 1, y, WfcDirection.Right);
            if (y > 0) yield return (x, y - 1, WfcDirection.Backward);
            if (y < context.Meta.Height - 1) yield return (x, y + 1, WfcDirection.Forward);
        }

        private static WfcDirection Opposite(WfcDirection direction)
        {
            return direction switch
            {
                WfcDirection.Left => WfcDirection.Right,
                WfcDirection.Right => WfcDirection.Left,
                WfcDirection.Forward => WfcDirection.Backward,
                WfcDirection.Backward => WfcDirection.Forward,
                _ => WfcDirection.Left
            };
        }

        private static Vector2Int ToCoords(GridContext context, int index)
        {
            var width = context.Meta.Width;
            int x = index % width;
            int y = index / width;
            return new Vector2Int(x, y);
        }

        private static string[] SnapshotTypes(GridContext context)
        {
            var snapshot = new string[context.CellCount];
            for (int i = 0; i < context.CellCount; i++)
            {
                snapshot[i] = context[i].Terrain.TypeId;
            }
            return snapshot;
        }

        private static void RestoreTypes(GridContext context, string[] snapshot)
        {
            if (snapshot == null) return;
            for (int i = 0; i < context.CellCount && i < snapshot.Length; i++)
            {
                context[i].Terrain.TypeId = snapshot[i];
            }
        }

        private string[] SnapshotNeighborSummaries(GridContext context, List<WfcTileRule>[] candidates, int centerIndex)
        {
            if (centerIndex < 0 || centerIndex >= context.CellCount) return Array.Empty<string>();

            var width = context.Meta.Width;
            int x = centerIndex % width;
            int y = centerIndex / width;

            var summaries = new List<string>();
            foreach (var (nx, ny, direction) in GetNeighbors(context, x, y))
            {
                var idx = ny * width + nx;
                var cell = context[idx];
                var list = candidates[idx];
                var candidateTypes = list == null
                    ? "(decided)"
                    : list.Count == 0
                        ? "(none)"
                        : string.Join(",", list.Select(r => r.TypeId));
                summaries.Add($"[{direction}]({nx},{ny}) type:{cell.Terrain.TypeId} blocked:{cell.Usage.IsBlocked} cand:{candidateTypes}");
            }

            return summaries.ToArray();
        }

        private static string FormatFailure(FailureInfo failure, int attempt, int seed)
        {
            var coordText = failure.Coordinates == Vector2Int.zero && failure.CellIndex < 0
                ? "알 수 없음"
                : $"({failure.Coordinates.x},{failure.Coordinates.y})";
            var selectedText = failure.SelectedCoordinates == Vector2Int.zero && failure.SelectedCellIndex < 0
                ? coordText
                : $"({failure.SelectedCoordinates.x},{failure.SelectedCoordinates.y})";

            var detail = $"시드:{seed}, 시도:{attempt + 1}, 결정셀:{selectedText}, 결정셀Blocked:{failure.SelectedIsBlocked}, 영향셀:{coordText}, 메시지:{failure.Message}";
            if (!string.IsNullOrWhiteSpace(failure.ChosenType))
            {
                detail += $", 선택:{failure.ChosenType}";
            }

            if (failure.CandidateCountBeforeRemoval > 0)
            {
                detail += $", 제거전후:{failure.CandidateCountBeforeRemoval}->0";
            }

            if (!string.IsNullOrWhiteSpace(failure.SelectedTypeId))
            {
                detail += $", 결정셀Type:{failure.SelectedTypeId}";
            }

            if (!string.IsNullOrWhiteSpace(failure.InfluenceTypeId))
            {
                detail += $", 영향셀Type:{failure.InfluenceTypeId}";
            }

            detail += $", 영향셀Blocked:{failure.InfluenceIsBlocked}";
            detail += $", 방향:{failure.Direction}";

            if (failure.InfluenceCandidatesBefore != null && failure.InfluenceCandidatesBefore.Length > 0)
            {
                detail += $", 영향셀후보(전):[{string.Join(",", failure.InfluenceCandidatesBefore)}]";
            }

            if (failure.InfluenceCandidatesAfter != null && failure.InfluenceCandidatesAfter.Length > 0)
            {
                detail += $", 영향셀후보(후):[{string.Join(",", failure.InfluenceCandidatesAfter)}]";
            }

            if (failure.RemovedCandidates != null && failure.RemovedCandidates.Length > 0)
            {
                detail += $", 제거된후보:[{string.Join(",", failure.RemovedCandidates)}]";
            }

            if (failure.SelectedNeighborSummaries != null && failure.SelectedNeighborSummaries.Length > 0)
            {
                detail += $", 결정셀주변:{string.Join(" | ", failure.SelectedNeighborSummaries)}";
            }

            if (failure.InfluenceNeighborSummaries != null && failure.InfluenceNeighborSummaries.Length > 0)
            {
                detail += $", 영향셀주변:{string.Join(" | ", failure.InfluenceNeighborSummaries)}";
            }

            return detail;
        }

        private static string FormatHistory(List<string> history)
        {
            if (history == null || history.Count == 0) return "(no history)";
            return string.Join("\n", history);
        }

        private bool IsAllowedForBlocked(string typeId)
        {
            if (blockedTypeIds == null || blockedTypeIds.Count == 0) return true;
            return blockedTypeIds.Contains(typeId);
        }

        private bool IsAllowedForUnblocked(string typeId)
        {
            if (unblockedTypeIds == null || unblockedTypeIds.Count == 0) return true;
            return unblockedTypeIds.Contains(typeId);
        }

        private struct FailureInfo
        {
            public int CellIndex;
            public Vector2Int Coordinates;
            public int SelectedCellIndex;
            public Vector2Int SelectedCoordinates;
            public bool SelectedIsBlocked;
            public string SelectedTypeId;
            public string Message;
            public string NeighborType;
            public string ChosenType;
            public int CandidateCountBeforeRemoval;
            public string InfluenceTypeId;
            public bool InfluenceIsBlocked;
            public string[] InfluenceCandidatesBefore;
            public string[] InfluenceCandidatesAfter;
            public string[] RemovedCandidates;
            public WfcDirection Direction;
            public string[] SelectedNeighborSummaries;
            public string[] InfluenceNeighborSummaries;
        }
    }
}
