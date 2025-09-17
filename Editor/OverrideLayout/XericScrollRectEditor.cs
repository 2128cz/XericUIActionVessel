using UnityEditor;
using UnityEditor.UI;
using XericUI.OverrideLayout;

namespace XericUIEditor.OverrideLayout
{
    [CustomEditor(typeof(XericScrollRect), true)]
    [CanEditMultipleObjects]
    public class XericScrollRectEditor : ScrollRectEditor
    {
        SerializedProperty m_EnableAutoActiveByMaskVisible;
        SerializedProperty m_layoutGroup;
        SerializedProperty m_preloadMargin;
        SerializedProperty m_discernibleDistance;
        SerializedProperty m_AAMVUpdateRate;

        protected override void OnEnable()
        {
            base.OnEnable();
            m_EnableAutoActiveByMaskVisible = serializedObject.FindProperty("enableAutoActiveByMaskVisible");
            m_layoutGroup = serializedObject.FindProperty("layoutGroup");
            m_preloadMargin = serializedObject.FindProperty("preloadMargin");
            m_discernibleDistance = serializedObject.FindProperty("discernibleDistance");
            m_AAMVUpdateRate = serializedObject.FindProperty("AAMVUpdateRate");
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            serializedObject.Update();
            
            EditorGUILayout.PropertyField(m_EnableAutoActiveByMaskVisible);
            if (m_EnableAutoActiveByMaskVisible.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(m_layoutGroup, EditorGUIUtility.TrTextContent("Layout Group"));
                EditorGUILayout.PropertyField(m_preloadMargin, EditorGUIUtility.TrTextContent("Preload Margin"));
                EditorGUILayout.PropertyField(m_discernibleDistance, EditorGUIUtility.TrTextContent("Discernible Distance"));
                EditorGUILayout.PropertyField(m_AAMVUpdateRate, EditorGUIUtility.TrTextContent("Aamv Update Rate"));

                EditorGUI.indentLevel--;
            }
            
            serializedObject.ApplyModifiedProperties();
        }
    }
}