using UnityEditor;
using XericUI.OverrideUI;

namespace XericUIEditor.OverrideUI
{
    [CustomEditor(typeof(XericUIrAttributeButton))]
    [CanEditMultipleObjects]
    internal class XericUIrAttributeButtonEditor : XericButtonEditor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            XericUIrAttributeEditorHelper.DrawAttributeTagFields(serializedObject);
        }
    }
}
