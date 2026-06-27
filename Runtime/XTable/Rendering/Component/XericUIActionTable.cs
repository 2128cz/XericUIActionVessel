using System;
using System.Collections.Generic;

using TMPro;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using XericUI.Core.Base;
using XericUI.XTable.Core;
using XericUI.XTable.Mapping;
using XericUI.XTable.Rendering.Elements;
using XericLibrary.Runtime.SuperStyleSheet;

namespace XericUI.XTable.Rendering.Component
{
	/// <summary>
	/// 表格组件——纯数据渲染器。
	/// 一张全幅背景 + 按需拼装单元格文本/图片。
	/// 无行列标题、无样式差异分割线——所有内容由数据定义。
	/// </summary>
	[ExecuteAlways]
	[AddComponentMenu("Xeric UI Vessel/Table/Xeric UI Action Table", 60)]
	public class XericUIActionTable : XericUIBehaciour, IPointerClickHandler
	{
		#region 常量

		private const string DEFAULT_STYLE_NS = "xeric_table_default";

		#endregion

		#region 序列化字段

		[SerializeField] private XTableData m_TableData;

		[SerializeField] private float[] m_RowHeights = new float[] { 40f, 30f };

		[SerializeField] private float[] m_ColWidths = new float[] { 100f, 80f, 120f };

		[SerializeField] private string m_DefaultStyleNamespace = DEFAULT_STYLE_NS;

		[SerializeField] private ScrollRect m_ScrollRect;

		#endregion

		#region 私有字段

		[System.NonSerialized] private CellAssembler m_Assembler;
		[System.NonSerialized] private XTableDirtyFlag m_DirtyFlag;

		private int m_SelectedRow = -1;
		private int m_SelectedCol = -1;

		// 多选范围
		private int m_SelectStartRow = -1;
		private int m_SelectStartCol = -1;
		private int m_SelectEndRow = -1;
		private int m_SelectEndCol = -1;

		[System.NonSerialized] private GameObject m_BackgroundGo;

		private float m_TotalWidth;
		private float m_TotalHeight;

		// ScrollRect 可见范围
		private Rect m_VisibleRect;

		#endregion

		#region 事件

		public event Action<int, int> OnCellSelected;
		public event Action OnTableRefreshed;

		#endregion

		#region 公开属性

		public XTableData TableData
		{
			get
			{
				if (m_TableData == null)
					m_TableData = new XTableData();
				return m_TableData;
			}
			set { m_TableData = value; MarkDirty(TableDirtyType.DataChanged); }
		}

		public float[] RowHeights => m_RowHeights;
		public float[] ColWidths => m_ColWidths;

		public string DefaultStyleNamespace => m_DefaultStyleNamespace;
		public int SelectedRow => m_SelectedRow;
		public int SelectedCol => m_SelectedCol;

		public int SelectStartRow => m_SelectStartRow;
		public int SelectStartCol => m_SelectStartCol;
		public int SelectEndRow => m_SelectEndRow;
		public int SelectEndCol => m_SelectEndCol;
		public bool HasMultiSelection => m_SelectStartRow >= 0 && m_SelectEndRow >= 0;

		/// <summary>判断单元格是否在多选范围内</summary>
		public bool IsInSelectionRange(int row, int col)
		{
			if (!HasMultiSelection) return false;
			int minR = Mathf.Min(m_SelectStartRow, m_SelectEndRow);
			int maxR = Mathf.Max(m_SelectStartRow, m_SelectEndRow);
			int minC = Mathf.Min(m_SelectStartCol, m_SelectEndCol);
			int maxC = Mathf.Max(m_SelectStartCol, m_SelectEndCol);
			return row >= minR && row <= maxR && col >= minC && col <= maxC;
		}

		#endregion

		#region 生命周期

		protected override void Awake()
		{
			base.Awake();

			if (m_TableData == null)
				m_TableData = new XTableData();

			m_Assembler = new CellAssembler(rectTransform);
			RecalculateTotalSize();
			EnsureDefaultStyleNamespace();
			CreateDefaultSampleData();
			CreateBackground();

			if (m_ScrollRect != null)
				m_ScrollRect.onValueChanged.AddListener(OnScrollValueChanged);

			if (!Application.isPlaying)
				SubscribeEditorUpdate();

			MarkDirty(TableDirtyType.All);
		}

		protected override void OnEnable()
		{
			base.OnEnable();
			EnsureDefaultStyleNamespace();
			MarkDirty(TableDirtyType.All);
		}

		protected override void OnDisable()
		{
			base.OnDisable();
		}

		protected override void OnDestroy()
		{
			if (m_ScrollRect != null)
				m_ScrollRect.onValueChanged.RemoveListener(OnScrollValueChanged);
			UnsubscribeEditorUpdate();

			// 销毁所有动态生成的对象
			if (m_Assembler != null)
				m_Assembler.DestroyAll();
			if (m_BackgroundGo != null)
			{
				if (Application.isPlaying) Destroy(m_BackgroundGo);
				else DestroyImmediate(m_BackgroundGo);
				m_BackgroundGo = null;
			}

			base.OnDestroy();
		}

		private void Update()
		{
			if (m_DirtyFlag.IsDirty) RefreshView();
		}

		#endregion

		#region 默认示例数据

		private void CreateDefaultSampleData()
		{
			// 仅当数据完全为空时才创建示例数据（首次使用或数据损坏后恢复）
			if (m_TableData.BlockCount > 0) return;

			string[,] sample = new string[2, 3]
			{
				{ "Name",    "Age",  "City" },
				{ "Alice",   "25",   "NYC"  }
			};

			for (int r = 0; r < 2; r++)
				for (int c = 0; c < 3; c++)
				{
					var data = m_TableData.GetOrCreateCell(r, c);
					data.Text = sample[r, c];
				}
		}

		#endregion

		#region 编辑器模式支持

		[System.NonSerialized] private bool m_EditorUpdateSubscribed;

		private void SubscribeEditorUpdate()
		{
			if (m_EditorUpdateSubscribed) return;
#if UNITY_EDITOR
			UnityEditor.EditorApplication.update += EditorUpdate;
			m_EditorUpdateSubscribed = true;
#endif
		}

		private void UnsubscribeEditorUpdate()
		{
			if (!m_EditorUpdateSubscribed) return;
#if UNITY_EDITOR
			UnityEditor.EditorApplication.update -= EditorUpdate;
			m_EditorUpdateSubscribed = false;
#endif
		}

#if UNITY_EDITOR
		private void EditorUpdate()
		{
			if (this == null || !isActiveAndEnabled)
			{
				UnsubscribeEditorUpdate();
				return;
			}
			if (m_DirtyFlag.IsDirty)
				RefreshView();
		}
#endif

		#endregion

		#region 公开方法

		public void SetCellData(int row, int col, XTableCellData data)
		{
			TableData.SetCell(row, col, data);
			MarkDirty(TableDirtyType.DataChanged);
		}

		public void SetCellText(int row, int col, string text)
		{
			var data = TableData.GetOrCreateCell(row, col);
			data.Text = text;
			MarkDirty(TableDirtyType.DataChanged);
		}

		public XTableCellData GetCellData(int row, int col)
			=> TableData.GetOrCreateCell(row, col);

		public void MergeCells(int startRow, int startCol, int rowSpan, int colSpan)
		{
			TableData.MergeCells(startRow, startCol, rowSpan, colSpan);
			MarkDirty(TableDirtyType.DataChanged);
		}

		public void UnmergeCells(int startRow, int startCol)
		{
			TableData.UnmergeCells(startRow, startCol);
			MarkDirty(TableDirtyType.DataChanged);
		}

		public void SelectCell(int row, int col)
		{
			ClearMultiSelection();
			m_SelectedRow = row;
			m_SelectedCol = col;
			MarkDirty(TableDirtyType.SelectionChanged);
		}

		public void ClearSelection()
		{
			m_SelectedRow = -1;
			m_SelectedCol = -1;
			ClearMultiSelection();
			MarkDirty(TableDirtyType.SelectionChanged);
		}

		/// <summary>开始多选（以当前格为锚点）</summary>
		public void StartMultiSelect(int row, int col)
		{
			m_SelectStartRow = row;
			m_SelectStartCol = col;
			m_SelectEndRow = row;
			m_SelectEndCol = col;
			m_SelectedRow = row;
			m_SelectedCol = col;
			MarkDirty(TableDirtyType.SelectionChanged);
		}

		/// <summary>扩展多选范围到目标格</summary>
		public void ExtendMultiSelect(int row, int col)
		{
			m_SelectEndRow = row;
			m_SelectEndCol = col;
			m_SelectedRow = row;
			m_SelectedCol = col;
			MarkDirty(TableDirtyType.SelectionChanged);
		}

		/// <summary>清除多选范围</summary>
		public void ClearMultiSelection()
		{
			m_SelectStartRow = -1;
			m_SelectStartCol = -1;
			m_SelectEndRow = -1;
			m_SelectEndCol = -1;
		}

		/// <summary>通过文本选择一个单元格（如 "A0"）</summary>
		public bool SelectCellByText(string text)
		{
			if (!XTableCoordinateFormat.TryParseCell(text, out int row, out int col))
				return false;
			SelectCell(row, col);
			return true;
		}

		/// <summary>通过文本选择一个范围（如 "A0:B1", "A:A", "0:0"）</summary>
		public bool SelectRangeByText(string text)
		{
			if (!XTableCoordinateFormat.TryParseRange(text,
				out int sr, out int sc, out int er, out int ec))
				return false;

			ClearSelection();
			m_SelectStartRow = sr;
			m_SelectStartCol = sc;
			m_SelectEndRow = er;
			m_SelectEndCol = ec;
			m_SelectedRow = sr;
			m_SelectedCol = sc;
			MarkDirty(TableDirtyType.SelectionChanged);
			return true;
		}

		/// <summary>获取当前选区的文本描述（单格 "A0"，多格 "A0:B1"）</summary>
		public string GetSelectionText()
		{
			if (m_SelectedRow < 0 || m_SelectedCol < 0) return string.Empty;
			if (HasMultiSelection &&
				(m_SelectStartRow != m_SelectEndRow || m_SelectStartCol != m_SelectEndCol))
			{
				return XTableCoordinateFormat.RangeToText(
					m_SelectStartRow, m_SelectStartCol,
					m_SelectEndRow, m_SelectEndCol);
			}
			return XTableCoordinateFormat.CellToText(m_SelectedRow, m_SelectedCol);
		}

		public void RefreshView()
		{
			if (m_Assembler == null) return;
			if (m_RowHeights == null || m_ColWidths == null) return;
			if (m_RowHeights.Length == 0 || m_ColWidths.Length == 0) return;

#if UNITY_EDITOR
			if (!Application.isPlaying)
				Canvas.ForceUpdateCanvases();
#endif

			RecalculateTotalSize();
			UpdateVisibleRect();

			// 更新背景尺寸
			if (m_BackgroundGo != null)
			{
				var bgRt = m_BackgroundGo.GetComponent<RectTransform>();
				bgRt.sizeDelta = new Vector2(m_TotalWidth, m_TotalHeight);
			}

			// 回收池
			m_Assembler.ReturnAll();

			// 计算可见范围
			int sr, er, sc, ec;
			CalcVisibleRange(out sr, out er, out sc, out ec);

			if (er <= sr || ec <= sc) return;

			// 渲染每个可见单元格
			RenderCells(sr, er, sc, ec);

			m_DirtyFlag.Clear();
			OnTableRefreshed?.Invoke();
		}

		#endregion

		#region 背景

		private void CreateBackground()
		{
			if (m_BackgroundGo != null) return;

			m_BackgroundGo = new GameObject("_TableBackground",
				typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
			m_BackgroundGo.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;
			m_BackgroundGo.transform.SetParent(rectTransform, false);

			RectTransform bgRt = m_BackgroundGo.GetComponent<RectTransform>();
			bgRt.anchorMin = new Vector2(0, 1);
			bgRt.anchorMax = new Vector2(0, 1);
			bgRt.pivot = new Vector2(0, 1);
			bgRt.anchoredPosition = Vector2.zero;
			bgRt.sizeDelta = new Vector2(m_TotalWidth, m_TotalHeight);

			var bgImg = m_BackgroundGo.GetComponent<Image>();
			bgImg.color = new Color(0.18f, 0.18f, 0.18f, 1f);
			bgImg.raycastTarget = false;
		}

		/// <summary>完全重建表格——销毁所有动态生成的对象并重新初始化</summary>
		[ContextMenu("Xeric UI/完全重建表格")]
		public void FullRebuild()
		{
			m_Assembler?.DestroyAll();

			if (m_BackgroundGo != null)
			{
				if (Application.isPlaying) Destroy(m_BackgroundGo);
				else DestroyImmediate(m_BackgroundGo);
				m_BackgroundGo = null;
			}

			m_TableData = new XTableData();

			m_Assembler = new CellAssembler(rectTransform);
			RecalculateTotalSize();
			CreateDefaultSampleData();
			CreateBackground();
			MarkDirty(TableDirtyType.All);
			RefreshView();
		}

		#endregion

		#region 单元格渲染

		private void RenderCells(int sr, int er, int sc, int ec)
		{
			Color fgColor = GetStyleColor(m_DefaultStyleNamespace, "/cell/fg/color",
				new Color(0.05f, 0.05f, 0.05f));
			Color selectionColor = new Color(0.18f, 0.42f, 0.82f, 0.25f);
			Color multiSelectColor = new Color(0.18f, 0.42f, 0.82f, 0.12f);
			Color focusColor = new Color(0.22f, 0.55f, 0.95f, 0.35f);
			int fontSize = GetStyleInt(m_DefaultStyleNamespace, "/cell/font/size", 14);

			for (int r = sr; r < er && r < m_RowHeights.Length; r++)
			{
				for (int c = sc; c < ec && c < m_ColWidths.Length; c++)
				{
					float cellX = GetColLeftX(c);
					float cellY = -GetRowTopY(r);
					float cellW = m_ColWidths[c];
					float cellH = m_RowHeights[r];
					XTableCellData data = TableData.GetCell(r, c);

					// 多选范围高亮（覆盖被选中范围内的所有格）
					if (IsInSelectionRange(r, c))
					{
						var selImg = m_Assembler.GetImage("cell_multiselect");
						selImg.Rect.anchoredPosition = new Vector2(cellX, cellY);
						selImg.Rect.sizeDelta = new Vector2(cellW, cellH);
						selImg.Image.color = (r == m_SelectedRow && c == m_SelectedCol)
							? focusColor : multiSelectColor;
					}
					// 单选高亮
					else if (r == m_SelectedRow && c == m_SelectedCol)
					{
						var selImg = m_Assembler.GetImage("cell_select");
						selImg.Rect.anchoredPosition = new Vector2(cellX, cellY);
						selImg.Rect.sizeDelta = new Vector2(cellW, cellH);
						selImg.Image.color = selectionColor;
					}

					// 水平网格线
					var hLine = m_Assembler.GetImage("cell_hline");
					hLine.Rect.anchoredPosition = new Vector2(cellX, cellY - cellH);
					hLine.Rect.sizeDelta = new Vector2(cellW, 1f);
					hLine.Image.color = new Color(0.35f, 0.35f, 0.35f, 0.5f);

					// 垂直网格线
					var vLine = m_Assembler.GetImage("cell_vline");
					vLine.Rect.anchoredPosition = new Vector2(cellX + cellW - 1, cellY);
					vLine.Rect.sizeDelta = new Vector2(1f, cellH);
					vLine.Image.color = new Color(0.35f, 0.35f, 0.35f, 0.5f);

					if (data == null) continue;

					// 文本：与 cell 同位置同尺寸，TMP 内部 Midline 对齐实现居中
					if (!string.IsNullOrEmpty(data.Text))
					{
						var txt = m_Assembler.GetText("cell_text");
						txt.Rect.anchoredPosition = new Vector2(cellX + 4, cellY);
						txt.Rect.sizeDelta = new Vector2(cellW - 8, cellH);
						txt.Text.text = data.Text;
						txt.Text.color = fgColor;
						txt.Text.fontSize = fontSize;
						txt.Text.alignment = TextAlignmentOptions.Midline;
					}
				}
			}
		}

		#endregion

		#region 坐标计算

		private float GetRowTopY(int row)
		{
			float y = 0;
			for (int i = 0; i < row && i < m_RowHeights.Length; i++)
				y += m_RowHeights[i];
			return y;
		}

		private float GetColLeftX(int col)
		{
			float x = 0;
			for (int i = 0; i < col && i < m_ColWidths.Length; i++)
				x += m_ColWidths[i];
			return x;
		}

		private void RecalculateTotalSize()
		{
			m_TotalWidth = 0;
			if (m_ColWidths != null)
				foreach (float w in m_ColWidths) m_TotalWidth += w;

			m_TotalHeight = 0;
			if (m_RowHeights != null)
				foreach (float h in m_RowHeights) m_TotalHeight += h;

			if (m_ScrollRect != null && m_ScrollRect.content != null)
				m_ScrollRect.content.sizeDelta = new Vector2(m_TotalWidth, m_TotalHeight);
		}

		private void UpdateVisibleRect()
		{
			m_VisibleRect = new Rect(0, 0, m_TotalWidth, m_TotalHeight);

			if (m_ScrollRect != null && m_ScrollRect.viewport != null)
			{
				RectTransform vt = m_ScrollRect.viewport;
				RectTransform ct = m_ScrollRect.content;
				if (vt != null && ct != null)
				{
					float sx = Mathf.Max(0, -ct.anchoredPosition.x);
					float sy = Mathf.Max(0, ct.anchoredPosition.y);
					m_VisibleRect = new Rect(sx, sy, vt.rect.width, vt.rect.height);
				}
			}
		}

		private void CalcVisibleRange(out int sr, out int er, out int sc, out int ec)
		{
			sr = 0; er = 0; sc = 0; ec = 0;

			float accY = 0;
			for (int r = 0; r < m_RowHeights.Length; r++)
			{
				float rowBottom = accY + m_RowHeights[r];
				if (rowBottom > m_VisibleRect.y && accY < m_VisibleRect.yMax)
				{
					if (er == 0 && sr == 0) sr = r;
					er = r + 1;
				}
				accY = rowBottom;
				if (accY > m_VisibleRect.yMax) break;
			}

			float accX = 0;
			for (int c = 0; c < m_ColWidths.Length; c++)
			{
				float colRight = accX + m_ColWidths[c];
				if (colRight > m_VisibleRect.x && accX < m_VisibleRect.xMax)
				{
					if (ec == 0 && sc == 0) sc = c;
					ec = c + 1;
				}
				accX = colRight;
				if (accX > m_VisibleRect.xMax) break;
			}

			if (sr < 0) sr = 0;
			if (sc < 0) sc = 0;
		}

		#endregion

		#region 样式

		private Color GetStyleColor(string ns, string path, Color fallback)
		{
			StyleValue val = StyleManager.GetValue(ns, path);
			return val != null ? (Color)val : fallback;
		}

		private int GetStyleInt(string ns, string path, int fallback)
		{
			StyleValue val = StyleManager.GetValue(ns, path);
			return val != null ? (int)val : fallback;
		}

		private void EnsureDefaultStyleNamespace()
		{
			if (!StyleManager.HasStyle(m_DefaultStyleNamespace, "/cell/bg/color"))
			{
				StyleManager.AddDynamicStyle(m_DefaultStyleNamespace, "/cell/bg/color",
					new Color(0.18f, 0.18f, 0.18f));
				StyleManager.AddDynamicStyle(m_DefaultStyleNamespace, "/cell/fg/color",
					new Color(0.05f, 0.05f, 0.05f));
				StyleManager.AddDynamicStyle(m_DefaultStyleNamespace, "/cell/font/size", 14);
				StyleManager.ForceRefresh();
			}
		}

		#endregion

		#region 其他

		private void MarkDirty(TableDirtyType type) => m_DirtyFlag.Mark(type);

		private void OnScrollValueChanged(Vector2 _) => MarkDirty(TableDirtyType.ViewChanged);

		#endregion

		#region IPointerClickHandler

		public void OnPointerClick(PointerEventData eventData)
		{
			RectTransformUtility.ScreenPointToLocalPointInRectangle(
				rectTransform, eventData.position, eventData.pressEventCamera, out Vector2 localPoint);

			// rectTransform 锚点在左上角，localPoint 以中心为原点
			float tableX = localPoint.x - rectTransform.rect.xMin;
			float tableY = rectTransform.rect.yMax - localPoint.y;

			float scrollY = m_ScrollRect != null ? Mathf.Max(0, m_ScrollRect.content.anchoredPosition.y) : 0;
			float scrollX = m_ScrollRect != null ? Mathf.Max(0, -m_ScrollRect.content.anchoredPosition.x) : 0;
			tableX += scrollX;
			tableY += scrollY;

			int col = -1;
			float ax = 0;
			for (int c = 0; c < m_ColWidths.Length; c++)
			{
				if (tableX >= ax && tableX < ax + m_ColWidths[c]) { col = c; break; }
				ax += m_ColWidths[c];
			}

			int row = -1;
			float ay = 0;
			for (int r = 0; r < m_RowHeights.Length; r++)
			{
				if (tableY >= ay && tableY < ay + m_RowHeights[r]) { row = r; break; }
				ay += m_RowHeights[r];
			}

			if (row >= 0 && col >= 0)
			{
				SelectCell(row, col);
				OnCellSelected?.Invoke(row, col);
			}
		}

		#endregion
	}
}
