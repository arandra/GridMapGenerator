using System;
using GridMapGenerator;

namespace GridMapGenerator.Modules
{
    /// <summary>
    /// 파이프라인 단계 정의 (1단계 Shape → 2단계 Generation → 3단계 Constraint).
    /// </summary>
    public enum GridModuleStage
    {
        Shape = 1,
        Generation = 2,
        Constraint = 3
    }

    /// <summary>
    /// 모든 모듈이 구현해야 하는 기본 계약.
    /// </summary>
    public interface IGridModule
    {
        GridModuleStage Stage { get; }
        void Process(GridContext context);
    }

    /// <summary>
    /// 1단계 모듈은 GridContext를 생성할 수 있다.
    /// </summary>
    public interface IGridShapeModule : IGridModule
    {
        GridContext CreateContext();
    }

    /// <summary>
    /// 2단계 모듈 마커 인터페이스.
    /// </summary>
    public interface IGridGenerationModule : IGridModule
    {
    }

    /// <summary>
    /// 3단계 모듈 마커 인터페이스.
    /// </summary>
    public interface IGridConstraintModule : IGridModule
    {
    }

    /// <summary>
    /// 모듈 등록 시점 메타데이터. 확장 시 우선순위 등으로 활용 가능.
    /// </summary>
    public readonly struct ModuleDescriptor
    {
        public ModuleDescriptor(Type type, GridModuleStage stage)
        {
            Type = type;
            Stage = stage;
        }

        public Type Type { get; }
        public GridModuleStage Stage { get; }

        public override string ToString() => $"{Type.Name} ({Stage})";
    }
}
