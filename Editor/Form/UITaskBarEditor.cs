using UnityEditor;
using UnityEngine;

namespace XericUIEditor.Form
{
    [CustomEditor(typeof(XericUI.Form.UITaskBar))]
    public class UITaskBarEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("任务栏绑定", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Manager"), new GUIContent("窗口管理器 (Manager)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_ItemPrefab"), new GUIContent("图标预制体 (Item Prefab)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_ItemsContainer"), new GUIContent("图标容器 (Container)"));

            serializedObject.ApplyModifiedProperties();
        }
    }

    [CustomEditor(typeof(XericUI.Form.UITaskBarItem))]
    public class UITaskBarItemEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("任务栏图标项控件", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Icon"), new GUIContent("图标 (Icon)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_TitleText"), new GUIContent("标题文本 (Title)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_ProgressBar"), new GUIContent("进度条 (Progress Bar)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Highlight"), new GUIContent("聚焦高亮 (Highlight)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Button"), new GUIContent("按钮 (Button)"));

            serializedObject.ApplyModifiedProperties();
        }
    }
}
