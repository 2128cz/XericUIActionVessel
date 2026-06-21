using UnityEditor;
using UnityEditor.UI;
using XericUI.OverrideUI;

namespace XericUIEditor.OverrideUI
{
    [CustomEditor(typeof(XericUIrAttributeInputField))]
    [CanEditMultipleObjects]
    internal class XericUIrAttributeInputFieldEditor : InputFieldEditor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            XericUIrAttributeEditorHelper.DrawAttributeTagFields(serializedObject);
        }
    }
}
