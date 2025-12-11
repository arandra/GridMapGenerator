using GridMapGenerator.Core;
using UnityEditor;
using UnityEngine;

namespace GridMapGenerator.Editor
{
    [CustomEditor(typeof(GridPipelineProfile))]
    public sealed class GridPipelineProfileEditor : UnityEditor.Editor
    {
        private SerializedProperty gridMeta;
        private SerializedProperty seeds;
        private SerializedProperty constraints;

        private SerializedProperty shapeModule;
        private SerializedProperty generationModules;
        private SerializedProperty flatTerrainScale;
        private SerializedProperty coreObjectSize;
        private SerializedProperty minimumCoreWidth;
        private SerializedProperty minimumHoldRows;
        private SerializedProperty maxLateralStep;
        private SerializedProperty symmetricMarginRange;
        private SerializedProperty marginChangeLimit;
        private SerializedProperty difficulty;
        private SerializedProperty initialCenterOffset;
        private SerializedProperty scrollingCorridorDebugLog;
        private SerializedProperty wfcRules;
        private SerializedProperty wfcRespectUsageBlocked;
        private SerializedProperty wfcRestartOnFailure;
        private SerializedProperty wfcMaxRetries;
        private SerializedProperty wfcUseNewSeedOnRetry;
        private SerializedProperty wfcVerboseLogging;
        private SerializedProperty constraintModules;

        private void OnEnable()
        {
            gridMeta = serializedObject.FindProperty(nameof(GridPipelineProfile.GridMeta));
            seeds = serializedObject.FindProperty(nameof(GridPipelineProfile.Seeds));
            constraints = serializedObject.FindProperty(nameof(GridPipelineProfile.Constraints));

            shapeModule = serializedObject.FindProperty(nameof(GridPipelineProfile.ShapeModule));
            generationModules = serializedObject.FindProperty(nameof(GridPipelineProfile.GenerationModules));
            flatTerrainScale = serializedObject.FindProperty(nameof(GridPipelineProfile.FlatTerrainScale));
            coreObjectSize = serializedObject.FindProperty(nameof(GridPipelineProfile.CoreObjectSize));
            minimumCoreWidth = serializedObject.FindProperty(nameof(GridPipelineProfile.MinimumCoreWidth));
            minimumHoldRows = serializedObject.FindProperty(nameof(GridPipelineProfile.MinimumHoldRows));
            maxLateralStep = serializedObject.FindProperty(nameof(GridPipelineProfile.MaxLateralStep));
            symmetricMarginRange = serializedObject.FindProperty(nameof(GridPipelineProfile.SymmetricMarginRange));
            marginChangeLimit = serializedObject.FindProperty(nameof(GridPipelineProfile.MarginChangeLimit));
            difficulty = serializedObject.FindProperty(nameof(GridPipelineProfile.Difficulty));
            initialCenterOffset = serializedObject.FindProperty(nameof(GridPipelineProfile.InitialCenterOffset));
            scrollingCorridorDebugLog = serializedObject.FindProperty(nameof(GridPipelineProfile.ScrollingCorridorDebugLog));
            wfcRules = serializedObject.FindProperty(nameof(GridPipelineProfile.WfcRules));
            wfcRespectUsageBlocked = serializedObject.FindProperty(nameof(GridPipelineProfile.WfcRespectUsageBlocked));
            wfcRestartOnFailure = serializedObject.FindProperty(nameof(GridPipelineProfile.WfcRestartOnFailure));
            wfcMaxRetries = serializedObject.FindProperty(nameof(GridPipelineProfile.WfcMaxRetries));
            wfcUseNewSeedOnRetry = serializedObject.FindProperty(nameof(GridPipelineProfile.WfcUseNewSeedOnRetry));
            wfcVerboseLogging = serializedObject.FindProperty(nameof(GridPipelineProfile.WfcVerboseLogging));
            constraintModules = serializedObject.FindProperty(nameof(GridPipelineProfile.ConstraintModules));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Grid Definition", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(gridMeta);
            EditorGUILayout.PropertyField(seeds);
            EditorGUILayout.PropertyField(constraints);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Modules", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(shapeModule, new GUIContent("Shape Module"));

            EditorGUILayout.PropertyField(generationModules, new GUIContent("Generation Modules"));
            using (new EditorGUI.IndentLevelScope())
            {
                if (HasFlag(generationModules, GenerationModuleOption.FlatTerrain))
                {
                    EditorGUILayout.PropertyField(flatTerrainScale, new GUIContent("Flat Terrain Scale"));
                }

                if (HasFlag(generationModules, GenerationModuleOption.ScrollingCorridor))
                {
                    EditorGUILayout.PropertyField(coreObjectSize, new GUIContent("Core Object Size (N=폭, M=높이)"));
                    EditorGUILayout.PropertyField(minimumCoreWidth, new GUIContent("Minimum Core Width"));
                    EditorGUILayout.PropertyField(minimumHoldRows, new GUIContent("Minimum Hold Rows"));
                    EditorGUILayout.PropertyField(maxLateralStep, new GUIContent("Max Lateral Step (0~1)"));
                    EditorGUILayout.PropertyField(
                        symmetricMarginRange,
                        new GUIContent("Symmetric Margin Range", "대칭 여유 폭 (x=min, y=max)"));
                    EditorGUILayout.PropertyField(marginChangeLimit, new GUIContent("Margin Change Limit"));
                    EditorGUILayout.PropertyField(difficulty, new GUIContent("Difficulty (0~1)"));
                    EditorGUILayout.PropertyField(initialCenterOffset, new GUIContent("Initial Center Offset"));
                    EditorGUILayout.PropertyField(scrollingCorridorDebugLog, new GUIContent("Debug Log (2-pass)"));
                }

                if (HasFlag(generationModules, GenerationModuleOption.Wfc))
                {
                    EditorGUILayout.PropertyField(wfcRules, new GUIContent("WFC Tile Rules"));
                    EditorGUILayout.PropertyField(
                        wfcRespectUsageBlocked,
                        new GUIContent("Respect Usage.IsBlocked", "Usage.IsBlocked 값에 따라 WFC 후보를 분리합니다."));
                    EditorGUILayout.PropertyField(
                        wfcRestartOnFailure,
                        new GUIContent("Restart On Failure", "실패 시 초기 상태로 되돌아가 재시도합니다."));
                    using (new EditorGUI.IndentLevelScope())
                    {
                        EditorGUILayout.PropertyField(wfcMaxRetries, new GUIContent("Max Retries"));
                    EditorGUILayout.PropertyField(
                        wfcUseNewSeedOnRetry,
                        new GUIContent("Use New Seed On Retry", "재시도마다 시드를 증가시켜 다른 결과를 탐색합니다."));
                    EditorGUILayout.PropertyField(
                        wfcVerboseLogging,
                        new GUIContent("Verbose Logging", "WFC 후보 변화 히스토리를 로그로 출력합니다."));
                }
            }
            }

            EditorGUILayout.PropertyField(constraintModules, new GUIContent("Constraint Modules"));

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "CreatePipeline()를 호출하면 선택한 모듈 조합으로 GridPipeline이 생성됩니다.\n" +
                "프로필 에셋을 컴포넌트나 스크립트에 할당해 코드 없이 모듈 구성을 공유하세요.",
                MessageType.Info);
        }

        private static bool HasFlag(SerializedProperty property, GenerationModuleOption flag)
        {
            return (property.intValue & (int)flag) == (int)flag;
        }
    }
}
