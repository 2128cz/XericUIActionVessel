using UnityEditor;
using UnityEditor.UI;
using UnityEngine;
using XericUI.OverrideLayout;

namespace XericUIEditor.OverrideLayout
{
    [CustomEditor(typeof(XericScrollRect), true)]
    [CanEditMultipleObjects]
    internal class XericScrollRectEditor : ScrollRectEditor
    {
        SerializedProperty m_EnableAutoActiveByMaskVisible;
        SerializedProperty m_layoutGroup;
        SerializedProperty m_preloadMargin;
        SerializedProperty m_discernibleDistance;
        SerializedProperty m_AAMVUpdateRate;
        SerializedProperty m_childrenPool;

        SerializedProperty m_cellSize;
        SerializedProperty m_spacing;
        SerializedProperty m_padding;
        SerializedProperty m_startCorner;
        SerializedProperty m_startAxis;
        SerializedProperty m_childAlignment;
        SerializedProperty m_constraint;

        protected override void OnEnable()
        {
            base.OnEnable();
            m_EnableAutoActiveByMaskVisible = serializedObject.FindProperty("m_enableAdvancedFunc");
            m_layoutGroup = serializedObject.FindProperty("layoutGroup");
            m_preloadMargin = serializedObject.FindProperty("m_preloadMargin");
            m_discernibleDistance = serializedObject.FindProperty("m_discernibleDistance");
            m_AAMVUpdateRate = serializedObject.FindProperty("m_AAMVUpdateRate");
            
            m_childrenPool = serializedObject.FindProperty("childrenPool");
            m_cellSize = serializedObject.FindProperty("m_cellSize");
            m_spacing = serializedObject.FindProperty("m_spacing");
            m_padding = serializedObject.FindProperty("m_padding");
            m_startCorner = serializedObject.FindProperty("m_startCorner");
            m_startAxis = serializedObject.FindProperty("m_startAxis");
            m_childAlignment = serializedObject.FindProperty("m_childAlignment");
            m_constraint = serializedObject.FindProperty("m_constraint");
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
                EditorGUILayout.PropertyField(m_AAMVUpdateRate, EditorGUIUtility.TrTextContent("Update Rate"));
                EditorGUILayout.PropertyField(m_childrenPool, EditorGUIUtility.TrTextContent("Item Pool"));
                EditorGUI.indentLevel--;
                EditorGUILayout.LabelField(new GUIContent("GridLayout"));
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(m_cellSize);
                EditorGUILayout.PropertyField(m_spacing);
                EditorGUILayout.PropertyField(m_padding);
                EditorGUILayout.PropertyField(m_startCorner);
                EditorGUILayout.PropertyField(m_startAxis);
                EditorGUILayout.PropertyField(m_childAlignment);
                EditorGUILayout.PropertyField(m_constraint);
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}