using UnityEditor;
using UnityEditor.UI;

using UnityEngine;
using UnityEngine.UI;

using XericUI.OverrideUI;

namespace XericUIEditor.OverrideUI
{
	/// <summary>
	/// XericStyleButton 的 Inspector 编辑器。
	/// 完全替换 Selectable 的 Transition/ColorBlock/SpriteState 区域为 StyleSheet 状态块，
	/// 保留 Interactable、Navigation、onClick 等基础功能。
	/// </summary>
	[CustomEditor(typeof(XericStyleButton), true)]
	[CanEditMultipleObjects]
	internal class XericStyleButtonEditor : SelectableEditor
	{
		private SerializedProperty m_InteractableProp;
		private SerializedProperty m_TargetGraphicProp;
		private SerializedProperty m_NavigationProp;

		private SerializedProperty m_ColorStateProp;
		private SerializedProperty m_TextureStateProp;
		private SerializedProperty m_EnableStyleTransitionProp;
		private SerializedProperty m_OnClickProp;

		protected override void OnEnable()
		{
			base.OnEnable();

			m_InteractableProp = serializedObject.FindProperty("m_Interactable");
			m_TargetGraphicProp = serializedObject.FindProperty("m_TargetGraphic");
			m_NavigationProp = serializedObject.FindProperty("m_Navigation");

			m_ColorStateProp = serializedObject.FindProperty("m_ColorState");
			m_TextureStateProp = serializedObject.FindProperty("m_TextureState");
			m_EnableStyleTransitionProp = serializedObject.FindProperty("m_EnableStyleTransition");
			m_OnClickProp = serializedObject.FindProperty("m_OnClick");
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			// === 1. Interactable ===
			EditorGUILayout.PropertyField(m_InteractableProp);

			// === 2. 样式表状态块（替代 Transition + ColorBlock + SpriteState） ===
			EditorGUILayout.Space();
			EditorGUILayout.LabelField("样式表配置（替代 Transition）", EditorStyles.boldLabel);

			EditorGUILayout.PropertyField(m_ColorStateProp, new GUIContent("颜色状态块"), true);
			EditorGUILayout.Space(4);
			EditorGUILayout.PropertyField(m_TextureStateProp, new GUIContent("纹理状态块"), true);

			if (m_ColorStateProp.isExpanded || m_TextureStateProp.isExpanded)
			{
				EditorGUILayout.Space(2);
				EditorGUILayout.PropertyField(m_EnableStyleTransitionProp);
			}

			// === 3. Target Graphic ===
			EditorGUILayout.Space();
			EditorGUILayout.PropertyField(m_TargetGraphicProp);

			Graphic graphic = m_TargetGraphicProp.objectReferenceValue as Graphic;
			if (graphic == null)
				graphic = (target as Selectable)?.GetComponent<Graphic>();
			if (graphic == null)
				EditorGUILayout.HelpBox("你需要指定一个 Graphic 目标才能使用样式表颜色过渡。", MessageType.Warning);

			// === 4. Navigation ===
			EditorGUILayout.Space();
			EditorGUILayout.PropertyField(m_NavigationProp);

			// === 5. onClick ===
			EditorGUILayout.Space();
			EditorGUILayout.PropertyField(m_OnClickProp);

			serializedObject.ApplyModifiedProperties();
		}
	}
}
