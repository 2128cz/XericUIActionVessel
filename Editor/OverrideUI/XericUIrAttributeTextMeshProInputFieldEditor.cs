using UnityEditor;
using UnityEngine;
using XericUI.OverrideUI;

namespace XericUIEditor.OverrideUI
{
    [CustomEditor(typeof(XericUIrAttributeTextMeshProInputField))]
    [CanEditMultipleObjects]
    internal class XericUIrAttributeTextMeshProInputFieldEditor : Editor
    {
#if dUI_TextMeshPro
        SerializedProperty _targetInputFieldProp;
#endif
        SerializedProperty _entryTagProp;
        SerializedProperty _tagIndexProp;

        private void OnEnable()
        {
#if dUI_TextMeshPro
            _targetInputFieldProp = serializedObject.FindProperty("_targetInputField");
#endif
            _entryTagProp = serializedObject.FindProperty("_entryTag");
            _tagIndexProp = serializedObject.FindProperty("_tagIndex");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("XericUIrAttributeTextMeshProInputField", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "此组件为 TMP_InputField 的标记附加器，需挂在与 TMP_InputField 同一个 GameObject 上。\n" +
                "_entryTag 和 _tagIndex 用于反射界面生成器自动绑定。",
                MessageType.Info);

#if dUI_TextMeshPro
            EditorGUILayout.PropertyField(_targetInputFieldProp);
#endif

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Reflection UI Attribute Tag", EditorStyles.boldLabel);

            if (_entryTagProp != null)
                EditorGUILayout.PropertyField(_entryTagProp);

            if (_tagIndexProp != null)
                EditorGUILayout.PropertyField(_tagIndexProp);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
