using System;
using System.Collections.Generic;
using System.Linq;
using GridMapGenerator.Modules;

namespace GridMapGenerator.Core
{
    /// <summary>
    /// 모듈을 등록된 순서대로 관리하면서 Stage 순서를 보장하는 파이프라인 실행기.
    /// </summary>
    public sealed class GridPipeline
    {
        private readonly List<IGridModule> modules = new();

        public IReadOnlyList<IGridModule> Modules => modules;

        public void RegisterModule(IGridModule module)
        {
            if (module == null)
            {
                throw new ArgumentNullException(nameof(module));
            }

            modules.Add(module);
        }

        public bool RemoveModule(IGridModule module) => modules.Remove(module);

        public void ClearModules() => modules.Clear();

        public GridContext Run(GridContext context = null)
        {
            if (modules.Count == 0)
            {
                throw new InvalidOperationException("파이프라인에 최소 1개의 모듈이 필요합니다.");
            }

            var orderedModules = modules
                .Select((module, order) => new { module, order })
                .OrderBy(pair => (int)pair.module.Stage)
                .ThenBy(pair => pair.order)
                .Select(pair => pair.module)
                .ToList();

            var shapeModule = orderedModules.OfType<IGridShapeModule>().FirstOrDefault();
            if (context == null)
            {
                if (shapeModule == null)
                {
                    throw new InvalidOperationException("GridContext를 초기화할 1단계 모듈이 필요합니다.");
                }

                context = shapeModule.CreateContext();
            }

            foreach (var module in orderedModules)
            {
                module.Process(context);
            }

            return context;
        }
    }
}
