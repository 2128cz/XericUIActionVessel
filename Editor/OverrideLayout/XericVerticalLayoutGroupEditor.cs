using UnityEditor;
using UnityEditor.UI;
using UnityEngine;
using XericUI.OverrideLayout;

namespace XericUIEditor.OverrideLayout
{
    [CustomEditor(typeof(XericHorizontalOrVerticalLayoutGroup), true)]
    [CanEditMultipleObjects]
    public class XericHorizontalOrVerticalLayoutGroupEditor : HorizontalOrVerticalLayoutGroupEditor
    {
        SerializedProperty ignoreInvisible;

        protected override void OnEnable()
        {
            base.OnEnable();
            ignoreInvisible = serializedObject.FindProperty("layoutIgnoreInactive");
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            
            serializedObject.Update();
            
            EditorGUILayout.PropertyField(ignoreInvisible, true);

            serializedObject.ApplyModifiedProperties();
        }

    }
}