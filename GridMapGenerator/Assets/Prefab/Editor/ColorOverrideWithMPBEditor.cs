using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ColorOverrideWithMPB))]
public class ColorOverrideWithMPBEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // 기본 인스펙터
        DrawPropertiesExcluding(serializedObject, "m_Script", "errorMessage");

        // 에러 메시지 출력
        var errorProp = serializedObject.FindProperty("errorMessage");
        if (!string.IsNullOrEmpty(errorProp.stringValue))
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(errorProp.stringValue, MessageType.Error);
        }

        serializedObject.ApplyModifiedProperties();

        // 실시간 적용 버튼(선택사항)
        if (GUILayout.Button("Apply Now"))
        {
            var comp = (ColorOverrideWithMPB)target;
            comp.Apply();
        }
    }
}