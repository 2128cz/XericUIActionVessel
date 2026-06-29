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
#if UNITY_EDITOR
using UnityEditor;
#endif

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

        [SerializeField]
        [Tooltip("在 Hierarchy 中显示动态生成的子对象（仅编辑器，不保存到场景）")]
        private bool m_ChildrenVisible;

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
            set
            {
                m_TableData = value;
                MarkDirty(TableDirtyType.DataChanged);
            }
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

        /// <summary>动态子对象在 Hierarchy 中是否可见（仅编辑器有效）</summary>
        public bool ChildrenVisible
        {
            get => m_ChildrenVisible;
            set => SetChildrenVisible(value);
        }

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

            // 域重载后旧子对象可能残留在场景中（DontSave 不自动销毁），先清空再重建
            ClearAllChildren();

            if (m_TableData == null)
                m_TableData = new XTableData();

            m_Assembler = new CellAssembler(rectTransform, m_DefaultStyleNamespace);
            RecalculateTotalSize();
            EnsureDefaultStyleNamespace();
            CreateDefaultSampleData();
            CreateBackground();

            if (m_ScrollRect != null)
                m_ScrollRect.onValueChanged.AddListener(OnScrollValueChanged);

            if (!Application.isPlaying)
                SubscribeEditorUpdate();
            else
                m_Assembler.OnStyleChangeDetected += OnPoolStyleChanged;

            MarkDirty(TableDirtyType.All);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            EnsureDefaultStyleNamespace();
            MarkDirty(TableDirtyType.All);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            // 编辑器下属性变更 / 选中组件时即刻刷新样式
            MarkDirty(TableDirtyType.All);
        }
#endif

        protected override void OnDisable()
        {
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            if (m_ScrollRect != null)
                m_ScrollRect.onValueChanged.RemoveListener(OnScrollValueChanged);
            UnsubscribeEditorUpdate();

            if (m_Assembler != null)
                m_Assembler.OnStyleChangeDetected -= OnPoolStyleChanged;

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
                { "Name", "Age", "City" },
                { "Alice", "25", "NYC" }
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

        /// <summary>设置单元格的样式命名空间</summary>
        public void SetCellStyleNamespace(int row, int col, string styleNamespace)
        {
            var data = TableData.GetOrCreateCell(row, col);
            data.StyleNamespace = styleNamespace;
            MarkDirty(TableDirtyType.StyleChanged);
        }

        /// <summary>获取单元格的样式命名空间</summary>
        public string GetCellStyleNamespace(int row, int col)
        {
            return TableData.GetCell(row, col)?.StyleNamespace ?? m_DefaultStyleNamespace;
        }

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

        /// <summary>判断指定单元格是否被合并覆盖（非源，会被重定向）</summary>
        public bool IsCellMerged(int row, int col) => TableData.IsCellMerged(row, col);

        /// <summary>判断指定单元格是否为合并源（锚点），并返回合并跨度</summary>
        public bool IsMergeSource(int row, int col, out int rowSpan, out int colSpan)
            => TableData.IsMergeSource(row, col, out rowSpan, out colSpan);

        /// <summary>在末尾追加一行，默认行高 40</summary>
        public void AddRow(float height = 40f)
        {
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(this, "Add Table Row");
#endif
            int newLen = m_RowHeights.Length + 1;
            var newArr = new float[newLen];
            System.Array.Copy(m_RowHeights, newArr, m_RowHeights.Length);
            newArr[newLen - 1] = height;
            m_RowHeights = newArr;
            MarkDirty(TableDirtyType.LayoutChanged);
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>在末尾追加一列，默认列宽 100</summary>
        public void AddColumn(float width = 100f)
        {
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(this, "Add Table Column");
#endif
            int newLen = m_ColWidths.Length + 1;
            var newArr = new float[newLen];
            System.Array.Copy(m_ColWidths, newArr, m_ColWidths.Length);
            newArr[newLen - 1] = width;
            m_ColWidths = newArr;
            MarkDirty(TableDirtyType.LayoutChanged);
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>设置指定行的高度</summary>
        public void SetRowHeight(int row, float height)
        {
            if (row < 0 || row >= m_RowHeights.Length) return;
            if (height <= 0) return;
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(this, "Set Row Height");
#endif
            m_RowHeights[row] = height;
            MarkDirty(TableDirtyType.LayoutChanged);
#if UNITY_EDITOR
            if (!Application.isPlaying) UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>设置指定列的宽度</summary>
        public void SetColWidth(int col, float width)
        {
            if (col < 0 || col >= m_ColWidths.Length) return;
            if (width <= 0) return;
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(this, "Set Column Width");
#endif
            m_ColWidths[col] = width;
            MarkDirty(TableDirtyType.LayoutChanged);
#if UNITY_EDITOR
            if (!Application.isPlaying) UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>删除指定行，单元格数据上移</summary>
        public void RemoveRow(int row)
        {
            if (row < 0 || row >= m_RowHeights.Length) return;
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(this, "Remove Table Row");
#endif
            int colCount = m_ColWidths.Length;

            // 先解除涉及该行的合并
            for (int c = 0; c < colCount; c++)
                TableData.UnmergeCells(row, c);

            // 将下方行数据上移
            for (int r = row + 1; r < m_RowHeights.Length; r++)
            for (int c = 0; c < colCount; c++)
            {
                var data = TableData.GetCell(r, c);
                if (data != null)
                    TableData.SetCell(r - 1, c, data);
                else
                    TableData.SetCell(r - 1, c, null); // 显式清空原位置
            }

            // 清空最后一行数据
            int lastRow = m_RowHeights.Length - 1;
            for (int c = 0; c < colCount; c++)
                TableData.SetCell(lastRow, c, null);

            // 缩小行高数组
            int newLen = m_RowHeights.Length - 1;
            var newArr = new float[newLen];
            for (int i = 0; i < row; i++)
                newArr[i] = m_RowHeights[i];
            for (int i = row; i < newLen; i++)
                newArr[i] = m_RowHeights[i + 1];
            m_RowHeights = newArr;

            // 更新选中状态
            if (m_SelectedRow >= m_RowHeights.Length)
                m_SelectedRow = Mathf.Max(-1, m_RowHeights.Length - 1);
            if (m_SelectStartRow >= m_RowHeights.Length)
                m_SelectStartRow = Mathf.Max(-1, m_RowHeights.Length - 1);
            if (m_SelectEndRow >= m_RowHeights.Length)
                m_SelectEndRow = Mathf.Max(-1, m_RowHeights.Length - 1);

            MarkDirty(TableDirtyType.LayoutChanged | TableDirtyType.DataChanged | TableDirtyType.SelectionChanged);
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>删除指定列，单元格数据左移</summary>
        public void RemoveColumn(int col)
        {
            if (col < 0 || col >= m_ColWidths.Length) return;
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(this, "Remove Table Column");
#endif
            int rowCount = m_RowHeights.Length;

            // 先解除涉及该列的合并
            for (int r = 0; r < rowCount; r++)
                TableData.UnmergeCells(r, col);

            // 将右侧列数据左移
            for (int r = 0; r < rowCount; r++)
            for (int c = col + 1; c < m_ColWidths.Length; c++)
            {
                var data = TableData.GetCell(r, c);
                if (data != null)
                    TableData.SetCell(r, c - 1, data);
                else
                    TableData.SetCell(r, c - 1, null);
            }

            // 清空最后一列数据
            int lastCol = m_ColWidths.Length - 1;
            for (int r = 0; r < rowCount; r++)
                TableData.SetCell(r, lastCol, null);

            // 缩小列宽数组
            int newLen = m_ColWidths.Length - 1;
            var newArr = new float[newLen];
            for (int i = 0; i < col; i++)
                newArr[i] = m_ColWidths[i];
            for (int i = col; i < newLen; i++)
                newArr[i] = m_ColWidths[i + 1];
            m_ColWidths = newArr;

            // 更新选中状态
            if (m_SelectedCol >= m_ColWidths.Length)
                m_SelectedCol = Mathf.Max(-1, m_ColWidths.Length - 1);
            if (m_SelectStartCol >= m_ColWidths.Length)
                m_SelectStartCol = Mathf.Max(-1, m_ColWidths.Length - 1);
            if (m_SelectEndCol >= m_ColWidths.Length)
                m_SelectEndCol = Mathf.Max(-1, m_ColWidths.Length - 1);

            MarkDirty(TableDirtyType.LayoutChanged | TableDirtyType.DataChanged | TableDirtyType.SelectionChanged);
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
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
            // 编辑模式下 Awake 可能尚未执行，按需回退初始化
            if (m_Assembler == null)
            {
                if (rectTransform != null)
                    m_Assembler = new CellAssembler(rectTransform, m_DefaultStyleNamespace);
                else return;
            }

            if (m_RowHeights == null || m_ColWidths == null) return;
            if (m_RowHeights.Length == 0 || m_ColWidths.Length == 0) return;

            CreateBackground();

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

            // 额外清理孤立的直接子对象（防止域重载或异常流程产生的残留）
            Transform poolRoot = m_Assembler.PoolRoot;
            for (int i = rectTransform.childCount - 1; i >= 0; i--)
            {
                var child = rectTransform.GetChild(i);
                if ((poolRoot != null && child == poolRoot)) continue;
                if (m_BackgroundGo != null && child.gameObject == m_BackgroundGo) continue;

                child.gameObject.hideFlags = HideFlags.None;
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }

            // 计算可见范围
            int sr, er, sc, ec;
            CalcVisibleRange(out sr, out er, out sc, out ec);

            if (er <= sr || ec <= sc) return;

            // 渲染每个可见单元格
            RenderCells(sr, er, sc, ec);

            m_DirtyFlag.Clear();
            OnTableRefreshed?.Invoke();
            SetChildrenVisible(m_ChildrenVisible);
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
            bgImg.color = GetStyleColor(m_DefaultStyleNamespace, "/cell/bg/color", new Color(0.18f, 0.18f, 0.18f, 1f));
            bgImg.raycastTarget = false;
        }

        /// <summary>销毁 transform 下所有子对象（用于域重载后清理残留）</summary>
        private void ClearAllChildren()
        {
            if (rectTransform == null) return;

#if UNITY_EDITOR
            // 编辑器模式下先重置 HideFlags 再销毁，防止 DontSave 对象拒绝销毁
            while (rectTransform.childCount > 0)
            {
                var child = rectTransform.GetChild(0);
                child.gameObject.hideFlags = HideFlags.None;
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }
#else
			for (int i = rectTransform.childCount - 1; i >= 0; i--)
			{
				var child = rectTransform.GetChild(i);
				Destroy(child.gameObject);
			}
#endif
        }

        /// <summary>设置所有动态子对象的可见性（仅编辑器）</summary>
        public void SetChildrenVisible(bool visible)
        {
            m_ChildrenVisible = visible;
#if UNITY_EDITOR
            if (rectTransform == null) return;
            for (int i = rectTransform.childCount - 1; i >= 0; i--)
            {
                var child = rectTransform.GetChild(i);
                child.gameObject.hideFlags = visible
                    ? HideFlags.DontSave
                    : HideFlags.DontSave | HideFlags.HideInHierarchy;
            }
#endif
        }

        /// <summary>完全重建表格——销毁所有动态生成的对象并重新初始化</summary>
        [ContextMenu("Xeric UI/完全重建表格")]
        public void FullRebuild()
        {
            m_Assembler?.DestroyAll();
            ClearAllChildren();
            m_BackgroundGo = null;

            m_TableData = new XTableData();

            m_Assembler = new CellAssembler(rectTransform, m_DefaultStyleNamespace);
            RecalculateTotalSize();
            CreateDefaultSampleData();
            CreateBackground();
            MarkDirty(TableDirtyType.All);
            RefreshView();
            SetChildrenVisible(m_ChildrenVisible);
        }

        /// <summary>重新生成表格但保留已有文本数据——先保存，重建后再回填</summary>
        public void FullRebuildPreservingData()
        {
            // 保存当前单元格文本
            var savedTexts = new Dictionary<(int, int), string>();
            if (m_TableData != null)
            {
                int rows = m_RowHeights?.Length ?? 0;
                int cols = m_ColWidths?.Length ?? 0;
                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < cols; c++)
                    {
                        var cell = m_TableData.GetCell(r, c);
                        if (cell != null && !string.IsNullOrEmpty(cell.Text))
                            savedTexts[(r, c)] = cell.Text;
                    }
                }
            }

            FullRebuild();

            // 恢复文本数据
            foreach (var kv in savedTexts)
            {
                int r = kv.Key.Item1, c = kv.Key.Item2;
                if (r < (m_RowHeights?.Length ?? 0) && c < (m_ColWidths?.Length ?? 0))
                    m_TableData.GetOrCreateCell(r, c).Text = kv.Value;
            }

            RefreshView();
            SetChildrenVisible(m_ChildrenVisible);
        }

        /// <summary>仅强制刷新样式，不重建数据结构</summary>
        public void ForceStyleRefresh()
        {
            StyleManager.ForceRefresh();
            MarkDirty(TableDirtyType.StyleChanged);
            RefreshView();
        }

        #endregion

        #region 单元格渲染

        private void RenderCells(int sr, int er, int sc, int ec)
        {
            Color fgFallback = new Color(0.05f, 0.05f, 0.05f);
            string ns = m_DefaultStyleNamespace;

            // 编辑器模式：从样式表 pull 所有颜色值
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Color selectionColor = GetStyleColor(ns, "/cell/selection/bg/color", new Color(0.2f, 0.5f, 1f, 0.3f));
                Color borderColor = GetStyleColor(ns, "/cell/border/bottom/color", new Color(0.35f, 0.35f, 0.35f, 0.5f));
                float borderWidth = GetStyleFloat(ns, "/cell/border/bottom/width", 1f);

                Color multiSelectColor = new Color(selectionColor.r, selectionColor.g, selectionColor.b, selectionColor.a * 0.4f);
                Color focusColor = new Color(selectionColor.r + 0.04f, selectionColor.g + 0.13f, selectionColor.b + 0.13f, selectionColor.a * 1.4f);

                for (int r = sr; r < er && r < m_RowHeights.Length; r++)
                {
                    for (int c = sc; c < ec && c < m_ColWidths.Length; c++)
                    {
                        // 跳过被合并覆盖的单元格
                        if (TableData.IsCellMerged(r, c)) continue;

                        float cellX = GetColLeftX(c);
                        float cellY = -GetRowTopY(r);
                        float cellW = m_ColWidths[c];
                        float cellH = m_RowHeights[r];

                        // 合并源：扩展尺寸
                        if (TableData.IsMergeSource(r, c, out int mergeRS, out int mergeCS))
                        {
                            for (int i = 1; i < mergeCS && c + i < m_ColWidths.Length; i++)
                                cellW += m_ColWidths[c + i];
                            for (int i = 1; i < mergeRS && r + i < m_RowHeights.Length; i++)
                                cellH += m_RowHeights[r + i];
                        }

                        XTableCellData data = TableData.GetCell(r, c);
                        string cellNS = (data != null && !string.IsNullOrEmpty(data.StyleNamespace))
                            ? data.StyleNamespace : ns;

                        // 多选 / 单选高亮
                        if (IsInSelectionRange(r, c))
                        {
                            var selImg = m_Assembler.GetImage("cell_multiselect");
                            selImg.Rect.anchoredPosition = new Vector2(cellX, cellY);
                            selImg.Rect.sizeDelta = new Vector2(cellW, cellH);
                            selImg.Image.color = (r == m_SelectedRow && c == m_SelectedCol)
                                ? focusColor : multiSelectColor;
                        }
                        else if (r == m_SelectedRow && c == m_SelectedCol)
                        {
                            var selImg = m_Assembler.GetImage("cell_select");
                            selImg.Rect.anchoredPosition = new Vector2(cellX, cellY);
                            selImg.Rect.sizeDelta = new Vector2(cellW, cellH);
                            selImg.Image.color = selectionColor;
                        }

                        // 水平网格线（合并区域底部）
                        var hLine = m_Assembler.GetImage("cell_hline");
                        hLine.Rect.anchoredPosition = new Vector2(cellX, cellY - cellH);
                        hLine.Rect.sizeDelta = new Vector2(cellW, borderWidth);
                        hLine.Image.color = borderColor;

                        // 垂直网格线（合并区域右侧）
                        var vLine = m_Assembler.GetImage("cell_vline");
                        vLine.Rect.anchoredPosition = new Vector2(cellX + cellW - borderWidth, cellY);
                        vLine.Rect.sizeDelta = new Vector2(borderWidth, cellH);
                        vLine.Image.color = borderColor;

                        if (data == null) continue;

                        if (!string.IsNullOrEmpty(data.Text))
                        {
                            var txt = m_Assembler.GetText("cell_text", cellNS);
                            txt.Rect.anchoredPosition = new Vector2(cellX + 4, cellY);
                            txt.Rect.sizeDelta = new Vector2(cellW - 8, cellH);
                            txt.Text.text = data.Text;
                            txt.Text.color = GetStyleColor(cellNS, "/cell/fg/color", fgFallback);
                            txt.Text.fontSize = GetStyleInt(cellNS, "/cell/font/size", 14);
                            txt.Text.alignment = TextAlignmentOptions.Midline;
                            txt.Text.font = GetStyleFontAsset(cellNS, "/cell/font/fontAsset") ?? txt.Text.font;
                        }
                    }
                }
                return;
            }
#endif

            // 运行时模式
            Color rtSelectionColor = new Color(0.18f, 0.42f, 0.82f, 0.25f);
            Color rtMultiSelectColor = new Color(0.18f, 0.42f, 0.82f, 0.12f);
            Color rtFocusColor = new Color(0.22f, 0.55f, 0.95f, 0.35f);

            for (int r = sr; r < er && r < m_RowHeights.Length; r++)
            {
                for (int c = sc; c < ec && c < m_ColWidths.Length; c++)
                {
                    // 跳过被合并覆盖的单元格
                    if (TableData.IsCellMerged(r, c)) continue;

                    float cellX = GetColLeftX(c);
                    float cellY = -GetRowTopY(r);
                    float cellW = m_ColWidths[c];
                    float cellH = m_RowHeights[r];

                    // 合并源：扩展尺寸
                    if (TableData.IsMergeSource(r, c, out int mergeRS, out int mergeCS))
                    {
                        for (int i = 1; i < mergeCS && c + i < m_ColWidths.Length; i++)
                            cellW += m_ColWidths[c + i];
                        for (int i = 1; i < mergeRS && r + i < m_RowHeights.Length; i++)
                            cellH += m_RowHeights[r + i];
                    }

                    XTableCellData data = TableData.GetCell(r, c);
                    string cellNS = (data != null && !string.IsNullOrEmpty(data.StyleNamespace))
                        ? data.StyleNamespace : ns;

                    if (IsInSelectionRange(r, c))
                    {
                        var selImg = m_Assembler.GetImage("cell_multiselect");
                        selImg.Rect.anchoredPosition = new Vector2(cellX, cellY);
                        selImg.Rect.sizeDelta = new Vector2(cellW, cellH);
                        selImg.Image.color = (r == m_SelectedRow && c == m_SelectedCol)
                            ? rtFocusColor : rtMultiSelectColor;
                    }
                    else if (r == m_SelectedRow && c == m_SelectedCol)
                    {
                        var selImg = m_Assembler.GetImage("cell_select");
                        selImg.Rect.anchoredPosition = new Vector2(cellX, cellY);
                        selImg.Rect.sizeDelta = new Vector2(cellW, cellH);
                        selImg.Image.color = rtSelectionColor;
                    }

                    var hLine = m_Assembler.GetImage("cell_hline");
                    hLine.Rect.anchoredPosition = new Vector2(cellX, cellY - cellH);
                    hLine.Rect.sizeDelta = new Vector2(cellW, 1f);
                    hLine.Image.color = new Color(0.35f, 0.35f, 0.35f, 0.5f);

                    var vLine = m_Assembler.GetImage("cell_vline");
                    vLine.Rect.anchoredPosition = new Vector2(cellX + cellW - 1, cellY);
                    vLine.Rect.sizeDelta = new Vector2(1f, cellH);
                    vLine.Image.color = new Color(0.35f, 0.35f, 0.35f, 0.5f);

                    if (data == null) continue;

                    if (!string.IsNullOrEmpty(data.Text))
                    {
                        var txt = m_Assembler.GetText("cell_text", cellNS);
                        txt.Rect.anchoredPosition = new Vector2(cellX + 4, cellY);
                        txt.Rect.sizeDelta = new Vector2(cellW - 8, cellH);
                        txt.Text.text = data.Text;

#if UNITY_EDITOR
                        if (!Application.isPlaying)
                        {
                            txt.Text.color = GetStyleColor(cellNS, "/cell/fg/color", fgFallback);
                            txt.Text.fontSize = GetStyleInt(cellNS, "/cell/font/size", 14);
                            txt.Text.alignment = TextAlignmentOptions.Midline;
                            txt.Text.font = GetStyleFontAsset(cellNS, "/cell/font/fontAsset") ?? txt.Text.font;
                        }
#endif
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
                foreach (float w in m_ColWidths)
                    m_TotalWidth += w;

            m_TotalHeight = 0;
            if (m_RowHeights != null)
                foreach (float h in m_RowHeights)
                    m_TotalHeight += h;

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
            sr = 0;
            er = 0;
            sc = 0;
            ec = 0;

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

        private float GetStyleFloat(string ns, string path, float fallback)
        {
            StyleValue val = StyleManager.GetValue(ns, path);
            return val != null ? (float)val : fallback;
        }

        private TMP_FontAsset GetStyleFontAsset(string ns, string path, TMP_FontAsset fallback = null)
        {
            StyleValue val = StyleManager.GetValue(ns, path);
            if (val == null) return fallback;
            try { return val.GetValueAs<TMP_FontAsset>(); }
            catch { return fallback; }
        }

        private void EnsureDefaultStyleNamespace()
        {
            // 避免在样式表尚未加载时触发 HasStyle → GetValue 的命名空间不存在错误
            var namespaces = StyleManager.GetAvailableNamespaces();
            if (namespaces == null || !namespaces.Contains(m_DefaultStyleNamespace))
                return;

            if (!StyleManager.HasStyle(m_DefaultStyleNamespace, "/cell/bg/color"))
            {
                // 回退默认值匹配 DefaultTableStyles.xsss
                StyleManager.AddDynamicStyle(m_DefaultStyleNamespace, "/cell/bg/color", Color.white);
                StyleManager.AddDynamicStyle(m_DefaultStyleNamespace, "/cell/fg/color",
                    new Color(0.1f, 0.1f, 0.1f));
                StyleManager.AddDynamicStyle(m_DefaultStyleNamespace, "/cell/border/top/color",
                    new Color(0.8f, 0.8f, 0.8f));
                StyleManager.AddDynamicStyle(m_DefaultStyleNamespace, "/cell/border/top/width", 1f);
                StyleManager.AddDynamicStyle(m_DefaultStyleNamespace, "/cell/border/bottom/color",
                    new Color(0.8f, 0.8f, 0.8f));
                StyleManager.AddDynamicStyle(m_DefaultStyleNamespace, "/cell/border/bottom/width", 1f);
                StyleManager.AddDynamicStyle(m_DefaultStyleNamespace, "/cell/border/left/color",
                    new Color(0.8f, 0.8f, 0.8f));
                StyleManager.AddDynamicStyle(m_DefaultStyleNamespace, "/cell/border/left/width", 1f);
                StyleManager.AddDynamicStyle(m_DefaultStyleNamespace, "/cell/border/right/color",
                    new Color(0.8f, 0.8f, 0.8f));
                StyleManager.AddDynamicStyle(m_DefaultStyleNamespace, "/cell/border/right/width", 1f);
                StyleManager.AddDynamicStyle(m_DefaultStyleNamespace, "/cell/selection/bg/color",
                    new Color(0.2f, 0.5f, 1f, 0.3f));
                StyleManager.AddDynamicStyle(m_DefaultStyleNamespace, "/cell/font/size", 14);
                StyleManager.AddDynamicStyle(m_DefaultStyleNamespace, "/cell/font/alignment", "MiddleCenter");
                StyleManager.AddDynamicStyle(m_DefaultStyleNamespace, "/cell/font/richText", true);
#if UNITY_EDITOR
                // 编辑器下尝试加载默认字体资源
                var defaultFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                    "Packages/com.lrss3.xericuiactionvessel/Runtime/Font/SourceHanSansSC-Regular-2 SDF.asset");
                if (defaultFont != null)
                    StyleManager.AddDynamicStyle(m_DefaultStyleNamespace, "/cell/font/fontAsset", defaultFont);
#endif
                StyleManager.AddDynamicStyle(m_DefaultStyleNamespace, "/cell/default/height", 40f);
                StyleManager.AddDynamicStyle(m_DefaultStyleNamespace, "/cell/default/width", 100f);
                StyleManager.ForceRefresh();
            }
        }

        #endregion

        #region 其他

        private void MarkDirty(TableDirtyType type) => m_DirtyFlag.Mark(type);

        /// <summary>运行时样式变更回调：标记脏数据，下一帧重绘</summary>
        private void OnPoolStyleChanged() => MarkDirty(TableDirtyType.StyleChanged);

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
                if (tableX >= ax && tableX < ax + m_ColWidths[c])
                {
                    col = c;
                    break;
                }

                ax += m_ColWidths[c];
            }

            int row = -1;
            float ay = 0;
            for (int r = 0; r < m_RowHeights.Length; r++)
            {
                if (tableY >= ay && tableY < ay + m_RowHeights[r])
                {
                    row = r;
                    break;
                }

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