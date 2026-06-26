using UnityEngine;
using UnityEngine.UI;

using XericLibrary.Runtime.SuperStyleSheet;
using XericUI.Core.StyleSheetUI;
using XericUI.Helper;

namespace XericUI.OverrideUI
{
	/// <summary>
	/// 基于 SuperStyleSheet 的图像组件。
	/// 使用 StyleField 引用样式表中的颜色和纹理路径，样式变更时自动刷新。
	/// </summary>
	[AddComponentMenu("Xeric UI Vessel/UI/Style Image", 53)]
	public class XericStyleImage : Image
	{
		#region 序列化字段

		[Header("图像样式")]
		[SerializeField]
		[Tooltip("颜色样式路径")]
		private StyleField m_ColorStyle = new StyleField();

		[SerializeField]
		[Tooltip("纹理/精灵样式路径")]
		private StyleField m_TextureStyle = new StyleField();

		[SerializeField]
		[Tooltip("材质样式路径")]
		private StyleField m_MaterialStyle = new StyleField();

		[SerializeField]
		[Tooltip("启用样式表自动刷新")]
		private bool m_AutoRefresh = true;

		#endregion

		#region 运行时

		[System.NonSerialized] private Sprite m_ResolvedSprite;

		#endregion

		#region 生命周期

		protected override void OnEnable()
		{
			base.OnEnable();

			if (!m_AutoRefresh) return;

			m_ColorStyle?.Register();
			m_TextureStyle?.Register();
			m_MaterialStyle?.Register();
			m_ColorStyle.OnValueChanged += OnStyleChanged;
			m_TextureStyle.OnValueChanged += OnStyleChanged;
			m_MaterialStyle.OnValueChanged += OnStyleChanged;

			ApplyAllStyles();
		}

		protected override void OnDisable()
		{
			if (m_ColorStyle != null) { m_ColorStyle.OnValueChanged -= OnStyleChanged; m_ColorStyle.Unregister(); }
			if (m_TextureStyle != null) { m_TextureStyle.OnValueChanged -= OnStyleChanged; m_TextureStyle.Unregister(); }
			if (m_MaterialStyle != null) { m_MaterialStyle.OnValueChanged -= OnStyleChanged; m_MaterialStyle.Unregister(); }

			base.OnDisable();
		}

		#endregion

		#region 样式应用

		public void ApplyAllStyles()
		{
			if (m_ColorStyle != null)
			{
				StyleValue val = m_ColorStyle.GetValue();
				if (val != null) color = val;
			}

			if (m_TextureStyle != null)
			{
				StyleValue val = m_TextureStyle.GetValue();
				Sprite sp = val?.GetValueAs<Sprite>();
				if (sp != null) { m_ResolvedSprite = sp; sprite = sp; }
				else
				{
					Texture2D tex = val?.GetValueAs<Texture2D>();
					if (tex != null)
					{
						m_ResolvedSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
						sprite = m_ResolvedSprite;
					}
				}
			}

			if (m_MaterialStyle != null)
			{
				StyleValue val = m_MaterialStyle.GetValue();
				Material mat = val?.GetValueAs<Material>();
				if (mat != null) material = mat;
			}
		}

		private void OnStyleChanged(StyleValue val)
		{
			if (gameObject.activeInHierarchy)
				ApplyAllStyles();
		}

		#endregion

		#region 组件替换

#if UNITY_EDITOR
		[UnityEditor.MenuItem("CONTEXT/Image/Replace To/Xeric (Style)", true)]
		private static bool ValidateReplaceToStyleImage(UnityEditor.MenuCommand command)
			=> command.context is Image and not XericStyleImage;

		[UnityEditor.MenuItem("CONTEXT/Image/Replace To/Xeric (Style)", false, 11)]
		private static void ReplaceToStyleImage(UnityEditor.MenuCommand command)
			=> command.ReplaceCommandContext<Image, XericStyleImage, Sprite>(
				o => o.sprite,
				(t, d) => { t.sprite = d; });

		[UnityEditor.MenuItem("CONTEXT/Image/Replace To/Unity Image", true)]
		private static bool ValidateReplaceStyleImageToUnity(UnityEditor.MenuCommand command)
			=> command.context is XericStyleImage;

		[UnityEditor.MenuItem("CONTEXT/Image/Replace To/Unity Image", false, 100)]
		private static void ReplaceStyleImageToUnity(UnityEditor.MenuCommand command)
			=> command.ReplaceCommandContext<XericStyleImage, Image, Sprite>(
				o => o.sprite,
				(t, d) => { t.sprite = d; });
#endif

		#endregion
	}
}
