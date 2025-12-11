using System;
using System.Collections.Generic;
using System.Linq;
using GridMapGenerator;
using GridMapGenerator.Core;
using GridMapGenerator.Modules;
using UnityEngine;

namespace GridMapGenerator.Data
{
    [CreateAssetMenu(fileName = "TileAssignmentRules", menuName = "Grid Map Generator/Tile Assignment Rules")]
    public sealed class TileAssignmentRules : ScriptableObject, ITileAssignmentStrategyProvider
    {
        [Header("Editor")]
        [Tooltip("에디터 팝업에서 TypeId를 선택할 때 사용할 TileSet. 비워두면 문자열 입력으로 fallback.")]
        public TileSetData EditorTileSet;

        [Tooltip("Generation 모듈별로 적용할 타일 배정 규칙 목록입니다. 첫 번째로 매칭되는 규칙이 사용됩니다.")]
        public List<ModuleTileRule> ModuleRules = new();

        [Tooltip("조건부 규칙: 모듈 플래그/막힘 여부 기반으로 Override TypeId를 지정합니다.")]
        public List<ConditionalTileRule> ConditionalRules = new();

        public bool TrySelectRule(
            GenerationModuleOption activeModules,
            TileSetData tileSet,
            out ModuleTileRule rule,
            out string error)
        {
            rule = null;
            error = null;

            if (tileSet == null)
            {
                error = "TileSetData가 없습니다.";
                return false;
            }

            var match = ModuleRules.FirstOrDefault(r => r != null && r.Matches(activeModules));
            if (match == null)
            {
                error = "활성화된 Generation 모듈에 대응하는 타일 규칙이 없습니다.";
                return false;
            }

            if (!match.HasUsableTiles(tileSet))
            {
                error = "규칙에 유효한 타일(TypeId/Prefab/Weight>0)이 없습니다.";
                return false;
            }

            rule = match;
            return true;
        }

        public bool TryValidateFor(GenerationModuleOption activeModules, TileSetData tileSet, out string error)
        {
            return TrySelectRule(activeModules, tileSet, out _, out error);
        }

        public ITileAssignmentStrategy CreateStrategy(GenerationModuleOption activeModules, TileSetData tileSet, out string error)
        {
            if (!TrySelectRule(activeModules, tileSet, out var rule, out error))
            {
                return null;
            }

            return new ConditionalWeightedStrategy(rule, ConditionalRules, tileSet);
        }

        /// <summary>
        /// TileAssignmentRules와 ConditionalRules를 바탕으로 TypeId의 막힘 여부 후보 집합을 계산한다.
        /// 기본 규칙에 포함된 타입은 양쪽(Blocked/Unblocked) 모두에 넣고,
        /// ConditionalRule의 RequireBlocked에 따라 추가로 분류한다.
        /// </summary>
        public void BuildBlockedTypeSets(
            GenerationModuleOption activeModules,
            TileSetData tileSet,
            out HashSet<string> blocked,
            out HashSet<string> unblocked)
        {
            blocked = new HashSet<string>();
            unblocked = new HashSet<string>();

            if (tileSet == null)
            {
                return;
            }

            if (!TrySelectRule(activeModules, tileSet, out var rule, out _))
            {
                return;
            }

            var validTypeIds = new HashSet<string>(tileSet.Tiles
                .Where(t => t != null && !string.IsNullOrWhiteSpace(t.TypeId) && t.Prefab != null)
                .Select(t => t.TypeId));

            foreach (var option in rule.GetUsableTiles(tileSet))
            {
                blocked.Add(option.TypeId);
                unblocked.Add(option.TypeId);
            }

            if (ConditionalRules == null || ConditionalRules.Count == 0)
            {
                return;
            }

            foreach (var cond in ConditionalRules)
            {
                if (cond == null) continue;
                if (string.IsNullOrWhiteSpace(cond.OverrideTypeId)) continue;
                if (!validTypeIds.Contains(cond.OverrideTypeId)) continue;

                switch (cond.RequireBlocked)
                {
                    case ConditionalTileRule.BlockRequirement.Blocked:
                        blocked.Add(cond.OverrideTypeId);
                        break;
                    case ConditionalTileRule.BlockRequirement.Unblocked:
                        unblocked.Add(cond.OverrideTypeId);
                        break;
                    case ConditionalTileRule.BlockRequirement.Any:
                    default:
                        blocked.Add(cond.OverrideTypeId);
                        unblocked.Add(cond.OverrideTypeId);
                        break;
                }
            }
        }
    }

    [Serializable]
    public sealed class ModuleTileRule
    {
        [Tooltip("이 규칙을 적용할 Generation 모듈(마스크). None이면 기본 규칙으로 간주합니다.")]
        public GenerationModuleOption TargetModules = GenerationModuleOption.None;

        [Tooltip("타일 TypeId와 가중치 목록. TypeId와 Prefab이 모두 존재하고 Weight>0인 항목만 사용됩니다.")]
        public List<WeightedTileOption> Tiles = new();

        public bool Matches(GenerationModuleOption activeModules)
        {
            if (TargetModules == GenerationModuleOption.None)
            {
                return true;
            }

            return (activeModules & TargetModules) == TargetModules;
        }

        public bool HasUsableTiles(TileSetData tileSet)
        {
            return GetUsableTiles(tileSet).Any();
        }

        public IEnumerable<WeightedTileOption> GetUsableTiles(TileSetData tileSet)
        {
            if (tileSet == null)
            {
                yield break;
            }

            var validTypeIds = new HashSet<string>(tileSet.Tiles
                .Where(t => t != null && !string.IsNullOrWhiteSpace(t.TypeId) && t.Prefab != null)
                .Select(t => t.TypeId));

            foreach (var option in Tiles)
            {
                if (option == null) continue;
                if (option.Weight <= 0f) continue;
                if (string.IsNullOrWhiteSpace(option.TypeId)) continue;
                if (!validTypeIds.Contains(option.TypeId)) continue;
                yield return option;
            }
        }
    }

    [Serializable]
    public sealed class WeightedTileOption
    {
        public string TypeId;
        [Min(0f)]
        public float Weight = 1f;
    }

    [Serializable]
    public sealed class ConditionalTileRule
    {
        public enum BlockRequirement
        {
            Any,
            Blocked,
            Unblocked
        }

        [Tooltip("Usage.IsBlocked 조건. Any면 무시")]
        public BlockRequirement RequireBlocked = BlockRequirement.Any;
        [Tooltip("조건이 만족될 때 사용할 TypeId. 비워두면 무시")]
        public string OverrideTypeId;
        [Tooltip("조건이 만족될 때 추가 Weight 배수 (가중치 스케일)")]
        public float WeightMultiplier = 1f;

        public bool Matches(GridContext context, Vector2Int coords, CellData cell)
        {
            if (RequireBlocked == BlockRequirement.Blocked && !cell.Usage.IsBlocked) return false;
            if (RequireBlocked == BlockRequirement.Unblocked && cell.Usage.IsBlocked) return false;

            return true;
        }
    }

    /// <summary>
    /// 전략 주입을 위한 공급자 인터페이스. 다른 전략 SO를 만들 때도 동일 인터페이스를 구현하면 된다.
    /// </summary>
    public interface ITileAssignmentStrategyProvider
    {
        ITileAssignmentStrategy CreateStrategy(GenerationModuleOption activeModules, TileSetData tileSet, out string error);
    }

    /// <summary>
    /// 타일 배정 전략 인터페이스. 다양한 결정 방식을 확장할 수 있다.
    /// </summary>
    public interface ITileAssignmentStrategy
    {
        bool TryPickTile(out string typeId);
    }

    /// <summary>
    /// 가중치 기반 선택 전략. 기존 규칙 로직을 캡슐화.
    /// </summary>
    internal sealed class ConditionalWeightedStrategy : ITileAssignmentStrategy
    {
        private readonly List<WeightedTileOption> options;
        private readonly System.Random random;
        private readonly float totalWeight;
        private readonly List<ConditionalTileRule> conditionalRules;
        private readonly TileSetData tileSet;
        private GridContext context;
        private Vector2Int coords;

        public ConditionalWeightedStrategy(ModuleTileRule rule, List<ConditionalTileRule> conditionalRules, TileSetData tileSet)
        {
            options = rule.GetUsableTiles(tileSet).ToList();
            totalWeight = options.Sum(o => Mathf.Max(0f, o.Weight));
            random = new System.Random();
            this.conditionalRules = conditionalRules ?? new List<ConditionalTileRule>();
            this.tileSet = tileSet;
        }

        public bool TryPickTile(out string typeId)
        {
            typeId = string.Empty;

            if (options.Count == 0 || totalWeight <= 0f)
            {
                return false;
            }

            // 조건부 규칙: 맞는 것이 있으면 OverrideTypeId 우선, WeightMultiplier 반영
            if (TryConditional(out typeId))
            {
                return true;
            }

            typeId = PickWeighted(options, totalWeight, random.NextDouble());
            return !string.IsNullOrWhiteSpace(typeId);
        }

        public void SetContext(GridContext ctx, Vector2Int position)
        {
            context = ctx;
            coords = position;
        }

        private bool TryConditional(out string typeId)
        {
            typeId = null;
            if (conditionalRules == null || conditionalRules.Count == 0 || context == null)
            {
                return false;
            }

            if (!context.TryGetCell(coords.x, coords.y, out var cell))
            {
                return false;
            }

            foreach (var cond in conditionalRules)
            {
                if (cond == null) continue;
                if (!cond.Matches(context, coords, cell)) continue;

                if (!string.IsNullOrWhiteSpace(cond.OverrideTypeId) &&
                    tileSet.Tiles.Any(t => t.TypeId == cond.OverrideTypeId && t.Prefab != null))
                {
                    typeId = cond.OverrideTypeId;
                    return true;
                }
            }

            return false;
        }

        private static string PickWeighted(
            IReadOnlyList<WeightedTileOption> opts,
            float totalWeight,
            double randomValue)
        {
            var threshold = (float)(randomValue * totalWeight);
            var accumulator = 0f;

            foreach (var option in opts)
            {
                var weight = Mathf.Max(0f, option.Weight);
                accumulator += weight;
                if (threshold <= accumulator)
                {
                    return option.TypeId;
                }
            }

            return opts.Count > 0 ? opts[^1].TypeId : string.Empty;
        }
    }
}
