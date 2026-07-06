using UnityEngine;
using UnityEditor;
using UnityEditor.UI;

using XericUI.OverrideUI;

namespace XericUIEditor.OverrideUI
{
	/// <summary>
	/// XericStyleImage 的 Inspector 编辑器——
	/// 仅显示颜色样式字段 + raycastTarget / raycastPadding / maskable。
	/// 不展示原始 Color/Sprite/Material（被样式表接管）。
	/// </summary>
	[CustomEditor(typeof(XericStyleImage), true)]
	[CanEditMultipleObjects]
	internal class XericStyleImageEditor : GraphicEditor
	{
		private SerializedProperty m_ColorStyleProp;
		private SerializedProperty m_TextureStyleProp;
		private SerializedProperty m_MaterialStyleProp;
		private SerializedProperty m_RaycastTargetProp;
		private SerializedProperty m_RaycastPaddingProp;
		private SerializedProperty m_MaskableProp;
		private SerializedProperty m_ColorProp;

		// 折叠状态
		private static bool s_TextureFoldout = false;

		protected override void OnEnable()
		{
			base.OnEnable();

			m_ColorStyleProp   = serializedObject.FindProperty("m_ColorStyle");
			m_TextureStyleProp = serializedObject.FindProperty("m_TextureStyle");
			m_MaterialStyleProp = serializedObject.FindProperty("m_MaterialStyle");
			m_RaycastTargetProp = serializedObject.FindProperty("m_RaycastTarget");
			m_RaycastPaddingProp = serializedObject.FindProperty("m_RaycastPadding");
			m_MaskableProp      = serializedObject.FindProperty("m_Maskable");
			m_ColorProp         = serializedObject.FindProperty("m_Color");
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			// === 颜色样式（主要展示） ===
			EditorGUILayout.LabelField("样式表配置", EditorStyles.boldLabel);
			EditorGUILayout.Space(2);
			if (m_ColorStyleProp != null)
				EditorGUILayout.PropertyField(m_ColorStyleProp, new GUIContent("颜色样式"));

			// 纹理和材质样式折叠（通常不需要改动）
			s_TextureFoldout = EditorGUILayout.Foldout(s_TextureFoldout, "纹理 / 材质样式（高级）", true);
			if (s_TextureFoldout)
			{
				EditorGUI.indentLevel++;
				if (m_TextureStyleProp != null)
					EditorGUILayout.PropertyField(m_TextureStyleProp, new GUIContent("纹理样式"));
				if (m_MaterialStyleProp != null)
					EditorGUILayout.PropertyField(m_MaterialStyleProp, new GUIContent("材质样式"));
				EditorGUI.indentLevel--;
			}

			EditorGUILayout.Space(8);

			// === 颜色预览（只读） ===
			if (m_ColorProp != null)
			{
				EditorGUI.BeginDisabledGroup(true);
				EditorGUILayout.PropertyField(m_ColorProp, new GUIContent("颜色（预览）"));
				EditorGUI.EndDisabledGroup();
			}

			EditorGUILayout.Space(4);

			// === Raycast 与 Mask ===
			EditorGUILayout.LabelField("交互", EditorStyles.boldLabel);
			if (m_RaycastTargetProp != null)
				EditorGUILayout.PropertyField(m_RaycastTargetProp, new GUIContent("Raycast Target"));
			if (m_RaycastPaddingProp != null)
				EditorGUILayout.PropertyField(m_RaycastPaddingProp, new GUIContent("Raycast Padding"));
			if (m_MaskableProp != null)
				EditorGUILayout.PropertyField(m_MaskableProp, new GUIContent("Maskable"));

			serializedObject.ApplyModifiedProperties();
		}
	}
}
