using UnityEditor;
using UnityEditor.UI;
using XericUI.OverrideUI;

namespace XericUIEditor.OverrideUI
{
    [CustomEditor(typeof(XericUIrAttributeSlider))]
    [CanEditMultipleObjects]
    internal class XericUIrAttributeSliderEditor : SliderEditor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            XericUIrAttributeEditorHelper.DrawAttributeTagFields(serializedObject);
        }
    }
}
