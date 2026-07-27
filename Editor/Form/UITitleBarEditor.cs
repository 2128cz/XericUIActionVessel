using UnityEditor;
using UnityEngine;

namespace XericUIEditor.Form
{
    [CustomEditor(typeof(XericUI.Form.UITitleBar))]
    public class UITitleBarEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("标题栏控件引用", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "将标题栏下的子控件拖入下方槽位。\n" +
                "UITitleBar 在 Start 时向上查找父级 UIFormController 并绑定事件。",
                MessageType.Info);

            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_IconImage"), new GUIContent("窗口图标 (Icon Image)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_TitleText"), new GUIContent("标题文本 (Title Text)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_CloseButton"), new GUIContent("关闭按钮 (Close)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_MaximizeToggle"), new GUIContent("最大化 Toggle (Maximize)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_MinimizeButton"), new GUIContent("最小化按钮 (Minimize)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_AutoSnapToggle"), new GUIContent("吸附开关 (Auto Snap)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_AutoHideToggle"), new GUIContent("隐藏开关 (Auto Hide)"));

            serializedObject.ApplyModifiedProperties();
        }
    }
}
