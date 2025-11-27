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
        private SerializedProperty minCorridorSize;
        private SerializedProperty corridorMaxShiftPerStep;
        private SerializedProperty leftBlockedRange;
        private SerializedProperty rightBlockedRange;
        private SerializedProperty lockSides;
        private SerializedProperty minMaxWidthRatio;
        private SerializedProperty widthChangeProbability;
        private SerializedProperty widthSmoothing;
        private SerializedProperty widthJitterPercent;
        private SerializedProperty curvatureLevel;
        private SerializedProperty wfcRules;
        private SerializedProperty constraintModules;

        private void OnEnable()
        {
            gridMeta = serializedObject.FindProperty(nameof(GridPipelineProfile.GridMeta));
            seeds = serializedObject.FindProperty(nameof(GridPipelineProfile.Seeds));
            constraints = serializedObject.FindProperty(nameof(GridPipelineProfile.Constraints));

            shapeModule = serializedObject.FindProperty(nameof(GridPipelineProfile.ShapeModule));
            generationModules = serializedObject.FindProperty(nameof(GridPipelineProfile.GenerationModules));
            flatTerrainScale = serializedObject.FindProperty(nameof(GridPipelineProfile.FlatTerrainScale));
            minCorridorSize = serializedObject.FindProperty(nameof(GridPipelineProfile.MinCorridorSize));
            corridorMaxShiftPerStep = serializedObject.FindProperty(nameof(GridPipelineProfile.CorridorMaxShiftPerStep));
            leftBlockedRange = serializedObject.FindProperty(nameof(GridPipelineProfile.LeftBlockedRange));
            rightBlockedRange = serializedObject.FindProperty(nameof(GridPipelineProfile.RightBlockedRange));
            lockSides = serializedObject.FindProperty(nameof(GridPipelineProfile.LockSides));
            minMaxWidthRatio = serializedObject.FindProperty(nameof(GridPipelineProfile.MinMaxWidthRatio));
            widthChangeProbability = serializedObject.FindProperty(nameof(GridPipelineProfile.WidthChangeProbability));
            widthSmoothing = serializedObject.FindProperty(nameof(GridPipelineProfile.WidthSmoothing));
            widthJitterPercent = serializedObject.FindProperty(nameof(GridPipelineProfile.WidthJitterPercent));
            curvatureLevel = serializedObject.FindProperty(nameof(GridPipelineProfile.CurvatureLevel));
            wfcRules = serializedObject.FindProperty(nameof(GridPipelineProfile.WfcRules));
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
                    EditorGUILayout.PropertyField(minCorridorSize, new GUIContent("Min Corridor Size (x=폭, y=유지 행)"));
                    EditorGUILayout.PropertyField(corridorMaxShiftPerStep, new GUIContent("Max Shift Per Step"));
                    EditorGUILayout.PropertyField(leftBlockedRange, new GUIContent("Left Blocked Range"));
                    EditorGUILayout.PropertyField(lockSides, new GUIContent("Lock Sides"));
                    using (new EditorGUI.DisabledScope(lockSides.boolValue))
                    {
                        EditorGUILayout.PropertyField(rightBlockedRange, new GUIContent("Right Blocked Range"));
                    }

                    EditorGUILayout.PropertyField(minMaxWidthRatio, new GUIContent("Min/Max Width Ratio"));
                    EditorGUILayout.PropertyField(widthChangeProbability, new GUIContent("Width Change Probability"));
                    EditorGUILayout.PropertyField(widthJitterPercent, new GUIContent("Width Jitter Percent"));
                    EditorGUILayout.PropertyField(widthSmoothing, new GUIContent("Width Smoothing"));
                    EditorGUILayout.PropertyField(curvatureLevel, new GUIContent("Curvature Level"));
                }

                if (HasFlag(generationModules, GenerationModuleOption.Wfc))
                {
                    EditorGUILayout.PropertyField(wfcRules, new GUIContent("WFC Tile Rules"));
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
