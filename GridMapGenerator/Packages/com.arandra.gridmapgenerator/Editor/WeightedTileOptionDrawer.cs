using System.Collections.Generic;
using System.Linq;
using GridMapGenerator.Data;
using UnityEditor;
using UnityEngine;

namespace GridMapGenerator.Editor
{
    [CustomPropertyDrawer(typeof(WeightedTileOption))]
    internal sealed class WeightedTileOptionDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float h = 0f;
            h += EditorGUIUtility.singleLineHeight; // label
            h += EditorGUIUtility.standardVerticalSpacing;
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative(nameof(WeightedTileOption.TypeId)), true);
            h += EditorGUIUtility.standardVerticalSpacing;
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative(nameof(WeightedTileOption.Weight)), true);
            return h;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var typeIdProp = property.FindPropertyRelative(nameof(WeightedTileOption.TypeId));
            var weightProp = property.FindPropertyRelative(nameof(WeightedTileOption.Weight));

            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(line, label);

            line.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            DrawTypeIdPopup(line, property.serializedObject, typeIdProp);

            line.y += EditorGUI.GetPropertyHeight(typeIdProp, true) + EditorGUIUtility.standardVerticalSpacing;
            EditorGUI.PropertyField(line, weightProp);
        }

        private static void DrawTypeIdPopup(Rect rect, SerializedObject so, SerializedProperty typeIdProp)
        {
            var tileSet = FindTileSet(so);
            if (tileSet == null)
            {
                EditorGUI.PropertyField(rect, typeIdProp, new GUIContent("TypeId"));
                return;
            }

            var typeIds = CollectTypeIds(tileSet);
            if (typeIds.Count == 0)
            {
                EditorGUI.PropertyField(rect, typeIdProp, new GUIContent("TypeId (TileSet empty)"));
                return;
            }

            int currentIndex = Mathf.Max(0, typeIds.IndexOf(typeIdProp.stringValue));
            int newIndex = EditorGUI.Popup(rect, "TypeId", currentIndex, typeIds.ToArray());
            if (newIndex >= 0 && newIndex < typeIds.Count)
            {
                typeIdProp.stringValue = typeIds[newIndex];
            }
        }

        private static TileSetData FindTileSet(SerializedObject so)
        {
            if (so?.targetObject is TileAssignmentRules rules && rules.EditorTileSet != null)
            {
                return rules.EditorTileSet;
            }

            var tileSetProp = so.FindProperty("TileSet");
            if (tileSetProp != null && tileSetProp.objectReferenceValue is TileSetData ts)
            {
                return ts;
            }

            return null;
        }

        private static List<string> CollectTypeIds(TileSetData tileSet)
        {
            return tileSet.Tiles
                .Where(t => t != null && !string.IsNullOrWhiteSpace(t.TypeId))
                .Select(t => t.TypeId)
                .Distinct()
                .Prepend(string.Empty)
                .ToList();
        }
    }
}
