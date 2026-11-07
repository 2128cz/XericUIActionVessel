using System;
using System.Collections.Generic;
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
		private readonly VisualElement m_FaceOverlay;
		private readonly Label m_FaceOverlayLabel;
		private readonly VisualElement m_GridOverlay;

		// 分格层：线的条数只随行列数变，编号只随格子数变，所以按数量复用，不每帧重建
		private readonly List<VisualElement> m_GridVerticals = new List<VisualElement>();
		private readonly List<VisualElement> m_GridHorizontals = new List<VisualElement>();
		private readonly List<Label> m_GridLabels = new List<Label>();
		private int m_GridColumns = -1;
		private int m_GridRows = -1;

		/// <summary> 在图上点了一下（不是拖拽）。参数是 0~1、左上为原点的坐标 </summary>
		public event Action<Vector2> faceClicked;

		private Texture2D m_CheckerTexture;
		private int m_CheckerWidth;
		private int m_CheckerHeight;
		private bool m_CheckerVisible;
		/// <summary> 上一次挂上去的那张图；引用没变就只重画，不重设样式 </summary>
		private Texture m_Content;
		private int m_AppliedWidth = -1;
		private int m_AppliedHeight = -1;
		private Vector2Int m_PixelSize = Vector2Int.zero;
		private float m_Zoom = 1f;
		private bool m_AutoFit = true;
		private bool m_Dragging;
		private bool m_PointerMoved;
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
			m_FaceOverlay = root.Q<VisualElement>("face-overlay");
			m_FaceOverlayLabel = root.Q<Label>("face-overlay-label");
			if (m_FaceOverlay != null)
				m_FaceOverlay.pickingMode = PickingMode.Ignore;
			m_GridOverlay = root.Q<VisualElement>("grid-overlay");
			if (m_GridOverlay != null)
				m_GridOverlay.pickingMode = PickingMode.Ignore;

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
			// 实时预览每秒重画几十次，但画进的是同一张 RT：内容每帧都在变，背景引用没变。
			// 这时只要一次重画，不必再走一遍样式解析 + 重新适配尺寸 + 重写读数
			bool sameContent = ReferenceEquals(content, m_Content) && pixelSize == m_PixelSize;
			m_Content = content;
			m_PixelSize = pixelSize;
			if (sameContent)
			{
				m_Image.MarkDirtyRepaint();
				return;
			}

			m_Image.style.backgroundImage = ToBackground(content);
			if (m_AutoFit)
				FitToView();
			else
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
			{
				// 尺寸没变时 ApplySize 会跳过写样式，这里要强制走一遍，否则棋盘纹理不会被建出来
				m_AppliedWidth = m_AppliedHeight = -1;
				ApplySize();
			}
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

		/// <summary>
		/// 把选中面片的框贴到图上。范围是 0~1（左上为原点），用百分比定位，缩放/滚动都不用重算。
		/// </summary>
		public void SetFaceSelection(bool visible, Vector2 min, Vector2 max, string label)
		{
			if (m_FaceOverlay == null)
				return;
			if (!visible)
			{
				m_FaceOverlay.style.display = DisplayStyle.None;
				return;
			}
			m_FaceOverlay.style.display = DisplayStyle.Flex;
			m_FaceOverlay.style.left = Length.Percent(Mathf.Clamp01(min.x) * 100f);
			m_FaceOverlay.style.top = Length.Percent(Mathf.Clamp01(min.y) * 100f);
			m_FaceOverlay.style.width = Length.Percent(Mathf.Max(max.x - min.x, 0.002f) * 100f);
			m_FaceOverlay.style.height = Length.Percent(Mathf.Max(max.y - min.y, 0.002f) * 100f);
			if (m_FaceOverlayLabel != null)
			{
				// 分了格的时候每格都有自己的编号标签，选中框只留框，免得同一格里两行字叠着
				m_FaceOverlayLabel.style.display = string.IsNullOrEmpty(label) ? DisplayStyle.None : DisplayStyle.Flex;
				m_FaceOverlayLabel.text = label;
			}
		}

		/// <summary> 一格在预览图里的位置（0~1，左上为原点）和它该显示的 UV 编号 </summary>
		public struct FaceCellRect
		{
			public Vector2 min;
			public Vector2 max;
			public string label;
		}

		/// <summary>
		/// 画分格线和每格的 UV 编号，回答"这条线右边/下边那一片是 UV 几"。
		///
		/// <para>只有一格时不画：这时右侧面片区已经写着 UV，再叠一层只会挡画面。
		/// 位置全用百分比，所以缩放、拖动滚动条都不用重算。</para>
		/// </summary>
		public void SetFaceGrid(IList<FaceCellRect> cells, int columns, int rows)
		{
			if (m_GridOverlay == null)
				return;
			if (cells == null || cells.Count < 2)
			{
				m_GridOverlay.style.display = DisplayStyle.None;
				return;
			}

			m_GridOverlay.style.display = DisplayStyle.Flex;
			EnsureGridLines(columns, rows);

			float left = float.MaxValue, top = float.MaxValue;
			float right = float.MinValue, bottom = float.MinValue;
			for (int i = 0; i < cells.Count; i++)
			{
				left = Mathf.Min(left, cells[i].min.x);
				top = Mathf.Min(top, cells[i].min.y);
				right = Mathf.Max(right, cells[i].max.x);
				bottom = Mathf.Max(bottom, cells[i].max.y);
			}
			left = Mathf.Clamp01(left);
			top = Mathf.Clamp01(top);
			right = Mathf.Clamp01(right);
			bottom = Mathf.Clamp01(bottom);

			// 线按整个网格的范围等分。格是投影后的四边形，这里用的是它的视口包围盒，
			// 正交正视时两者完全重合；斜看时线会略微跑到格边外，但仍然对得上编号
			for (int i = 0; i < m_GridVerticals.Count; i++)
			{
				VisualElement line = m_GridVerticals[i];
				line.style.left = Length.Percent(Mathf.Lerp(left, right, (float)i / Mathf.Max(columns, 1)) * 100f);
				line.style.top = Length.Percent(top * 100f);
				line.style.width = 1f;
				line.style.height = Length.Percent((bottom - top) * 100f);
			}
			for (int j = 0; j < m_GridHorizontals.Count; j++)
			{
				VisualElement line = m_GridHorizontals[j];
				line.style.left = Length.Percent(left * 100f);
				line.style.top = Length.Percent(Mathf.Lerp(top, bottom, (float)j / Mathf.Max(rows, 1)) * 100f);
				line.style.width = Length.Percent((right - left) * 100f);
				line.style.height = 1f;
			}

			EnsureGridLabels(cells.Count);
			// 一格在屏幕上还不到一个编号宽/高时，编号只会互相压着，索性不画。
			// 布局还没算出来（宽度为 0）时不拦，等下一次重画再按真实尺寸决定
			float frameWidth = m_Frame != null ? m_Frame.resolvedStyle.width : 0f;
			float frameHeight = m_Frame != null ? m_Frame.resolvedStyle.height : 0f;
			bool canMeasure = frameWidth > 1f && frameHeight > 1f;
			for (int i = 0; i < m_GridLabels.Count; i++)
			{
				Label label = m_GridLabels[i];
				bool used = i < cells.Count;
				if (used && canMeasure)
				{
					float cellWidth = (cells[i].max.x - cells[i].min.x) * frameWidth;
					float cellHeight = (cells[i].max.y - cells[i].min.y) * frameHeight;
					used = cellWidth >= 36f && cellHeight >= 16f;
				}
				label.style.display = used ? DisplayStyle.Flex : DisplayStyle.None;
				if (!used)
					continue;
				label.text = cells[i].label;
				label.style.left = Length.Percent(Mathf.Clamp01(cells[i].min.x) * 100f);
				label.style.top = Length.Percent(Mathf.Clamp01(cells[i].min.y) * 100f);
			}
		}

		private void EnsureGridLines(int columns, int rows)
		{
			if (m_GridColumns == columns && m_GridRows == rows)
				return;
			m_GridColumns = columns;
			m_GridRows = rows;

			for (int i = 0; i < m_GridVerticals.Count; i++)
				m_GridVerticals[i].RemoveFromHierarchy();
			for (int i = 0; i < m_GridHorizontals.Count; i++)
				m_GridHorizontals[i].RemoveFromHierarchy();
			m_GridVerticals.Clear();
			m_GridHorizontals.Clear();

			// 列数 + 1 条竖线、行数 + 1 条横线：把外框也算进去，否则最外一圈没有边
			for (int i = 0; i <= columns; i++)
				m_GridVerticals.Add(CreateGridLine());
			for (int j = 0; j <= rows; j++)
				m_GridHorizontals.Add(CreateGridLine());

			// 新加的线在最后，会把已有的编号压住；改完行列数把编号重新提到最上层
			for (int i = 0; i < m_GridLabels.Count; i++)
				m_GridLabels[i].BringToFront();
		}

		private VisualElement CreateGridLine()
		{
			var line = new VisualElement { pickingMode = PickingMode.Ignore };
			line.AddToClassList("grid-line");
			m_GridOverlay.Add(line);
			return line;
		}

		private void EnsureGridLabels(int count)
		{
			while (m_GridLabels.Count < count)
			{
				var label = new Label { pickingMode = PickingMode.Ignore };
				label.AddToClassList("grid-cell-label");
				// 后加的在上面：编号要压在线之上，不能被 1px 的线切过
				m_GridOverlay.Add(label);
				m_GridLabels.Add(label);
			}
		}

		public void Clear()
		{
			m_PixelSize = Vector2Int.zero;
			// 清掉记录，下一次 SetResult 才会真的重设背景，而不是当"同一张图"直接重画
			m_Content = null;
			m_AppliedWidth = m_AppliedHeight = -1;
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
				m_AppliedWidth = m_AppliedHeight = -1;
				UpdateReadout();
				return;
			}
			int width = Mathf.Max(1, Mathf.RoundToInt(m_PixelSize.x * m_Zoom));
			int height = Mathf.Max(1, Mathf.RoundToInt(m_PixelSize.y * m_Zoom));
			// 尺寸没变就别再写一遍：写样式会让整个预览做一次布局，心跳时这是白花的
			if (width != m_AppliedWidth || height != m_AppliedHeight)
			{
				m_AppliedWidth = width;
				m_AppliedHeight = height;
				m_Frame.style.width = width;
				m_Frame.style.height = height;
				if (m_CheckerVisible)
					EnsureChecker(width, height);
			}
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
			string text = m_PixelSize.x + " × " + m_PixelSize.y + " px    " +
			              (m_Zoom * 100f).ToString("0.#") + "%";
			// 文字没变就不写：Label 换文字要重新排版，心跳时这个字符串通常是一样的
			if (m_Readout.text != text)
				m_Readout.text = text;
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
			m_PointerMoved = false;
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
			if (delta.sqrMagnitude > 16f)
				m_PointerMoved = true;
			m_Viewport.scrollOffset = m_ScrollStart - delta;
		}

		private void OnPointerUp(PointerUpEvent evt)
		{
			m_Dragging = false;
			m_Viewport.ReleasePointer(evt.pointerId);
			if (m_PointerMoved || m_PixelSize.x <= 0 || faceClicked == null)
				return;

			// 没拖动才算点选：拖动是平移视图，抬手时不该顺手换掉选中的面
			Rect bounds = m_Frame.worldBound;
			var position = new Vector2(evt.position.x, evt.position.y);
			if (bounds.width < 1f || bounds.height < 1f || !bounds.Contains(position))
				return;
			// 统一用"左上为原点"的 0~1 坐标，和 Renderer.TryPickFace 的约定一致
			faceClicked.Invoke(new Vector2(
				(position.x - bounds.x) / bounds.width,
				(position.y - bounds.y) / bounds.height));
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
