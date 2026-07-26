using UnityEditor;
using UnityEngine;

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

        #region 基本设置

        private void DrawBasicSection()
        {
            EditorGUILayout.LabelField("窗口基本设置", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_WindowTitle"), new GUIContent("窗口标题 (Title)"));
            EditorGUILayout.Space();
        }

        #endregion

        #region 边缘行为

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

        #endregion

        #region 调试与测试

        private void DrawDebugSection()
        {
            EditorGUILayout.LabelField("调试", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_DebugMode"), new GUIContent("调试模式 (Debug Mode)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_DebugLogEvents"), new GUIContent("事件日志 (Log Events)"));

            EditorGUILayout.Space();

            // 当前状态
            EditorGUILayout.LabelField("当前状态", EditorStyles.boldLabel);

            var state = _controller.CurrentState;
            Color stateColor;
            string stateText;

            switch (state)
            {
                case XericUI.Form.WindowState.Normal:
                    stateColor = Color.white;
                    stateText = "● 窗口化 (Normal)";
                    break;
                case XericUI.Form.WindowState.Maximized:
                    stateColor = Color.green;
                    stateText = "■ 全屏 (Maximized)";
                    break;
                case XericUI.Form.WindowState.Minimized:
                    stateColor = Color.gray;
                    stateText = "○ 最小化 (Minimized)";
                    break;
                case XericUI.Form.WindowState.EdgeHidden:
                    stateColor = new Color(1f, 0.6f, 0f);
                    stateText = "◄ 边缘隐藏 (Edge Hidden)";
                    break;
                default:
                    stateColor = Color.white;
                    stateText = state.ToString();
                    break;
            }

            GUI.color = stateColor;
            EditorGUILayout.LabelField(stateText, EditorStyles.miniBoldLabel);
            GUI.color = Color.white;

            EditorGUILayout.Space();

            // 手动测试按钮
            EditorGUILayout.LabelField("手动测试", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("全屏/还原"))
            {
                _controller.ToggleMaximize();
                SceneView.RepaintAll();
            }
            if (GUILayout.Button("模拟进入"))
            {
                _controller.SimulateEnter();
                SceneView.RepaintAll();
            }
            if (GUILayout.Button("模拟退出"))
            {
                _controller.SimulateExit();
                SceneView.RepaintAll();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // 调试提示
            if (!Application.isPlaying && _controller.DebugMode)
            {
                EditorGUILayout.HelpBox(
                    "调试模式已启用 — 在 SceneView 中移动鼠标即可预览。\n" +
                    "蓝色边框 = 窗口范围 | 绿色 = 鼠标悬停中 | 橙色 = 吸附阈值范围",
                    MessageType.Info);
            }
        }

        #endregion
    }
}
