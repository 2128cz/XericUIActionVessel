using UnityEditor;
using UnityEditor.UI;
using XericUI.OverrideUI;

namespace XericUIEditor.OverrideUI
{
    [CustomEditor(typeof(XericUIrAttributeText))]
    [CanEditMultipleObjects]
    internal class XericUIrAttributeTextEditor : ImageEditor
    {
        SerializedProperty _targetTextProp;

        protected override void OnEnable()
        {
            base.OnEnable();
            _targetTextProp = serializedObject.FindProperty("_targetText");
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            EditorGUILayout.Space();
            if (_targetTextProp != null)
                EditorGUILayout.PropertyField(_targetTextProp);

            XericUIrAttributeEditorHelper.DrawAttributeTagFields(serializedObject);
        }
    }
}
