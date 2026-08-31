using UnityEditor;
using UnityEngine;
using XericUI.XChart;

namespace XericUI.XChart.Editor
{
    [CustomEditor(typeof(XChartBase), true)]
    internal sealed class XChartBaseEditor : UnityEditor.Editor
    {
        private bool m_ShowData = true;
        private bool m_ShowAdvanced;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script", "m_DataModel", "m_ShowDebug", "m_HideAllTools");
            m_ShowData = EditorGUILayout.Foldout(m_ShowData, "数据", true);
            if (m_ShowData)
            {
                var model = ((XChartBase)target).DataModel;
                EditorGUILayout.LabelField("维度", model.Dimensions.Count.ToString());
                EditorGUILayout.LabelField("记录", model.Records.Count.ToString());
                EditorGUILayout.LabelField("版本", model.Version.ToString());
            }
            m_ShowAdvanced = EditorGUILayout.Foldout(m_ShowAdvanced, "高级", true);
            if (m_ShowAdvanced)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_ShowDebug"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_HideAllTools"));
            }
            serializedObject.ApplyModifiedProperties();
        }
    }
}
