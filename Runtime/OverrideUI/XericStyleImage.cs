using UnityEngine;
using UnityEngine.UI;

using XericLibrary.Runtime.SuperStyleSheet;
using XericUI.Core.StyleSheetUI;
using XericUI.Helper;
using XericUI.Core;

namespace XericUI.OverrideUI
{
	/// <summary>
	/// 基于 SuperStyleSheet 的图像组件。
	/// 使用 StyleField 引用样式表中的颜色和纹理路径，样式变更时自动刷新。
	/// </summary>
	[AddComponentMenu(XericRegistrationPaths.UI + "/Style Image", 53)]
	public class XericStyleImage : Image
	{
		#region 序列化字段

		[SerializeField]
		[Tooltip("颜色样式路径")]
		private StyleField m_ColorStyle = new StyleField("image/ColorTint/NormalColor");

		[SerializeField]
		[Tooltip("纹理/精灵样式路径")]
		private StyleField m_TextureStyle = new StyleField("image/ColorTint/Sprite");

		[SerializeField]
		[Tooltip("材质样式路径")]
		private StyleField m_MaterialStyle = new StyleField("image/ColorTint/Material");

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

			RegisterIfValid(m_ColorStyle);
			RegisterIfValid(m_TextureStyle);
			RegisterIfValid(m_MaterialStyle);

			ApplyAllStyles();
		}

#if UNITY_EDITOR
		/// <summary>编辑器下属性变更时即刻刷新（路径从空变为有效等场景）</summary>
		protected override void OnValidate()
		{
			base.OnValidate();
			if (!m_AutoRefresh) return;

			RegisterIfValid(m_ColorStyle);
			RegisterIfValid(m_TextureStyle);
			RegisterIfValid(m_MaterialStyle);

			ApplyAllStyles();
		}
#endif

		protected override void OnDisable()
		{
			UnregisterIfValid(m_ColorStyle);
			UnregisterIfValid(m_TextureStyle);
			UnregisterIfValid(m_MaterialStyle);

			base.OnDisable();
		}

		private void RegisterIfValid(StyleField field)
		{
			if (field == null) return;
			// 路径为空说明未在 Inspector 中配置，跳过注册避免空路径查询错误
			if (string.IsNullOrEmpty(field.StylePath)) return;
			// 先退订再注册，防止 OnValidate 重复调用导致双重订阅
			field.Member.OnValueChanged -= OnStyleChanged;
			field.Register();
			field.Member.OnValueChanged += OnStyleChanged;
		}

		private void UnregisterIfValid(StyleField field)
		{
			if (field == null) return;
			if (string.IsNullOrEmpty(field.StylePath)) return;
			field.Member.OnValueChanged -= OnStyleChanged;
			field.Unregister();
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
				if (val != null)
				{
					// 通过 Unity Object 基类获取并类型判断，抑制 Color→Object 转换的日志错误
					Object obj = SafeGetObject<Object>(val);
					if (obj is Sprite sp) { m_ResolvedSprite = sp; sprite = sp; }
					else if (obj is Texture2D tex)
					{
						m_ResolvedSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
						sprite = m_ResolvedSprite;
					}
				}
			}

			if (m_MaterialStyle != null)
			{
				StyleValue val = m_MaterialStyle.GetValue();
				if (val != null)
				{
					Object obj = SafeGetObject<Object>(val);
					if (obj is Material mat) material = mat;
				}
			}
		}

		private void OnStyleChanged(StyleValue val)
		{
			if (gameObject.activeInHierarchy)
				ApplyAllStyles();
		}

		#endregion

		#region 辅助

		/// <summary>
		/// 安全获取 UnityEngine.Object 类型值——临时关闭日志以抑制 StyleValue 内部的类型不匹配错误。
		/// </summary>
		private static T SafeGetObject<T>(StyleValue val) where T : Object
		{
			if (val == null) return null;
			var logger = Debug.unityLogger;
			bool prev = logger.logEnabled;
			logger.logEnabled = false;
			try { return val.GetValueAs<T>(); }
			finally { logger.logEnabled = prev; }
		}

		#endregion

		#region 组件替换

#if UNITY_EDITOR
		[UnityEditor.MenuItem(XericRegistrationPaths.ContextPrefix + "Image/Replace To/Xeric (Style)", true)]
		private static bool ValidateReplaceToStyleImage(UnityEditor.MenuCommand command)
			=> command.context is Image and not XericStyleImage;

		[UnityEditor.MenuItem(XericRegistrationPaths.ContextPrefix + "Image/Replace To/Xeric (Style)", false, 11)]
		private static void ReplaceToStyleImage(UnityEditor.MenuCommand command)
			=> command.ReplaceCommandContext<Image, XericStyleImage, Sprite>(
				o => o.sprite,
				(t, d) => { t.sprite = d; });

		[UnityEditor.MenuItem(XericRegistrationPaths.ContextPrefix + "Image/Replace To/Unity Image (Style)", true)]
		private static bool ValidateReplaceStyleImageToUnity(UnityEditor.MenuCommand command)
			=> command.context is XericStyleImage;

		[UnityEditor.MenuItem(XericRegistrationPaths.ContextPrefix + "Image/Replace To/Unity Image (Style)", false, 100)]
		private static void ReplaceStyleImageToUnity(UnityEditor.MenuCommand command)
			=> command.ReplaceCommandContext<XericStyleImage, Image, Sprite>(
				o => o.sprite,
				(t, d) => { t.sprite = d; });
#endif

		#endregion
	}
}
