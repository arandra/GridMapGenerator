using GridMapGenerator.Data;
using GridMapGenerator.Modules;
using UnityEngine;

namespace GridMapGenerator.Core
{
    [System.Flags]
    public enum GenerationModuleOption
    {
        None = 0,
        FlatTerrain = 1 << 0,
        BasicUsage = 1 << 1,
        SimpleVariant = 1 << 2,
        ScrollingCorridor = 1 << 3,
        Wfc = 1 << 4
    }

    [System.Flags]
    public enum ConstraintModuleOption
    {
        None = 0,
        Connectivity = 1 << 0
    }

    public enum ShapeModuleOption
    {
        OneDirectionalStrip = 0
    }

    /// <summary>
    /// 인스펙터에서 모듈 구성을 선택하고 파이프라인을 빌드할 수 있는 프로필 에셋.
    /// </summary>
    [CreateAssetMenu(fileName = "GridPipelineProfile", menuName = "Grid Map Generator/Pipeline Profile")]
    public sealed class GridPipelineProfile : ScriptableObject
    {
        [Header("Grid Definition")]
        public GridMeta GridMeta = new()
        {
            Width = 16,
            Height = 16,
            CellSize = 1f,
            CoordinatePlane = CoordinatePlane.XZ
        };

        [Tooltip("GridMeta의 폭/높이가 0(무한)일 때 미리보기 및 런타임에서 사용할 대체 크기")]
        public Vector2Int InfinitePreviewSize = new(16, 16);

        public Seeds Seeds = new()
        {
            GlobalSeed = 1234,
            LocalSeed = 0
        };

        public ConstraintsInfo Constraints = new();

        [Header("Modules")]
        [Tooltip("1단계 Shape 모듈 선택")]
        public ShapeModuleOption ShapeModule = ShapeModuleOption.OneDirectionalStrip;

        [Tooltip("2단계 Generation 모듈 선택 (멀티 선택 가능)")]
        public GenerationModuleOption GenerationModules =
            GenerationModuleOption.FlatTerrain |
            GenerationModuleOption.BasicUsage |
            GenerationModuleOption.SimpleVariant;

        [Min(0.001f)]
        public float FlatTerrainScale = 0.1f;

        [Header("Scrolling Corridor")]
        [Tooltip("통로 최소 폭(x)과 위치 유지 행 수(y)")]
        public Vector2Int MinCorridorSize = new(3, 2);

        [Tooltip("통로 시작 위치를 한 번에 이동시킬 수 있는 최대 칸 수")]
        [Min(0)]
        public int CorridorMaxShiftPerStep = 1;

        [Header("Scrolling Corridor Advanced")]
        [Tooltip("좌측 막힘 최소/최대 폭")]
        public Vector2Int LeftBlockedRange = new(0, 0);

        [Tooltip("우측 막힘 최소/최대 폭")]
        public Vector2Int RightBlockedRange = new(0, 0);

        [Tooltip("좌우 막힘 폭을 동일하게 적용")]
        public bool LockSides = true;

        [Tooltip("통로 최소/최대 폭 비율(그리드 폭 기준 0~1)")]
        public Vector2 MinMaxWidthRatio = new(0.2f, 0.6f);

        [Tooltip("행마다 폭을 변경할 확률(0~1)")]
        [Range(0f, 1f)]
        public float WidthChangeProbability = 0.25f;

        [Tooltip("폭 변경을 서서히 적용하는 정도(0=즉시, 1=매우 완만)")]
        [Range(0f, 1f)]
        public float WidthSmoothing = 0.5f;

        [Tooltip("폭 변경 시 추가되는 랜덤 변동(비율)")]
        [Range(0f, 1f)]
        public float WidthJitterPercent = 0.1f;

        [Tooltip("곡률 수준(0=직선, 1=많이 휨)")]
        [Range(0f, 1f)]
        public float CurvatureLevel = 0.3f;

        [Header("WFC")]
        public WfcTileRules WfcRules;

        [Tooltip("3단계 Constraint 모듈 선택 (멀티 선택 가능)")]
        public ConstraintModuleOption ConstraintModules = ConstraintModuleOption.None;

        /// <summary>
        /// 프로필에서 선택한 모듈 조합으로 파이프라인을 구성합니다.
        /// </summary>
        public GridPipeline CreatePipeline(
            Seeds? seedsOverride = null,
            Vector2Int? infiniteSizeOverride = null,
            ITileAssignmentStrategyProvider tileRules = null,
            TileSetData tileSet = null)
        {
            var pipeline = new GridPipeline();

            var constraints = Constraints ?? new ConstraintsInfo();
            var seedsToUse = seedsOverride ?? Seeds;
            var infiniteSize = infiniteSizeOverride ?? InfinitePreviewSize;

            var meta = GridMeta;
            meta.Width = AdjustInfiniteSize(meta.Width, infiniteSize.x);
            meta.Height = AdjustInfiniteSize(meta.Height, infiniteSize.y);

            RegisterShape(pipeline, constraints, meta, seedsToUse);
            RegisterGeneration(pipeline, seedsToUse, tileRules, tileSet);
            RegisterConstraints(pipeline, constraints);

            return pipeline;
        }

        /// <summary>
        /// 모듈 구성을 바탕으로 바로 GridContext를 생성합니다.
        /// </summary>
        public GridContext Run(ITileAssignmentStrategyProvider tileRules = null, TileSetData tileSet = null) =>
            CreatePipeline(null, null, tileRules, tileSet).Run();

        private static int AdjustInfiniteSize(int value, int fallback)
        {
            if (value > 0)
            {
                return value;
            }

            return Mathf.Max(1, fallback);
        }

        private void RegisterShape(GridPipeline pipeline, ConstraintsInfo constraints, GridMeta meta, Seeds seeds)
        {
            switch (ShapeModule)
            {
                case ShapeModuleOption.OneDirectionalStrip:
                default:
                    pipeline.RegisterModule(new OneDirectionalStripGridModule(meta, seeds, constraints));
                    break;
            }
        }

        private void RegisterGeneration(
            GridPipeline pipeline,
            Seeds seedsToUse,
            ITileAssignmentStrategyProvider tileRules,
            TileSetData tileSet)
        {
            if (GenerationModules.HasFlag(GenerationModuleOption.FlatTerrain))
            {
                var scale = Mathf.Max(0.001f, FlatTerrainScale);
                pipeline.RegisterModule(new FlatTerrainModule(scale));
            }

            if (GenerationModules.HasFlag(GenerationModuleOption.BasicUsage))
            {
                pipeline.RegisterModule(new BasicUsageModule());
            }

            if (GenerationModules.HasFlag(GenerationModuleOption.ScrollingCorridor))
            {
                pipeline.RegisterModule(new ScrollingCorridorModule(
                    Mathf.Max(1, MinCorridorSize.x),
                    Mathf.Max(1, MinCorridorSize.y),
                    Mathf.Max(0, CorridorMaxShiftPerStep),
                    LeftBlockedRange,
                    RightBlockedRange,
                    LockSides,
                    MinMaxWidthRatio,
                    Mathf.Clamp01(WidthChangeProbability),
                    Mathf.Clamp01(WidthSmoothing),
                    Mathf.Clamp01(WidthJitterPercent),
                    Mathf.Clamp01(CurvatureLevel),
                    seedsToUse));
            }

            if (GenerationModules.HasFlag(GenerationModuleOption.Wfc))
            {
                pipeline.RegisterModule(new WfcGenerationModule(WfcRules, seedsToUse));
            }

            if (GenerationModules.HasFlag(GenerationModuleOption.SimpleVariant))
            {
                pipeline.RegisterModule(new SimpleVariantModule());
            }

            if (tileRules != null && tileSet != null)
            {
                pipeline.RegisterModule(new TileAssignmentModule(tileSet, tileRules, GenerationModules, seedsToUse));
            }
        }

        private void RegisterConstraints(GridPipeline pipeline, ConstraintsInfo constraints)
        {
            if (ConstraintModules.HasFlag(ConstraintModuleOption.Connectivity))
            {
                pipeline.RegisterModule(new ConnectivityConstraintModule());
            }
        }
    }
}
