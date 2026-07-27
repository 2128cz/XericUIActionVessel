using UnityEditor;
using UnityEngine;
using XericUI.Form;

namespace XericUIEditor.Form
{
    [CustomEditor(typeof(XericUI.Form.UIFormController))]
    public class UIFormControllerEditor : Editor
    {
        private XericUI.Form.UIFormController _controller;

        private void OnEnable()
        {
            _controller = (XericUI.Form.UIFormController)target;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawBasicSection();
            DrawEdgeSection();
            DrawDebugSection();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawBasicSection()
        {
            EditorGUILayout.LabelField("窗口基本设置", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_WindowTitle"), new GUIContent("窗口标题 (Title)"));
            EditorGUILayout.Space();
        }

        private void DrawEdgeSection()
        {
            EditorGUILayout.LabelField("边缘吸附", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_EnableAutoSnap"), new GUIContent("启用边缘吸附 (Auto Snap)"));
            if (_controller.AutoSnapEnabled)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_SnapThreshold"), new GUIContent("吸附阈值 (Snap Threshold, px)"));
            }

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("自动隐藏", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_EnableAutoHide"), new GUIContent("启用自动隐藏 (Auto Hide)"));
            if (_controller.AutoHideEnabled)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_AutoHideDirection"), new GUIContent("隐藏方向 (Direction)"));
            }

            EditorGUILayout.Space();
        }

        private void DrawDebugSection()
        {
            EditorGUILayout.LabelField("调试", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_DebugMode"), new GUIContent("调试模式 (Debug Mode)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_DebugLogEvents"), new GUIContent("事件日志 (Log Events)"));

            EditorGUILayout.Space();

            // 当前状态
            EditorGUILayout.LabelField("当前状态", EditorStyles.boldLabel);
            var state = _controller.CurrentState;
            Color stateColor = state switch
            {
                XericUI.Form.WindowState.Normal => Color.white,
                XericUI.Form.WindowState.Maximized => Color.green,
                XericUI.Form.WindowState.Minimized => Color.gray,
                XericUI.Form.WindowState.EdgeHidden => new Color(1f, 0.6f, 0f),
                _ => Color.white
            };
            var entries = _controller.Manager?.GetAllEntries();
            WindowEntry entry = _controller.Entry;
            string extra = entry != null ? $" | ID={entry.Id}" : "";

            GUI.color = stateColor;
            EditorGUILayout.LabelField($"{state}{extra}", EditorStyles.miniBoldLabel);
            GUI.color = Color.white;

            EditorGUILayout.Space();

            // 手动测试
            EditorGUILayout.LabelField("手动测试", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("全屏/还原"))
            {
                _controller.ToggleMaximize();
                SceneView.RepaintAll();
            }
            if (GUILayout.Button("置顶 (Focus)"))
            {
                _controller.MoveToFront();
                SceneView.RepaintAll();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            if (!Application.isPlaying && _controller.DebugMode)
            {
                EditorGUILayout.HelpBox(
                    "调试模式已启用 — SceneView 中移动鼠标实时预览。\n" +
                    "蓝色边框=窗口范围 | 绿色=鼠标悬停中",
                    MessageType.Info);
            }
        }
    }
}
