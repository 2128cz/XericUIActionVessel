using UnityEditor;
using XericUI;
using XericUI.OverrideUI;

namespace XericUIEditor.OverrideUI
{
    [CustomEditor(typeof(XericUIrAttributeToggle))]
    internal class XericUIrAttributeToggleEditor : XericToggleEditor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            XericUIrAttributeEditorHelper.DrawAttributeTagFields(serializedObject);
        }
    }
}
