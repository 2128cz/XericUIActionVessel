using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UI;
using UnityEngine;
using UnityEngine.UI;

namespace XericUI
{
    [CustomEditor(typeof(XericToggle))]
    internal class XericToggleEditor : SelectableEditor
    {
        SerializedProperty m_OnValueChangedProperty;
        SerializedProperty m_TransitionProperty;
        SerializedProperty m_GraphicProperty;
        SerializedProperty m_GroupProperty;
        SerializedProperty m_IsOnProperty;

        SerializedProperty m_TextProperty;
        SerializedProperty m_EnableTextColorTransitionProperty;
        SerializedProperty m_TextTransitionColorModeProperty;
        SerializedProperty m_enableTextContextTransitionProperty;
        SerializedProperty m_textTransitionContextModeProperty;

        protected override void OnEnable()
        {
            base.OnEnable();

            m_TransitionProperty = serializedObject.FindProperty("toggleTransition");
            m_GraphicProperty = serializedObject.FindProperty("graphic");
            m_GroupProperty = serializedObject.FindProperty("m_Group");
            m_IsOnProperty = serializedObject.FindProperty("m_IsOn");
            m_OnValueChangedProperty = serializedObject.FindProperty("onValueChanged");

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
            Toggle toggle = serializedObject.targetObject as Toggle;
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(m_IsOnProperty);
            if (EditorGUI.EndChangeCheck())
            {
                if (!Application.isPlaying)
                    EditorSceneManager.MarkSceneDirty(toggle.gameObject.scene);

                ToggleGroup group = m_GroupProperty.objectReferenceValue as ToggleGroup;

                toggle.isOn = m_IsOnProperty.boolValue;

                if (group != null && group.isActiveAndEnabled && toggle.IsActive())
                {
                    if (toggle.isOn || (!group.AnyTogglesOn() && !group.allowSwitchOff))
                    {
                        toggle.isOn = true;
                        group.NotifyToggleOn(toggle);
                    }
                }
            }

            EditorGUILayout.PropertyField(m_TransitionProperty);
            EditorGUILayout.PropertyField(m_GraphicProperty);
            EditorGUILayout.PropertyField(m_TextProperty);
            EditorGUILayout.PropertyField(m_EnableTextColorTransitionProperty);
            if (EditorGUILayout.BeginFadeGroup(m_EnableTextColorTransitionProperty.boolValue ? 1 : 0))
            {
                EditorGUILayout.PropertyField(m_TextTransitionColorModeProperty);
            }
            EditorGUILayout.EndFadeGroup();

            EditorGUILayout.PropertyField(m_enableTextContextTransitionProperty);
            if (EditorGUILayout.BeginFadeGroup(m_enableTextContextTransitionProperty.boolValue ? 1 : 0))
            {
                EditorGUILayout.PropertyField(m_textTransitionContextModeProperty);
            }
            EditorGUILayout.EndFadeGroup();

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(m_GroupProperty);
            if (EditorGUI.EndChangeCheck())
            {
                if (!Application.isPlaying)
                    EditorSceneManager.MarkSceneDirty(toggle.gameObject.scene);

                ToggleGroup group = m_GroupProperty.objectReferenceValue as ToggleGroup;
                toggle.group = group;
            }

            EditorGUILayout.Space();

            // Draw the event notification options
            EditorGUILayout.PropertyField(m_OnValueChangedProperty);

            serializedObject.ApplyModifiedProperties();
        }
    }
}