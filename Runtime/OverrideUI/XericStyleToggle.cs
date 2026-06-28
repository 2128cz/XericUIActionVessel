using UnityEngine;
using UnityEngine.UI;

using XericLibrary.Runtime.SuperStyleSheet;
using XericUI.Core.StyleSheetUI;
using XericUI.Helper;

namespace XericUI.OverrideUI
{
	/// <summary>
	/// 基于 SuperStyleSheet 的开关组件。
	/// 使用两个 StyleSheetState 实例管理五态颜色和纹理样式，
	/// 额外支持 isOn 覆盖和 Checkmark 样式。
	/// </summary>
	[AddComponentMenu("Xeric UI Vessel/UI/Style Toggle", 52)]
	public class XericStyleToggle : Toggle, IStyleRefreshable
	{
		#region 序列化字段

		[Header("样式表状态")]
		[SerializeField]
		private StyleSheetState m_ColorState = StyleSheetState.Default;

		[SerializeField]
		private StyleSheetState m_TextureState = StyleSheetState.Default;

		[Header("Toggle 专用")]
		[SerializeField]
		[Tooltip("isOn=true 时的颜色覆盖")]
		private StyleField m_OnOverrideColor = new StyleField();

		[SerializeField]
		[Tooltip("isOn=true 时的纹理覆盖")]
		private StyleField m_OnOverrideTexture = new StyleField();

		[SerializeField]
		[Tooltip("Checkmark 打勾标志的样式")]
		private StyleField m_CheckmarkStyle = new StyleField();

		[SerializeField]
		[Tooltip("启用样式表驱动过渡")]
		private bool m_EnableStyleTransition = true;

		#endregion

		#region 运行时

		[System.NonSerialized] private StyleSheetSelectionState m_CurrentState = StyleSheetSelectionState.Normal;
		[System.NonSerialized] private Graphic m_TargetGraphic;
		[System.NonSerialized] private Image m_TargetImage;
		[System.NonSerialized] private Graphic m_CheckmarkGraphic;
		[System.NonSerialized] private Image m_CheckmarkImage;

		#endregion

		#region 生命周期

		protected override void Awake()
		{
			base.Awake();
			m_TargetGraphic = targetGraphic;
			m_TargetImage = m_TargetGraphic as Image;

			Transform checkmark = transform.Find("Background/Checkmark")
				?? transform.Find("Checkmark");
			if (checkmark != null)
			{
				m_CheckmarkGraphic = checkmark.GetComponent<Graphic>();
				m_CheckmarkImage = checkmark.GetComponent<Image>();
			}
		}

		protected override void OnEnable()
		{
			base.OnEnable();

			m_ColorState?.RegisterAll();
			m_TextureState?.RegisterAll();
			m_ColorState?.SubscribeAll(OnStyleChanged);
			m_TextureState?.SubscribeAll(OnStyleChanged);

			m_OnOverrideColor?.Register();
			m_OnOverrideColor.Member.OnValueChanged += OnStyleChanged;
			m_OnOverrideTexture?.Register();
			m_OnOverrideTexture.Member.OnValueChanged += OnStyleChanged;
			m_CheckmarkStyle?.Register();
			m_CheckmarkStyle.Member.OnValueChanged += OnStyleChanged;

			StyleRefreshManager.Register(this);
			onValueChanged.AddListener(OnToggleValueChanged);
			ApplyForState(m_CurrentState);
		}

		protected override void OnDisable()
		{
			StyleRefreshManager.Unregister(this);

			onValueChanged.RemoveListener(OnToggleValueChanged);

			m_ColorState?.UnsubscribeAll(OnStyleChanged);
			m_TextureState?.UnsubscribeAll(OnStyleChanged);
			m_ColorState?.UnregisterAll();
			m_TextureState?.UnregisterAll();
			if (m_OnOverrideColor != null) { m_OnOverrideColor.Member.OnValueChanged -= OnStyleChanged; m_OnOverrideColor.Unregister(); }
			if (m_OnOverrideTexture != null) { m_OnOverrideTexture.Member.OnValueChanged -= OnStyleChanged; m_OnOverrideTexture.Unregister(); }
			if (m_CheckmarkStyle != null) { m_CheckmarkStyle.Member.OnValueChanged -= OnStyleChanged; m_CheckmarkStyle.Unregister(); }

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

		private void OnToggleValueChanged(bool _)
		{
			if (m_EnableStyleTransition)
				ApplyForState(m_CurrentState);
		}

		#endregion

		#region 样式应用

		public void RefreshStyle()
		{
			ApplyForState(m_CurrentState, false);
		}

		private void ApplyForState(StyleSheetSelectionState state, bool instant = false)
		{
			// 颜色：On 覆盖优先
			StyleField colorField = isOn && m_OnOverrideColor?.GetValue() != null
				? m_OnOverrideColor
				: m_ColorState?.GetForState(state);

			if (colorField != null && m_TargetGraphic != null)
			{
				StyleValue val = colorField.GetValue();
				if (val != null)
				{
					float dur = instant ? 0f : (m_ColorState?.FadeDuration ?? 0.1f);
					m_TargetGraphic.CrossFadeColor((Color)val, dur, true, true);
				}
			}

			// 纹理：On 覆盖优先
			StyleField texField = isOn && m_OnOverrideTexture?.GetValue() != null
				? m_OnOverrideTexture
				: m_TextureState?.GetForState(state);

			if (texField != null && m_TargetImage != null)
			{
				StyleValue val = texField.GetValue();
				Texture2D tex = val?.GetValueAs<Texture2D>();
				if (tex != null)
					m_TargetImage.overrideSprite = Sprite.Create(tex,
						new Rect(0, 0, tex.width, tex.height),
						new Vector2(0.5f, 0.5f));
			}

			// Checkmark
			if (m_CheckmarkGraphic != null && m_CheckmarkStyle != null)
			{
				StyleValue val = m_CheckmarkStyle.GetValue();
				if (val != null) m_CheckmarkGraphic.color = val;
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
		[UnityEditor.MenuItem("CONTEXT/XericStyleToggle/Xeric UI/强制刷新样式", false, 1)]
		private static void ForceRefreshStyle(UnityEditor.MenuCommand command)
		{
			StyleRefreshManager.ForceRefreshAll();
		}

		[UnityEditor.MenuItem("CONTEXT/Toggle/Replace To/Xeric (Style)", true)]
		private static bool ValidateReplaceToStyleToggle(UnityEditor.MenuCommand command)
			=> command.context is Toggle and not XericStyleToggle;

		[UnityEditor.MenuItem("CONTEXT/Toggle/Replace To/Xeric (Style)", false, 11)]
		private static void ReplaceToStyleToggle(UnityEditor.MenuCommand command)
			=> command.ReplaceCommandContext<Toggle, XericStyleToggle,
				(Graphic graphic, ToggleGroup group, ToggleEvent onValueChanged, Navigation navigation)>(
				o => (o.graphic, o.group, o.onValueChanged, o.navigation),
				(t, d) => { t.graphic = d.graphic; t.group = d.group; t.onValueChanged = d.onValueChanged; t.navigation = d.navigation; });

		[UnityEditor.MenuItem("CONTEXT/Toggle/Replace To/Unity Toggle", true)]
		private static bool ValidateReplaceStyleToggleToUnity(UnityEditor.MenuCommand command)
			=> command.context is XericStyleToggle;

		[UnityEditor.MenuItem("CONTEXT/Toggle/Replace To/Unity Toggle", false, 100)]
		private static void ReplaceStyleToggleToUnity(UnityEditor.MenuCommand command)
			=> command.ReplaceCommandContext<XericStyleToggle, Toggle,
				(Graphic graphic, ToggleGroup group, ToggleEvent onValueChanged, Navigation navigation)>(
				o => (o.graphic, o.group, o.onValueChanged, o.navigation),
				(t, d) => { t.graphic = d.graphic; t.group = d.group; t.onValueChanged = d.onValueChanged; t.navigation = d.navigation; });
#endif

		#endregion
	}
}
