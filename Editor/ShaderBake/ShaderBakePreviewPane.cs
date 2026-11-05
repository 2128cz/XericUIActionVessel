using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace XericUI.ShaderBake.Editor
{
	/// <summary>
	/// 左侧浏览图：把渲染结果按缩放摆进滚动区，负责缩放、拖拽平移与透明底棋盘。
	///
	/// <para><b>为什么要把 stage 的最小尺寸钉成视口尺寸</b>：ScrollView 在 mode=Both 下不会把内容撑到
	/// 视口大小，于是图小于视口时"居中"这条样式不成立，图会缩在左上角。这里显式把 min 尺寸设成视口尺寸，
	/// 让居中和"图大了才出滚动条"两件事同时成立。</para>
	/// </summary>
	internal sealed class ShaderBakePreviewPane
	{
		private readonly ScrollView m_Viewport;
		private readonly VisualElement m_Stage;
		private readonly VisualElement m_Frame;
		private readonly Image m_Checker;
		private readonly Image m_Image;
		private readonly Label m_Readout;

		private Texture2D m_CheckerTexture;
		private int m_CheckerWidth;
		private int m_CheckerHeight;
		private bool m_CheckerVisible;
		private Vector2Int m_PixelSize = Vector2Int.zero;
		private float m_Zoom = 1f;
		private bool m_AutoFit = true;
		private bool m_Dragging;
		private Vector2 m_DragStart;
		private Vector2 m_ScrollStart;

		/// <summary> 当前缩放倍率（1 = 一个像素对应屏幕一个点） </summary>
		public float zoom { get { return m_Zoom; } }

		/// <summary> 用户手动改过缩放后就不再自动适配，免得每次渲染都把他的视图抢回去 </summary>
		public bool autoFit { get { return m_AutoFit; } set { m_AutoFit = value; } }

		public ShaderBakePreviewPane(VisualElement root)
		{
			m_Viewport = root.Q<ScrollView>("preview-viewport");
			m_Stage = root.Q<VisualElement>("preview-stage");
			m_Frame = root.Q<VisualElement>("preview-frame");
			m_Checker = root.Q<Image>("preview-checker");
			m_Image = root.Q<Image>("preview-image");
			m_Readout = root.Q<Label>("preview-readout");

			if (m_Viewport == null || m_Frame == null || m_Image == null)
				throw new InvalidOperationException("BakeShaderWindow.uxml 缺少预览区节点");

			m_Viewport.RegisterCallback<GeometryChangedEvent>(OnViewportGeometry);
			m_Viewport.RegisterCallback<WheelEvent>(OnWheel);
			m_Viewport.RegisterCallback<PointerDownEvent>(OnPointerDown);
			m_Viewport.RegisterCallback<PointerMoveEvent>(OnPointerMove);
			m_Viewport.RegisterCallback<PointerUpEvent>(OnPointerUp);
		}

		/// <summary> 挂上这次渲染的结果。传 null 表示"没有结果"，只清读数。 </summary>
		public void SetResult(Texture content, Vector2Int pixelSize)
		{
			m_PixelSize = pixelSize;
			m_Image.style.backgroundImage = ToBackground(content);
			if (m_AutoFit)
				FitToView();
			ApplySize();
		}

		/// <summary>
		/// RenderTexture 与 Texture2D 走的是 Background 的两个不同入口——UI Toolkit 里 RT 不是
		/// "一张普通纹理"，它要自己那条采样路径；传空则回到"没有背景"。
		/// </summary>
		private static StyleBackground ToBackground(Texture content)
		{
			if (content == null)
				return new StyleBackground(StyleKeyword.None);
			var rt = content as RenderTexture;
			if (rt != null)
				return new StyleBackground(Background.FromRenderTexture(rt));
			return new StyleBackground(Background.FromTexture2D(content as Texture2D));
		}

		/// <summary> 透明底棋盘：只在需要看 alpha 时生成那张纹理 </summary>
		public void SetCheckerVisible(bool visible)
		{
			m_CheckerVisible = visible;
			if (m_Checker == null)
				return;
			m_Checker.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
			if (visible)
				ApplySize();
		}

		public void ZoomIn() { SetZoom(m_Zoom * 1.25f); }
		public void ZoomOut() { SetZoom(m_Zoom / 1.25f); }
		public void ActualSize() { m_AutoFit = false; SetZoom(1f); }
		public void FitToView()
		{
			m_AutoFit = true;
			Rect rect = m_Viewport.contentRect;
			if (m_PixelSize.x <= 0 || m_PixelSize.y <= 0 || rect.width < 8f || rect.height < 8f)
				return;
			// 留 24px 边距，否则图正好贴边看上去像被裁了一半
			float fit = Mathf.Min((rect.width - 24f) / m_PixelSize.x, (rect.height - 24f) / m_PixelSize.y);
			SetZoom(fit);
		}

		public void Clear()
		{
			m_PixelSize = Vector2Int.zero;
			m_Image.style.backgroundImage = new StyleBackground(StyleKeyword.None);
			UpdateReadout();
		}

		public void Dispose()
		{
			if (m_CheckerTexture != null)
			{
				UnityEngine.Object.DestroyImmediate(m_CheckerTexture);
				m_CheckerTexture = null;
			}
		}

		private void SetZoom(float value)
		{
			m_Zoom = Mathf.Clamp(value, 0.02f, 32f);
			ApplySize();
		}

		private void ApplySize()
		{
			if (m_PixelSize.x <= 0 || m_PixelSize.y <= 0)
			{
				m_Frame.style.width = 0f;
				m_Frame.style.height = 0f;
				UpdateReadout();
				return;
			}
			int width = Mathf.Max(1, Mathf.RoundToInt(m_PixelSize.x * m_Zoom));
			int height = Mathf.Max(1, Mathf.RoundToInt(m_PixelSize.y * m_Zoom));
			m_Frame.style.width = width;
			m_Frame.style.height = height;
			if (m_CheckerVisible)
				EnsureChecker(width, height);
			UpdateReadout();
		}

		private void UpdateReadout()
		{
			if (m_Readout == null)
				return;
			if (m_PixelSize.x <= 0)
			{
				m_Readout.text = "无结果";
				return;
			}
			m_Readout.text = m_PixelSize.x + " × " + m_PixelSize.y + " px    " +
			                 (m_Zoom * 100f).ToString("0.#") + "%";
		}

		private void OnViewportGeometry(GeometryChangedEvent evt)
		{
			// 视口变高变宽都要重钉，否则居中的参照物是上一次的大小
			if (m_Stage != null)
			{
				m_Stage.style.minWidth = Mathf.Max(evt.newRect.width, 1f);
				m_Stage.style.minHeight = Mathf.Max(evt.newRect.height, 1f);
			}
			if (m_AutoFit)
				FitToView();
			else
				ApplySize();
		}

		private void OnWheel(WheelEvent evt)
		{
			if (m_PixelSize.x <= 0)
				return;
			// 滚轮在这块画布上的语义是缩放；平移交给拖拽，滚动条留给键盘和拖拽后的自动定位
			m_AutoFit = false;
			SetZoom(m_Zoom * (evt.delta.y > 0f ? 1.1f : 1f / 1.1f));
			evt.StopPropagation();
		}

		private void OnPointerDown(PointerDownEvent evt)
		{
			if (m_PixelSize.x <= 0)
				return;
			m_Dragging = true;
			m_DragStart = evt.position;
			m_ScrollStart = m_Viewport.scrollOffset;
			m_Viewport.CapturePointer(evt.pointerId);
		}

		private void OnPointerMove(PointerMoveEvent evt)
		{
			// 指针已经被 capture，move 一定送到这里，不需要再判断是否还在视口内
			if (!m_Dragging)
				return;
			// 2022.1 的 position 是 Vector3，直接和 Vector2 相减会二义
			Vector2 delta = (Vector2)evt.position - m_DragStart;
			m_Viewport.scrollOffset = m_ScrollStart - delta;
		}

		private void OnPointerUp(PointerUpEvent evt)
		{
			m_Dragging = false;
			m_Viewport.ReleasePointer(evt.pointerId);
		}

		/// <summary>
		/// 生成 8px 格子的棋盘纹理。
		///
		/// <para>2022.1 的 <c>ScaleMode</c> 还没有 <c>Tile</c>，靠铺贴做不到，所以干脆按面片当前的
		/// 显示尺寸生成一张等大纹理，元素直接拉伸也正好一格对一格。尺寸量化到 16 的整数倍：
		/// 连续缩放时大多数帧能命中缓存，且格子边界始终对齐。</para>
		/// </summary>
		private void EnsureChecker(int width, int height)
		{
			const int cell = 8;
			const int quantum = cell * 2;
			const int maxSide = 1024;

			int w = Mathf.Clamp(Mathf.Max(width, quantum) / quantum * quantum, quantum, maxSide);
			int h = Mathf.Clamp(Mathf.Max(height, quantum) / quantum * quantum, quantum, maxSide);
			if (m_CheckerTexture != null && m_CheckerWidth == w && m_CheckerHeight == h)
				return;

			UnityEngine.Object previous = m_CheckerTexture;
			var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
			{
				name = "ShaderBakeChecker",
				hideFlags = HideFlags.DontSave,
				filterMode = FilterMode.Point,
				wrapMode = TextureWrapMode.Clamp
			};

			var light = new Color32(107, 107, 107, 255);
			var dark = new Color32(84, 84, 84, 255);
			var pixels = new Color32[w * h];
			for (int y = 0; y < h; y++)
			{
				int rowParity = (y / cell) & 1;
				int offset = y * w;
				for (int x = 0; x < w; x++)
					pixels[offset + x] = (((x / cell) & 1) == rowParity) ? light : dark;
			}
			tex.SetPixels32(pixels);
			tex.Apply();

			m_CheckerTexture = tex;
			m_CheckerWidth = w;
			m_CheckerHeight = h;
			m_Checker.style.backgroundImage = new StyleBackground(tex);

			if (previous != null)
				UnityEngine.Object.DestroyImmediate(previous);
		}
	}
}
