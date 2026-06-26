using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UI;

using UnityEngine;

using UnityEngine.UI;

using XericUI.OverrideUI;

namespace XericUIEditor.OverrideUI
{
	/// <summary>
	/// XericStyleToggle 的 Inspector 编辑器。
	/// 完全替换 Selectable 的 Transition/ColorBlock/SpriteState 区域为 StyleSheet 状态块，
	/// 保留 Interactable、Navigation、isOn、graphic、group、onValueChanged 等基础功能。
	/// </summary>
	[CustomEditor(typeof(XericStyleToggle), true)]
	[CanEditMultipleObjects]
	internal class XericStyleToggleEditor : SelectableEditor
	{
		// Selectable 基础
		private SerializedProperty m_InteractableProp;
		private SerializedProperty m_NavigationProp;

		// Toggle 基础
		private SerializedProperty m_IsOnProp;
		private SerializedProperty m_TransitionProp;
		private SerializedProperty m_GraphicProp;
		private SerializedProperty m_GroupProp;
		private SerializedProperty m_OnValueChangedProp;

		// StyleSheet 专用
		private SerializedProperty m_ColorStateProp;
		private SerializedProperty m_TextureStateProp;
		private SerializedProperty m_OnOverrideColorProp;
		private SerializedProperty m_OnOverrideTextureProp;
		private SerializedProperty m_CheckmarkStyleProp;
		private SerializedProperty m_EnableStyleTransitionProp;

		protected override void OnEnable()
		{
			base.OnEnable();

			m_InteractableProp = serializedObject.FindProperty("m_Interactable");
			m_NavigationProp = serializedObject.FindProperty("m_Navigation");

			m_IsOnProp = serializedObject.FindProperty("m_IsOn");
			m_TransitionProp = serializedObject.FindProperty("toggleTransition");
			m_GraphicProp = serializedObject.FindProperty("graphic");
			m_GroupProp = serializedObject.FindProperty("m_Group");
			m_OnValueChangedProp = serializedObject.FindProperty("onValueChanged");

			m_ColorStateProp = serializedObject.FindProperty("m_ColorState");
			m_TextureStateProp = serializedObject.FindProperty("m_TextureState");
			m_OnOverrideColorProp = serializedObject.FindProperty("m_OnOverrideColor");
			m_OnOverrideTextureProp = serializedObject.FindProperty("m_OnOverrideTexture");
			m_CheckmarkStyleProp = serializedObject.FindProperty("m_CheckmarkStyle");
			m_EnableStyleTransitionProp = serializedObject.FindProperty("m_EnableStyleTransition");
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();
			XericStyleToggle toggle = target as XericStyleToggle;

			// === 1. Interactable ===
			EditorGUILayout.PropertyField(m_InteractableProp);

			// === 2. 样式表状态块（替代 Transition + ColorBlock + SpriteState） ===
			EditorGUILayout.Space();
			EditorGUILayout.LabelField("样式表配置（替代 Transition）", EditorStyles.boldLabel);

			EditorGUILayout.PropertyField(m_ColorStateProp, new GUIContent("颜色状态块"), true);
			EditorGUILayout.Space(4);
			EditorGUILayout.PropertyField(m_TextureStateProp, new GUIContent("纹理状态块"), true);

			bool anyExpanded = m_ColorStateProp.isExpanded || m_TextureStateProp.isExpanded;
			if (anyExpanded)
			{
				EditorGUILayout.Space(2);
				EditorGUILayout.PropertyField(m_OnOverrideColorProp);
				EditorGUILayout.PropertyField(m_OnOverrideTextureProp);
				EditorGUILayout.PropertyField(m_CheckmarkStyleProp);
				EditorGUILayout.PropertyField(m_EnableStyleTransitionProp);
			}

			// === 3. isOn ===
			EditorGUILayout.Space();
			EditorGUI.BeginChangeCheck();
			EditorGUILayout.PropertyField(m_IsOnProp);
			if (EditorGUI.EndChangeCheck())
			{
				if (!Application.isPlaying)
					EditorSceneManager.MarkSceneDirty(toggle!.gameObject.scene);

				ToggleGroup group = m_GroupProp.objectReferenceValue as ToggleGroup;
				toggle!.isOn = m_IsOnProp.boolValue;

				if (group != null && group.isActiveAndEnabled && toggle!.IsActive())
				{
					if (toggle!.isOn || (!group.AnyTogglesOn() && !group.allowSwitchOff))
					{
						toggle!.isOn = true;
						group.NotifyToggleOn(toggle!);
					}
				}
			}

			// === 4. Toggle 切换效果 & Graphic ===
			EditorGUILayout.PropertyField(m_TransitionProp);
			EditorGUILayout.PropertyField(m_GraphicProp);

			// === 5. Group ===
			EditorGUI.BeginChangeCheck();
			EditorGUILayout.PropertyField(m_GroupProp);
			if (EditorGUI.EndChangeCheck())
			{
				if (!Application.isPlaying)
					EditorSceneManager.MarkSceneDirty(toggle!.gameObject.scene);
				toggle!.group = m_GroupProp.objectReferenceValue as ToggleGroup;
			}

			// === 6. Navigation ===
			EditorGUILayout.Space();
			EditorGUILayout.PropertyField(m_NavigationProp);

			// === 7. onValueChanged ===
			EditorGUILayout.Space();
			EditorGUILayout.PropertyField(m_OnValueChangedProp);

			serializedObject.ApplyModifiedProperties();
		}
	}
}
