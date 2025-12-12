using System.Linq;
using GridMapGenerator.Core;
using GridMapGenerator.Data;
using GridMapGenerator.Testing;
using UnityEditor;
using UnityEngine;

namespace GridMapGenerator.Editor
{
    public sealed class GridMapTestWindow : EditorWindow
    {
        private const string SeedPrefKey = "GridMapGenerator.Test.LastSeed";
        private const string SuppressWarningPrefKey = "GridMapGenerator.Test.SuppressClearWarning";

        private GridMapTestSettings settings;
        private int seed;
        private bool seedLoaded;

        [MenuItem("Window/Grid Map Generator/Test Runner")]
        public static void Open()
        {
            GetWindow<GridMapTestWindow>("Grid Map Test");
        }

        private void OnEnable()
        {
            if (!seedLoaded)
            {
                seed = EditorPrefs.GetInt(SeedPrefKey, 1234);
                seedLoaded = true;
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Grid Map Test Runner", EditorStyles.boldLabel);
            settings = (GridMapTestSettings)EditorGUILayout.ObjectField("Test Settings", settings, typeof(GridMapTestSettings), false);

            if (settings == null)
            {
                EditorGUILayout.HelpBox("Test Settings 자산을 선택하거나 생성하세요.", MessageType.Info);
                if (GUILayout.Button("Create Test Settings Asset..."))
                {
                    CreateSettingsAsset();
                }
                return;
            }

            EditorGUI.BeginChangeCheck();
            DrawPreviewSettings();
            EditorGUILayout.Space();
            DrawSeedControls();
            EditorGUILayout.Space();
            DrawActions();

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(settings);
            }
        }

        private void DrawPreviewSettings()
        {
            EditorGUILayout.LabelField("Preview Options", EditorStyles.boldLabel);
            settings.RootObjectName = EditorGUILayout.TextField("Root Object Name", settings.RootObjectName);

            bool hasProfile = settings.PipelineProfile != null;
            bool isInfinite = hasProfile && IsInfiniteGrid(settings.PipelineProfile);
            using (new EditorGUI.DisabledScope(hasProfile && !isInfinite))
            {
                settings.PreviewSize = EditorGUILayout.Vector2IntField("Preview Size (for infinite)", settings.PreviewSize);
            }

            if (hasProfile && !isInfinite)
            {
                var meta = settings.PipelineProfile.GridMeta;
                EditorGUILayout.HelpBox(
                    $"Pipeline Profile의 GridMeta가 {meta.Width}x{meta.Height}로 설정되어 있어 Preview Size가 적용되지 않습니다. 폭/높이를 0으로 두면 Preview Size가 사용됩니다.",
                    MessageType.Info);
            }

            if (settings.PipelineProfile == null)
            {
                EditorGUILayout.HelpBox("Pipeline Profile이 필요합니다.", MessageType.Warning);
            }

            settings.PipelineProfile = (GridPipelineProfile)EditorGUILayout.ObjectField(
                "Pipeline Profile", settings.PipelineProfile, typeof(GridPipelineProfile), false);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Tile Configuration", EditorStyles.boldLabel);
            
            if (settings.TileSet == null)
            {
                EditorGUILayout.HelpBox("Tile Set Data가 필요합니다.", MessageType.Warning);
            }

            settings.TileSet = (TileSetData)EditorGUILayout.ObjectField(
                "Tile Set", settings.TileSet, typeof(TileSetData), false);

            if (settings.TileRules == null)
            {
                EditorGUILayout.HelpBox("Tile Assignment Rules가 필요합니다.", MessageType.Warning);
            }

            settings.TileRules = (TileAssignmentRules)EditorGUILayout.ObjectField(
                "Tile Assignment Rules", settings.TileRules, typeof(TileAssignmentRules), false);

            settings.WfcRules = (WfcTileRules)EditorGUILayout.ObjectField(
                "WFC Tile Rules", settings.WfcRules, typeof(WfcTileRules), false);
        }

        private void DrawSeedControls()
        {
            EditorGUILayout.LabelField("Seed", EditorStyles.boldLabel);
            int newSeed = EditorGUILayout.IntField("Seed", seed);
            if (newSeed != seed)
            {
                seed = newSeed;
                EditorPrefs.SetInt(SeedPrefKey, seed);
            }

            if (GUILayout.Button("Random Seed"))
            {
                seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
                EditorPrefs.SetInt(SeedPrefKey, seed);
            }
        }

        private void DrawActions()
        {
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(settings.PipelineProfile == null))
            {
                if (GUILayout.Button("Generate"))
                {
                    Generate();
                }
            }

            if (GUILayout.Button("Delete (Clear Root Children)"))
            {
                DeleteGenerated();
            }
        }

        private void Generate()
        {
            if (settings.PipelineProfile == null)
            {
                EditorUtility.DisplayDialog("Pipeline Profile 필요", "Pipeline Profile을 설정하세요.", "확인");
                return;
            }

            if (!ValidateTileRules(out var validationError))
            {
                Debug.LogError($"GridMapTestWindow: 타일 규칙 오류 - {validationError}");
                EditorUtility.DisplayDialog("타일 규칙 오류", validationError, "확인");
                return;
            }

            var root = FindOrCreateRoot(settings.RootObjectName);
            if (!ConfirmClearIfNeeded(root))
            {
                return;
            }

            ClearChildren(root);

            var seedsOverride = settings.PipelineProfile.Seeds;
            seedsOverride.GlobalSeed = seed;
            if (seedsOverride.LocalSeed == 0)
            {
                seedsOverride.LocalSeed = seed;
            }

            Vector2Int? previewSizeOverride = null;
            if (IsInfiniteGrid(settings.PipelineProfile))
            {
                if (!TryGetPreviewSize(out var previewSize, out var previewError))
                {
                    EditorUtility.DisplayDialog("프리뷰 크기 필요", previewError, "확인");
                    return;
                }

                previewSizeOverride = previewSize;
            }

            var pipeline = settings.PipelineProfile.CreatePipeline(
                seedsOverride,
                previewSizeOverride,
                settings.TileRules,
                settings.TileSet);
            var context = pipeline.Run();

            foreach (var (coords, cell) in context.EnumerateCells())
            {
                if (settings.TileSet == null) continue;

                var binding = settings.TileSet.Tiles.FirstOrDefault(t => t.TypeId == cell.Terrain.TypeId);
                if (binding == null || binding.Prefab == null)
                {
                    continue;
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(binding.Prefab);
                if (instance == null)
                {
                    continue;
                }

                instance.name = $"{binding.TypeId}_{coords.x}_{coords.y}";
                instance.transform.SetParent(root.transform);
                instance.transform.position = ToWorldPosition(context, coords);
            }
        }

        private void DeleteGenerated()
        {
            var root = FindOrCreateRoot(settings.RootObjectName);
            ClearChildren(root);
        }

        private static GameObject FindOrCreateRoot(string name)
        {
            var root = GameObject.Find(name);
            if (root == null)
            {
                root = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(root, "Create Grid Map Root");
            }

            return root;
        }

        private bool ConfirmClearIfNeeded(GameObject root)
        {
            if (root.transform.childCount == 0)
            {
                return true;
            }

            bool suppress = EditorPrefs.GetBool(SuppressWarningPrefKey, false);
            if (suppress)
            {
                return true;
            }

            int result = EditorUtility.DisplayDialogComplex(
                "경고",
                "루트 오브젝트에 자식이 있습니다. 삭제 후 다시 생성할까요?",
                "확인",
                "취소",
                "확인 및 다음에 보지 않음");

            switch (result)
            {
                case 0:
                    return true;
                case 1:
                    return false;
                case 2:
                    EditorPrefs.SetBool(SuppressWarningPrefKey, true);
                    return true;
                default:
                    return false;
            }
        }

        private static void ClearChildren(GameObject root)
        {
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                var child = root.transform.GetChild(i).gameObject;
                Undo.DestroyObjectImmediate(child);
            }
        }

        private static Vector3 ToWorldPosition(GridContext context, Vector2Int coords)
        {
            var cellSize = context.Meta.CellSize;
            var origin = context.Meta.Origin;
            if (context.Meta.CoordinatePlane == CoordinatePlane.XZ)
            {
                return origin + new Vector3(coords.x * cellSize, 0f, coords.y * cellSize);
            }

            return origin + new Vector3(coords.x * cellSize, coords.y * cellSize, 0f);
        }

        private void CreateSettingsAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Grid Map Test Settings",
                "GridMapTestSettings",
                "asset",
                "테스트 설정 자산을 저장할 위치를 선택하세요.");

            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var asset = ScriptableObject.CreateInstance<GridMapTestSettings>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            settings = asset;
        }

        private bool ValidateTileRules(out string error)
        {
            if (settings.TileSet == null)
            {
                error = "Tile Set Data가 필요합니다.";
                return false;
            }

            // TileRules는 WFC를 사용하지 않을 때 필수
            if (settings.TileRules == null && !settings.PipelineProfile.GenerationModules.HasFlag(GenerationModuleOption.Wfc))
            {
                error = "Tile Assignment Rules 자산을 설정하세요.";
                return false;
            }

            if (settings.TileRules != null &&
                !settings.TileRules.TryValidateFor(settings.PipelineProfile.GenerationModules, settings.TileSet, out var ruleError))
            {
                error = $"타일 규칙 검증 실패: {ruleError}";
                return false;
            }

            if (settings.PipelineProfile.GenerationModules.HasFlag(GenerationModuleOption.Wfc) &&
                settings.PipelineProfile.WfcRules == null && settings.WfcRules == null)
            {
                error = "WFC 모듈이 활성화되어 있으나 WFC Tile Rules가 없습니다.";
                return false;
            }

            if (settings.WfcRules != null)
            {
                settings.PipelineProfile.WfcRules = settings.WfcRules;
            }

            error = null;
            return true;
        }

        private static bool IsInfiniteGrid(GridPipelineProfile profile)
        {
            if (profile == null)
            {
                return false;
            }

            var meta = profile.GridMeta;
            return meta.Width <= 0 || meta.Height <= 0;
        }

        private bool TryGetPreviewSize(out Vector2Int previewSize, out string error)
        {
            previewSize = settings.PreviewSize;
            if (previewSize.x <= 0 || previewSize.y <= 0)
            {
                error = "Preview Size는 1 이상이어야 합니다.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
