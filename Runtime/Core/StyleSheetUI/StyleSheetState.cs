using System;

using UnityEngine;

using XericLibrary.Runtime.SuperStyleSheet;

namespace XericUI.Core.StyleSheetUI
{
	/// <summary>
	/// 样式表驱动的 UI 交互状态块——仿 ColorBlock 设计。
	/// 集中管理 Normal/Highlighted/Pressed/Selected/Disabled 五种状态的 StyleField。
	/// 职责单一：仅保存一组样式属性（如仅颜色、或仅纹理）。
	/// 在组件上声明多个实例分别实现颜色切换和纹理切换。
	/// </summary>
	[Serializable]
	public class StyleSheetState
	{
		#region 序列化字段

		[Header("五态样式路径")]
		[SerializeField]
		[Tooltip("正常状态")]
		private StyleField m_Normal = new StyleField();

		[SerializeField]
		[Tooltip("高亮状态")]
		private StyleField m_Highlighted = new StyleField();

		[SerializeField]
		[Tooltip("按下状态")]
		private StyleField m_Pressed = new StyleField();

		[SerializeField]
		[Tooltip("选中状态")]
		private StyleField m_Selected = new StyleField();

		[SerializeField]
		[Tooltip("禁用状态")]
		private StyleField m_Disabled = new StyleField();

		[Header("过渡")]
		[SerializeField]
		[Range(0.001f, 1f)]
		[Tooltip("渐变持续时间")]
		private float m_FadeDuration = 0.1f;

		#endregion

		#region 公开属性

		public StyleField Normal => m_Normal;
		public StyleField Highlighted => m_Highlighted;
		public StyleField Pressed => m_Pressed;
		public StyleField Selected => m_Selected;
		public StyleField Disabled => m_Disabled;

		public float FadeDuration
		{
			get => m_FadeDuration;
			set => m_FadeDuration = value;
		}

		/// <summary>所有 StyleField</summary>
		public StyleField[] AllFields => new[]
		{
			m_Normal, m_Highlighted, m_Pressed, m_Selected, m_Disabled
		};

		/// <summary>默认实例</summary>
		public static StyleSheetState Default => new StyleSheetState();

		#endregion

		#region 查找

		/// <summary>根据交互状态获取对应的 StyleField</summary>
		public StyleField GetForState(StyleSheetSelectionState state)
		{
			switch (state)
			{
				case StyleSheetSelectionState.Normal:      return m_Normal;
				case StyleSheetSelectionState.Highlighted: return m_Highlighted;
				case StyleSheetSelectionState.Pressed:     return m_Pressed;
				case StyleSheetSelectionState.Selected:    return m_Selected;
				case StyleSheetSelectionState.Disabled:    return m_Disabled;
				default:                                   return m_Normal;
			}
		}

		#endregion

		#region 注册 / 注销

		public void RegisterAll()
		{
			foreach (var f in AllFields) f?.Register();
		}

		public void UnregisterAll()
		{
			foreach (var f in AllFields) f?.Unregister();
		}

		public void SubscribeAll(Action<StyleValue> callback)
		{
			foreach (var f in AllFields) { if (f != null) f.Member.OnValueChanged += callback; }
		}

		public void UnsubscribeAll(Action<StyleValue> callback)
		{
			foreach (var f in AllFields) { if (f != null) f.Member.OnValueChanged -= callback; }
		}

		#endregion
	}
}
