using UnityEditor;
using UnityEngine;
using XericUI.BubbleLayout;

namespace XericUIEditor.BubbleLayout
{
	/// <summary>
	/// 气泡布局插件通用 Inspector —— 白色圆角边框 + 深色底，对所有 BubbleLayoutPluginBase 子类生效。
	/// </summary>
	[CustomEditor(typeof(BubbleLayoutPluginBase), true)]
	[CanEditMultipleObjects]
	internal class BubbleLayoutPluginEditor : Editor
	{
		public override void OnInspectorGUI()
		{
			EditorGUILayout.BeginVertical(BubbleLayoutEditorStyles.BoxStyle);

			serializedObject.Update();

			SerializedProperty iterator = serializedObject.GetIterator();
			bool enterChildren = true;

			while (iterator.NextVisible(enterChildren))
			{
				enterChildren = false;

				if (iterator.name == "m_Script")
					continue;

				EditorGUILayout.PropertyField(iterator, true);
			}

			serializedObject.ApplyModifiedProperties();

			EditorGUILayout.EndVertical();
		}
	}
}
