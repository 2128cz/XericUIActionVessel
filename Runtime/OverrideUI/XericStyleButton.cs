using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using XericLibrary.Runtime.SuperStyleSheet;
using XericUI.Core.StyleSheetUI;
using XericUI.Helper;

namespace XericUI.OverrideUI
{
	/// <summary>
	/// 基于 SuperStyleSheet 的按钮组件。
	/// 通过两个独立的 StyleSheetState 实例分别管理颜色和纹理的五态样式切换，
	/// 替代 Unity 内置的 ColorBlock 和 SpriteState。
	/// </summary>
	[AddComponentMenu("Xeric UI Vessel/UI/Style Button", 51)]
	public class XericStyleButton : Button, IStyleRefreshable
	{
		#region 序列化字段

		[Header("样式表状态")]
		[SerializeField]
		[Tooltip("五态颜色样式块")]
		private StyleSheetState m_ColorState = StyleSheetState.Default;

		[SerializeField]
		[Tooltip("五态纹理样式块")]
		private StyleSheetState m_TextureState = StyleSheetState.Default;

		[SerializeField]
		[Tooltip("启用样式表驱动过渡（关闭则回退为 Unity ColorBlock）")]
		private bool m_EnableStyleTransition = true;

		#endregion

		#region 运行时

		[System.NonSerialized] private StyleSheetSelectionState m_CurrentState = StyleSheetSelectionState.Normal;
		[System.NonSerialized] private Graphic m_TargetGraphic;
		[System.NonSerialized] private Image m_TargetImage;

		#endregion

		#region 生命周期

		protected override void Awake()
		{
			base.Awake();
			m_TargetGraphic = targetGraphic;
			m_TargetImage = m_TargetGraphic as Image;
		}

		protected override void OnEnable()
		{
			base.OnEnable();

			RevalidateStyles();

			StyleRefreshManager.Register(this);
			ApplyForState(m_CurrentState);
		}

#if UNITY_EDITOR
		protected override void OnValidate()
		{
			base.OnValidate();
			RevalidateStyles();
			ApplyForState(m_CurrentState);
		}
#endif

		private void RevalidateStyles()
		{
			m_ColorState?.UnsubscribeAll(OnStyleChanged);
			m_TextureState?.UnsubscribeAll(OnStyleChanged);

			m_ColorState?.RegisterAll();
			m_TextureState?.RegisterAll();
			m_ColorState?.SubscribeAll(OnStyleChanged);
			m_TextureState?.SubscribeAll(OnStyleChanged);
		}

		protected override void OnDisable()
		{
			StyleRefreshManager.Unregister(this);

			m_ColorState?.UnsubscribeAll(OnStyleChanged);
			m_TextureState?.UnsubscribeAll(OnStyleChanged);
			m_ColorState?.UnregisterAll();
			m_TextureState?.UnregisterAll();

			base.OnDisable();
		}

		#endregion

		#region 状态过渡

		protected override void DoStateTransition(SelectionState state, bool instant)
		{
			if (!gameObject.activeInHierarchy) return;

			m_CurrentState = (StyleSheetSelectionState)(int)state;

			if (m_EnableStyleTransition)
				ApplyForState(m_CurrentState, instant);
			else
				base.DoStateTransition(state, instant);
		}

		#endregion

		#region 样式应用

		public void RefreshStyle()
		{
			ApplyForState(m_CurrentState, false);
		}

		private void ApplyForState(StyleSheetSelectionState state, bool instant = false)
		{
			// 颜色
			if (m_ColorState != null)
			{
				StyleField colorField = m_ColorState.GetForState(state);
				if (colorField != null && m_TargetGraphic != null)
				{
					StyleValue val = colorField.GetValue();
					if (val != null)
						m_TargetGraphic.CrossFadeColor((Color)val,
							instant ? 0f : m_ColorState.FadeDuration, true, true);
				}
			}

			// 纹理
			if (m_TextureState != null && m_TargetImage != null)
			{
				StyleField texField = m_TextureState.GetForState(state);
				if (texField != null)
				{
					StyleValue val = texField.GetValue();
					Texture2D tex = val?.GetValueAs<Texture2D>();
					if (tex != null)
						m_TargetImage.overrideSprite = Sprite.Create(tex,
							new Rect(0, 0, tex.width, tex.height),
							new Vector2(0.5f, 0.5f));
				}
			}
		}

		private void OnStyleChanged(StyleValue _)
		{
			if (gameObject.activeInHierarchy)
				ApplyForState(m_CurrentState);
		}

		#endregion

		#region 组件替换

#if UNITY_EDITOR
		[UnityEditor.MenuItem("CONTEXT/XericStyleButton/Xeric UI/强制刷新样式", false, 1)]
		private static void ForceRefreshStyle(UnityEditor.MenuCommand command)
		{
			StyleRefreshManager.ForceRefreshAll();
		}

		[UnityEditor.MenuItem("CONTEXT/Button/Replace To/Xeric (Style)", true)]
		private static bool ValidateReplaceToStyleButton(UnityEditor.MenuCommand command)
			=> command.context is Button and not XericStyleButton;

		[UnityEditor.MenuItem("CONTEXT/Button/Replace To/Xeric (Style)", false, 11)]
		private static void ReplaceToStyleButton(UnityEditor.MenuCommand command)
			=> command.ReplaceCommandContext<Button, XericStyleButton,
				(Graphic targetGraphic, ButtonClickedEvent onClick, Navigation navigation)>(
				o => (o.targetGraphic, o.onClick, o.navigation),
				(t, d) =>
				{
					t.targetGraphic = d.targetGraphic;
					t.onClick = d.onClick;
					t.navigation = d.navigation;
				});

		[UnityEditor.MenuItem("CONTEXT/Button/Replace To/Unity Button", true)]
		private static bool ValidateReplaceStyleToUnity(UnityEditor.MenuCommand command)
			=> command.context is XericStyleButton;

		[UnityEditor.MenuItem("CONTEXT/Button/Replace To/Unity Button", false, 100)]
		private static void ReplaceStyleToUnity(UnityEditor.MenuCommand command)
			=> command.ReplaceCommandContext<XericStyleButton, Button,
				(Graphic targetGraphic, ButtonClickedEvent onClick, Navigation navigation)>(
				o => (o.targetGraphic, o.onClick, o.navigation),
				(t, d) =>
				{
					t.targetGraphic = d.targetGraphic;
					t.onClick = d.onClick;
					t.navigation = d.navigation;
				});
#endif

		#endregion
	}
}
