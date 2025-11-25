using System.Collections.Generic;
using System.Linq;
using GridMapGenerator.Data;
using UnityEditor;
using UnityEngine;

namespace GridMapGenerator.Editor
{
    [CustomPropertyDrawer(typeof(ConditionalTileRule))]
    internal sealed class ConditionalTileRuleDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float h = 0f;
            h += EditorGUIUtility.singleLineHeight; // label
            h += EditorGUIUtility.standardVerticalSpacing;
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative(nameof(ConditionalTileRule.RequireBlocked)), true);
            h += EditorGUIUtility.standardVerticalSpacing;
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative(nameof(ConditionalTileRule.OverrideTypeId)), true);
            h += EditorGUIUtility.standardVerticalSpacing;
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative(nameof(ConditionalTileRule.WeightMultiplier)), true);
            return h;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var requireBlocked = property.FindPropertyRelative(nameof(ConditionalTileRule.RequireBlocked));
            var overrideTypeId = property.FindPropertyRelative(nameof(ConditionalTileRule.OverrideTypeId));
            var weightMultiplier = property.FindPropertyRelative(nameof(ConditionalTileRule.WeightMultiplier));

            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(line, label);

            line.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            DrawRequireBlocked(line, requireBlocked);

            line.y += EditorGUI.GetPropertyHeight(requireBlocked, true) + EditorGUIUtility.standardVerticalSpacing;
            DrawTypeIdPopup(line, property.serializedObject, overrideTypeId);

            line.y += EditorGUI.GetPropertyHeight(overrideTypeId, true) + EditorGUIUtility.standardVerticalSpacing;
            EditorGUI.PropertyField(line, weightMultiplier, new GUIContent("Weight Multiplier"));
        }

        private static void DrawRequireBlocked(Rect rect, SerializedProperty prop)
        {
            EditorGUI.PropertyField(rect, prop, new GUIContent("Require Blocked"));
        }

        private static void DrawTypeIdPopup(Rect rect, SerializedObject so, SerializedProperty overrideTypeId)
        {
            var tileSet = FindTileSet(so);
            if (tileSet == null)
            {
                EditorGUI.PropertyField(rect, overrideTypeId, new GUIContent("Override TypeId"));
                return;
            }

            var typeIds = CollectTypeIds(tileSet);
            if (typeIds.Count == 0)
            {
                EditorGUI.PropertyField(rect, overrideTypeId, new GUIContent("Override TypeId (TileSet empty)"));
                return;
            }

            int currentIndex = Mathf.Max(0, typeIds.IndexOf(overrideTypeId.stringValue));
            int newIndex = EditorGUI.Popup(rect, "Override TypeId", currentIndex, typeIds.ToArray());
            if (newIndex >= 0 && newIndex < typeIds.Count)
            {
                overrideTypeId.stringValue = typeIds[newIndex];
            }
        }

        private static TileSetData FindTileSet(SerializedObject so)
        {
            if (so?.targetObject is TileAssignmentRules rules && rules.EditorTileSet != null)
            {
                return rules.EditorTileSet;
            }

            // Try to find sibling property named "TileSet" in the same settings asset.
            var tileSetProp = so.FindProperty("TileSet");
            if (tileSetProp != null && tileSetProp.objectReferenceValue is TileSetData ts)
            {
                return ts;
            }

            // Try parent property (e.g., GridPipelineProfile or GridMapTestSettings)
            return null;
        }

        private static List<string> CollectTypeIds(TileSetData tileSet)
        {
            return tileSet.Tiles
                .Where(t => t != null && !string.IsNullOrWhiteSpace(t.TypeId))
                .Select(t => t.TypeId)
                .Distinct()
                .Prepend(string.Empty) // allow empty
                .ToList();
        }
    }

    [CustomPropertyDrawer(typeof(ModuleTileRule))]
    internal sealed class ModuleTileRuleDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float h = 0f;
            h += EditorGUIUtility.singleLineHeight; // label
            h += EditorGUIUtility.standardVerticalSpacing;
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative(nameof(ModuleTileRule.TargetModules)), true);
            h += EditorGUIUtility.standardVerticalSpacing;
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative(nameof(ModuleTileRule.Tiles)), true);
            return h;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var targetModules = property.FindPropertyRelative(nameof(ModuleTileRule.TargetModules));
            var tiles = property.FindPropertyRelative(nameof(ModuleTileRule.Tiles));

            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(line, label);

            line.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            EditorGUI.PropertyField(line, targetModules);

            line.y += EditorGUI.GetPropertyHeight(targetModules, true) + EditorGUIUtility.standardVerticalSpacing;
            EditorGUI.PropertyField(line, tiles, new GUIContent("Tiles"), true);
        }
    }
}
