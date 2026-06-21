using UnityEditor;
using UnityEditor.UI;
using XericUI.OverrideUI;

namespace XericUIEditor.OverrideUI
{
    [CustomEditor(typeof(XericUIrAttributeDropdown))]
    [CanEditMultipleObjects]
    internal class XericUIrAttributeDropdownEditor : DropdownEditor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            XericUIrAttributeEditorHelper.DrawAttributeTagFields(serializedObject);
        }
    }
}
