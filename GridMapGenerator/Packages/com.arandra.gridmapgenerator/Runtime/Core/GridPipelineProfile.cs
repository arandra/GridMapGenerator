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
        SimpleVariant = 1 << 2
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

        [Tooltip("3단계 Constraint 모듈 선택 (멀티 선택 가능)")]
        public ConstraintModuleOption ConstraintModules = ConstraintModuleOption.None;

        /// <summary>
        /// 프로필에서 선택한 모듈 조합으로 파이프라인을 구성합니다.
        /// </summary>
        public GridPipeline CreatePipeline()
        {
            var pipeline = new GridPipeline();

            var constraints = Constraints ?? new ConstraintsInfo();

            RegisterShape(pipeline, constraints);
            RegisterGeneration(pipeline);
            RegisterConstraints(pipeline, constraints);

            return pipeline;
        }

        /// <summary>
        /// 모듈 구성을 바탕으로 바로 GridContext를 생성합니다.
        /// </summary>
        public GridContext Run() => CreatePipeline().Run();

        private void RegisterShape(GridPipeline pipeline, ConstraintsInfo constraints)
        {
            switch (ShapeModule)
            {
                case ShapeModuleOption.OneDirectionalStrip:
                default:
                    pipeline.RegisterModule(new OneDirectionalStripGridModule(GridMeta, Seeds, constraints));
                    break;
            }
        }

        private void RegisterGeneration(GridPipeline pipeline)
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

            if (GenerationModules.HasFlag(GenerationModuleOption.SimpleVariant))
            {
                pipeline.RegisterModule(new SimpleVariantModule());
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
