using UnityEditor;
using UnityEditor.UI;

using XericUI.OverrideUI;

namespace XericUIEditor.OverrideUI
{
	/// <summary>
	/// XericStyleImage 的 Inspector 编辑器——继承 GraphicEditor，
	/// 额外展示颜色、纹理、材质三个 StyleField 和自动刷新开关。
	/// </summary>
	[CustomEditor(typeof(XericStyleImage), true)]
	[CanEditMultipleObjects]
	internal class XericStyleImageEditor : GraphicEditor
	{
		private SerializedProperty m_ColorStyleProp;
		private SerializedProperty m_TextureStyleProp;
		private SerializedProperty m_MaterialStyleProp;
		private SerializedProperty m_AutoRefreshProp;

		protected override void OnEnable()
		{
			base.OnEnable();

			m_ColorStyleProp = serializedObject.FindProperty("m_ColorStyle");
			m_TextureStyleProp = serializedObject.FindProperty("m_TextureStyle");
			m_MaterialStyleProp = serializedObject.FindProperty("m_MaterialStyle");
			m_AutoRefreshProp = serializedObject.FindProperty("m_AutoRefresh");
		}

		public override void OnInspectorGUI()
		{
			base.OnInspectorGUI();
			EditorGUILayout.Space();

			serializedObject.Update();

			EditorGUILayout.LabelField("样式表配置", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(m_ColorStyleProp);
			EditorGUILayout.PropertyField(m_TextureStyleProp);
			EditorGUILayout.PropertyField(m_MaterialStyleProp);
			EditorGUILayout.PropertyField(m_AutoRefreshProp);

			serializedObject.ApplyModifiedProperties();
		}
	}
}
