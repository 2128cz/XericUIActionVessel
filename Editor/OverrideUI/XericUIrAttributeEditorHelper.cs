using UnityEditor;

namespace XericUIEditor.OverrideUI
{
    /// <summary>
    /// XericUIrAttribute* 组件的 Editor 通用辅助方法
    /// </summary>
    internal static class XericUIrAttributeEditorHelper
    {
        /// <summary>
        /// 绘制 _entryTag 和 _tagIndex 字段
        /// 在 OnInspectorGUI 末尾调用
        /// </summary>
        public static void DrawAttributeTagFields(SerializedObject serializedObject)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Reflection UI Attribute Tag", EditorStyles.boldLabel);

            serializedObject.Update();

            var entryTagProp = serializedObject.FindProperty("_entryTag");
            var tagIndexProp = serializedObject.FindProperty("_tagIndex");

            if (entryTagProp != null)
                EditorGUILayout.PropertyField(entryTagProp);

            if (tagIndexProp != null)
                EditorGUILayout.PropertyField(tagIndexProp);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
