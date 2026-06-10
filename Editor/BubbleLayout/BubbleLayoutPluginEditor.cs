using UnityEditor;
using UnityEngine;
using XericUI.BubbleLayout;

namespace XericUIEditor.BubbleLayout
{
	/// <summary>
	/// 气泡布局插件通用 Inspector —— 白色圆角边框 + 深色底，对所有 BubbleLayoutPluginBase 子类生效
	/// </summary>
	[CustomEditor(typeof(BubbleLayoutPluginBase), true)]
	[CanEditMultipleObjects]
	internal class BubbleLayoutPluginEditor : Editor
	{
		private static GUIStyle s_BoxStyle;
		private static bool s_StylesInitialized;

		private static void EnsureStyles()
		{
			if (s_StylesInitialized)
				return;

			s_BoxStyle = new GUIStyle(GUI.skin.box)
			{
				padding = new RectOffset(12, 12, 10, 10),
				margin = new RectOffset(4, 4, 4, 4),
			};

			var bgTex = new Texture2D(1, 1);
			bgTex.SetPixel(0, 0, new Color(0.18f, 0.18f, 0.18f, 1f));
			bgTex.Apply();
			s_BoxStyle.normal.background = bgTex;

			s_StylesInitialized = true;
		}

		public override void OnInspectorGUI()
		{
			EnsureStyles();

			// ── 白色圆角边框 + 深色底 ──
			EditorGUILayout.BeginVertical(s_BoxStyle);

			// 绘制所有序列化字段（包括基类的 Enabled 和 Order）
			serializedObject.Update();

			SerializedProperty iterator = serializedObject.GetIterator();
			bool enterChildren = true;

			while (iterator.NextVisible(enterChildren))
			{
				enterChildren = false;

				// 跳过脚本引用字段
				if (iterator.name == "m_Script")
					continue;

				EditorGUILayout.PropertyField(iterator, true);
			}

			serializedObject.ApplyModifiedProperties();

			EditorGUILayout.EndVertical();
		}
	}
}
