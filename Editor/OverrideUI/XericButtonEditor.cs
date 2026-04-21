using UnityEditor;
using UnityEditor.UI;
using XericUI.OverrideUI;

namespace XericUIEditor.OverrideUI
{
    [CustomEditor(typeof(XericButton), true)]
    [CanEditMultipleObjects]
    /// <summary>
    ///   Custom Editor for the Button Component.
    ///   Extend this class to write a custom editor for a component derived from Button.
    /// </summary>
    internal class XericButtonEditor : SelectableEditor
    {
        SerializedProperty m_OnClickProperty;
        
        SerializedProperty m_TextProperty;
        SerializedProperty m_EnableTextColorTransitionProperty;
        SerializedProperty m_TextTransitionColorModeProperty;
        SerializedProperty m_enableTextContextTransitionProperty;
        SerializedProperty m_textTransitionContextModeProperty;
        
        protected override void OnEnable()
        {
            base.OnEnable();
            m_OnClickProperty = serializedObject.FindProperty("m_OnClick");
            
            m_TextProperty = serializedObject.FindProperty("m_text");
            m_EnableTextColorTransitionProperty = serializedObject.FindProperty("m_enableTextColorTransition");
            m_TextTransitionColorModeProperty = serializedObject.FindProperty("m_textTransitionColorMode");
            m_enableTextContextTransitionProperty = serializedObject.FindProperty("m_enableTextContextTransition");
            m_textTransitionContextModeProperty = serializedObject.FindProperty("m_textTransitionContextMode");
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            EditorGUILayout.Space();

            serializedObject.Update();
            
            EditorGUILayout.PropertyField(m_TextProperty);
            EditorGUILayout.PropertyField(m_EnableTextColorTransitionProperty);
            if (m_EnableTextColorTransitionProperty.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(m_TextTransitionColorModeProperty);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.PropertyField(m_enableTextContextTransitionProperty);
            if (m_enableTextContextTransitionProperty.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(m_textTransitionContextModeProperty);
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.PropertyField(m_OnClickProperty);
            serializedObject.ApplyModifiedProperties();
        }
    }
}