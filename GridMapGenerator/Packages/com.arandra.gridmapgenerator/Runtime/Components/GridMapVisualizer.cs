using System.Linq;
using GridMapGenerator.Core;
using GridMapGenerator.Data;
using UnityEngine;

namespace GridMapGenerator.Components
{
    public class GridMapVisualizer : MonoBehaviour
    {
        public GridPipelineProfile PipelineProfile;
        public TileSetData TileSet;
        public TileAssignmentRules TileRules;
        public WfcTileRules WfcRules;
        public bool GenerateOnStart = true;

        private void Start()
        {
            if (GenerateOnStart)
            {
                Generate();
            }
        }

        public void Generate()
        {
            if (PipelineProfile == null || TileSet == null)
            {
                Debug.LogWarning("GridMapVisualizer: PipelineProfile 또는 TileSet이 없습니다.");
                return;
            }

            if (TileRules != null && !TileRules.TryValidateFor(PipelineProfile.GenerationModules, TileSet, out var ruleError))
            {
                Debug.LogError($"GridMapVisualizer: 타일 규칙 검증 실패 - {ruleError}");
                return;
            }

            // Clear existing children
            foreach (Transform child in transform)
            {
                Destroy(child.gameObject);
            }

            var context = PipelineProfile.CreatePipeline(null, null, TileRules, TileSet).Run();

            foreach (var (coords, cell) in context.EnumerateCells())
            {
                var binding = TileSet.Tiles.FirstOrDefault(t => t.TypeId == cell.Terrain.TypeId);
                if (binding == null || binding.Prefab == null)
                {
                    continue;
                }

                var instance = Instantiate(binding.Prefab, transform);
                instance.name = $"{binding.TypeId}_{coords.x}_{coords.y}";
                instance.transform.position = ToWorldPosition(context, coords);
            }
        }

        private Vector3 ToWorldPosition(GridContext context, Vector2Int coords)
        {
            var cellSize = context.Meta.CellSize;
            var origin = context.Meta.Origin;
            // Local position relative to this object? 
            // The original logic used world origin. 
            // If we want it relative to this transform, we should use local position logic but Instantiate puts it in world space by default unless we set parent.
            // We set parent to 'transform'.
            // So we should calculate local position.
            
            Vector3 localPos;
            if (context.Meta.CoordinatePlane == CoordinatePlane.XZ)
            {
                localPos = new Vector3(coords.x * cellSize, 0f, coords.y * cellSize);
            }
            else
            {
                localPos = new Vector3(coords.x * cellSize, coords.y * cellSize, 0f);
            }

            return transform.position + origin + localPos; 
            // Note: origin in GridMeta might be intended as an offset. 
            // If we want the grid to start at this object's position, we add transform.position.
        }
    }
}
