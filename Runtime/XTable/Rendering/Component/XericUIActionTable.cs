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
using XericUI.XTable.Core;
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
    public partial class XericUIActionTable : XericUIBehaciour, IPointerClickHandler
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

        [System.NonSerialized] private XTableSelectionHandle m_SelectionHandle;

        [System.NonSerialized] private GameObject m_BackgroundGo;

        /// <summary>预制体实例缓存，key 为 (row, col)，用于复用已实例化的预制体</summary>
        [System.NonSerialized] private Dictionary<(int, int), GameObject> m_PrefabInstances;

        /// <summary>表格四叉树——按 StyleNamespace 分区，渲染阶段查询可见象限</summary>
        [System.NonSerialized] private XTableQuadtree m_Quadtree;

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

        public XTableCellData this[int row, int col] => TableData.GetOrCreateCell(row, col);

        public float[] RowHeights => m_RowHeights;
        public float[] ColWidths => m_ColWidths;

        public string DefaultStyleNamespace => m_DefaultStyleNamespace;

        /// <summary>选中操作句柄——外部可通过此句柄控制选区，或直接操作 StartRow/StartCol 等属性</summary>
        public XTableSelectionHandle SelectionHandle
        {
            get
            {
                if (m_SelectionHandle == null)
                    m_SelectionHandle = new XTableSelectionHandle();
                return m_SelectionHandle;
            }
        }

        // ── 向后兼容属性（供 Editor 脚本使用）──
        public int SelectedRow => SelectionHandle.StartRow;
        public int SelectedCol => SelectionHandle.StartCol;
        public int SelectStartRow => SelectionHandle.StartRow;
        public int SelectStartCol => SelectionHandle.StartCol;
        public int SelectEndRow => SelectionHandle.EndRow;
        public int SelectEndCol => SelectionHandle.EndCol;
        public bool HasMultiSelection => SelectionHandle.HasMultiSelection;

        /// <summary>动态子对象在 Hierarchy 中是否可见（仅编辑器有效）</summary>
        public bool ChildrenVisible
        {
            get => m_ChildrenVisible;
            set => SetChildrenVisible(value);
        }

        /// <summary>判断单元格是否在多选范围内</summary>
        public bool IsInSelectionRange(int row, int col)
        {
            return SelectionHandle.IsInRange(row, col);
        }

        /// <summary>
        /// 动态子对象挂载的父节点。
        /// 若绑定了 ScrollRect 则使用其 Content，否则回退到自身的 rectTransform。
        /// </summary>
        public Transform ContentTransform
        {
            get
            {
                if (m_ScrollRect != null && m_ScrollRect.content != null)
                    return m_ScrollRect.content;
                return rectTransform;
            }
        }

        /// <summary>
        /// 基于四叉树查询结果渲染可见象限。
        /// 每个均匀象限渲染整块背景+边框，然后遍历象限内单元格渲染文本/预制体/选择高亮。
        /// </summary>
        private void RenderQuadrants(List<QuadrantResult> quadrants)
        {
            string ns = m_DefaultStyleNamespace;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Color borderColor = GetCellBorderColor(ns);
                float borderWidth = GetCellBorderWidth(ns);
                Color selectionColor = GetCellSelectionBgColor(ns);
                Color multiSelectColor = new Color(selectionColor.r, selectionColor.g, selectionColor.b, selectionColor.a * 0.4f);
                Color focusColor = new Color(selectionColor.r + 0.04f, selectionColor.g + 0.13f, selectionColor.b + 0.13f, selectionColor.a * 1.4f);

                foreach (var q in quadrants)
                {
                    if (string.IsNullOrEmpty(q.Namespace)) continue;

                    Rect r = q.Region;
                    string qNs = q.Namespace;

                    // 象限整块背景
                    var bgImg = m_Assembler.GetImage("quad_bg", qNs);
                    bgImg.Rect.anchoredPosition = new Vector2(r.x, -r.y);
                    bgImg.Rect.sizeDelta = new Vector2(r.width, r.height);
                    bgImg.Image.color = GetCellBgColor(qNs);

                    // 象限底部边框
                    var hLine = m_Assembler.GetImage("quad_hline");
                    hLine.Rect.anchoredPosition = new Vector2(r.x, -r.y - r.height);
                    hLine.Rect.sizeDelta = new Vector2(r.width, borderWidth);
                    hLine.Image.color = borderColor;

                    // 象限右侧边框
                    var vLine = m_Assembler.GetImage("quad_vline");
                    vLine.Rect.anchoredPosition = new Vector2(r.x + r.width - borderWidth, -r.y);
                    vLine.Rect.sizeDelta = new Vector2(borderWidth, r.height);
                    vLine.Image.color = borderColor;

                    // 渲染象限内各单元格的文本/预制体/选择高亮
                    if (q.Cells != null)
                    {
                        foreach (var (row, col) in q.Cells)
                        {
                            // 跳过被合并覆盖的单元格
                            if (TableData.IsCellMerged(row, col)) continue;

                            float cellX = GetColLeftX(col);
                            float cellY = -GetRowTopY(row);
                            float cellW = m_ColWidths[col];
                            float cellH = m_RowHeights[row];

                            // 合并源：扩展尺寸
                            if (TableData.IsMergeSource(row, col, out int mergeRS, out int mergeCS))
                            {
                                for (int i = 1; i < mergeCS && col + i < m_ColWidths.Length; i++)
                                    cellW += m_ColWidths[col + i];
                                for (int i = 1; i < mergeRS && row + i < m_RowHeights.Length; i++)
                                    cellH += m_RowHeights[row + i];
                            }

                            XTableCellData data = TableData.GetCell(row, col);
                            string cellNS = (data != null && !string.IsNullOrEmpty(data.StyleNamespace))
                                ? data.StyleNamespace : ns;

                            // 选择高亮
                            if (IsInSelectionRange(row, col))
                            {
                                var selImg = m_Assembler.GetImage("cell_multiselect");
                                selImg.Rect.anchoredPosition = new Vector2(cellX, cellY);
                                selImg.Rect.sizeDelta = new Vector2(cellW, cellH);
                                selImg.Image.color = (row == SelectionHandle.StartRow && col == SelectionHandle.StartCol)
                                    ? focusColor : multiSelectColor;
                            }
                            else if (row == SelectionHandle.StartRow && col == SelectionHandle.StartCol)
                            {
                                var selImg = m_Assembler.GetImage("cell_select");
                                selImg.Rect.anchoredPosition = new Vector2(cellX, cellY);
                                selImg.Rect.sizeDelta = new Vector2(cellW, cellH);
                                selImg.Image.color = selectionColor;
                            }

                            if (data == null) continue;

                            if (!string.IsNullOrEmpty(data.Text))
                            {
                                var txt = m_Assembler.GetText("cell_text", cellNS);
                                txt.Rect.anchoredPosition = new Vector2(cellX + 4, cellY);
                                txt.Rect.sizeDelta = new Vector2(cellW - 8, cellH);
                                txt.Text.text = data.Text;
                                txt.Text.color = GetCellFgColor(cellNS);
                                txt.Text.fontSize = GetCellFontSize(cellNS);
                                txt.Text.alignment = TextAlignmentOptions.Midline;
                                txt.Text.font = GetCellFontAsset(cellNS) ?? txt.Text.font;
                            }

                            if (data.Prefab != null)
                                PositionPrefabInstance(row, col, data.Prefab, cellX, cellY, cellW, cellH);
                        }
                    }
                }
                return;
            }
#endif

            // 运行时模式
            Color rtSelectionColor = RT_SELECTION_COLOR;
            Color rtMultiSelectColor = RT_MULTI_SELECT_COLOR;
            Color rtFocusColor = RT_FOCUS_COLOR;

            foreach (var q in quadrants)
            {
                if (string.IsNullOrEmpty(q.Namespace)) continue;

                Rect r = q.Region;
                string qNs = q.Namespace;

                var bgImg = m_Assembler.GetImage("quad_bg", qNs);
                bgImg.Rect.anchoredPosition = new Vector2(r.x, -r.y);
                bgImg.Rect.sizeDelta = new Vector2(r.width, r.height);
                bgImg.Image.color = GetCellBgColor(qNs);

                var hLine = m_Assembler.GetImage("quad_hline");
                hLine.Rect.anchoredPosition = new Vector2(r.x, -r.y - r.height);
                hLine.Rect.sizeDelta = new Vector2(r.width, RT_BORDER_WIDTH);
                hLine.Image.color = RT_BORDER_COLOR;

                var vLine = m_Assembler.GetImage("quad_vline");
                vLine.Rect.anchoredPosition = new Vector2(r.x + r.width - RT_BORDER_WIDTH, -r.y);
                vLine.Rect.sizeDelta = new Vector2(RT_BORDER_WIDTH, r.height);
                vLine.Image.color = RT_BORDER_COLOR;

                if (q.Cells != null)
                {
                    foreach (var (row, col) in q.Cells)
                    {
                        // 跳过被合并覆盖的单元格
                        if (TableData.IsCellMerged(row, col)) continue;

                        float cellX = GetColLeftX(col);
                        float cellY = -GetRowTopY(row);
                        float cellW = m_ColWidths[col];
                        float cellH = m_RowHeights[row];

                        // 合并源：扩展尺寸
                        if (TableData.IsMergeSource(row, col, out int mergeRS, out int mergeCS))
                        {
                            for (int i = 1; i < mergeCS && col + i < m_ColWidths.Length; i++)
                                cellW += m_ColWidths[col + i];
                            for (int i = 1; i < mergeRS && row + i < m_RowHeights.Length; i++)
                                cellH += m_RowHeights[row + i];
                        }

                        XTableCellData data = TableData.GetCell(row, col);
                        string cellNS = (data != null && !string.IsNullOrEmpty(data.StyleNamespace))
                            ? data.StyleNamespace : ns;

                        if (IsInSelectionRange(row, col))
                        {
                            var selImg = m_Assembler.GetImage("cell_multiselect");
                            selImg.Rect.anchoredPosition = new Vector2(cellX, cellY);
                            selImg.Rect.sizeDelta = new Vector2(cellW, cellH);
                            selImg.Image.color = (row == SelectionHandle.StartRow && col == SelectionHandle.StartCol)
                                ? rtFocusColor : rtMultiSelectColor;
                        }
                        else if (row == SelectionHandle.StartRow && col == SelectionHandle.StartCol)
                        {
                            var selImg = m_Assembler.GetImage("cell_select");
                            selImg.Rect.anchoredPosition = new Vector2(cellX, cellY);
                            selImg.Rect.sizeDelta = new Vector2(cellW, cellH);
                            selImg.Image.color = rtSelectionColor;
                        }

                        if (data == null) continue;

                        if (!string.IsNullOrEmpty(data.Text))
                        {
                            var txt = m_Assembler.GetText("cell_text", cellNS);
                            txt.Rect.anchoredPosition = new Vector2(cellX + 4, cellY);
                            txt.Rect.sizeDelta = new Vector2(cellW - 8, cellH);
                            txt.Text.text = data.Text;
                        }

                        if (data.Prefab != null)
                            PositionPrefabInstance(row, col, data.Prefab, cellX, cellY, cellW, cellH);
                    }
                }
            }
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

            m_Assembler = new CellAssembler(ContentTransform, m_DefaultStyleNamespace);
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
            ClearPrefabInstances();
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

            // 裁剪选区到新尺寸
            ClampSelectionToBounds();

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

            // 裁剪选区到新尺寸
            ClampSelectionToBounds();

            MarkDirty(TableDirtyType.LayoutChanged | TableDirtyType.DataChanged | TableDirtyType.SelectionChanged);
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>设置外部选择句柄（替换内部句柄）</summary>
        public void SetSelectionHandle(XTableSelectionHandle handle)
        {
            if (m_SelectionHandle != null)
                m_SelectionHandle.OnSelectionChanged -= OnHandleSelectionChanged;
            m_SelectionHandle = handle;
            if (m_SelectionHandle != null)
                m_SelectionHandle.OnSelectionChanged += OnHandleSelectionChanged;
            MarkDirty(TableDirtyType.SelectionChanged);
        }

        /// <summary>单选一个单元格</summary>
        public void SelectCell(int row, int col)
        {
            SelectionHandle.Select(row, col);
            MarkDirty(TableDirtyType.SelectionChanged);
        }

        /// <summary>多选一个范围</summary>
        public void SelectCell(int startRow, int startCol, int endRow, int endCol)
        {
            SelectionHandle.Select(startRow, startCol, endRow, endCol);
            MarkDirty(TableDirtyType.SelectionChanged);
        }

        /// <summary>清除选择</summary>
        public void ClearSelection()
        {
            SelectionHandle.Clear();
            MarkDirty(TableDirtyType.SelectionChanged);
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

            SelectCell(sr, sc, er, ec);
            return true;
        }

        /// <summary>获取当前选区的文本描述（单格 "A0"，多格 "A0:B1"）</summary>
        public string GetSelectionText()
        {
            if (!SelectionHandle.HasSelection) return string.Empty;
            if (SelectionHandle.HasMultiSelection)
            {
                return XTableCoordinateFormat.RangeToText(
                    SelectionHandle.MinRow, SelectionHandle.MinCol,
                    SelectionHandle.MaxRow, SelectionHandle.MaxCol);
            }

            return XTableCoordinateFormat.CellToText(
                SelectionHandle.StartRow, SelectionHandle.StartCol);
        }

        private void OnHandleSelectionChanged(XTableSelectionHandle handle)
        {
            MarkDirty(TableDirtyType.SelectionChanged);
        }

        /// <summary>裁剪选区到当前行列尺寸范围内</summary>
        private void ClampSelectionToBounds()
        {
            int maxR = (m_RowHeights?.Length ?? 0) - 1;
            int maxC = (m_ColWidths?.Length ?? 0) - 1;
            SelectionHandle.ClampTo(maxR, maxC);
        }

        public void RefreshView()
        {
            // 编辑模式下 Awake 可能尚未执行，按需回退初始化
            if (m_Assembler == null)
            {
                if (rectTransform != null)
                    m_Assembler = new CellAssembler(ContentTransform, m_DefaultStyleNamespace);
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

            // 清理上一次渲染的预制体实例
            ClearPrefabInstances();

            // 回收池
            m_Assembler.ReturnAll();

            // 额外清理孤立的直接子对象（防止域重载或异常流程产生的残留）
            Transform poolRoot = m_Assembler.PoolRoot;
            Transform contentT = ContentTransform;
            for (int i = contentT.childCount - 1; i >= 0; i--)
            {
                var child = contentT.GetChild(i);
                if ((poolRoot != null && child == poolRoot)) continue;
                if (m_BackgroundGo != null && child.gameObject == m_BackgroundGo) continue;

                child.gameObject.hideFlags = HideFlags.None;
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }

            // 渲染可见单元格：优先使用四叉树查询
            if (m_Quadtree != null && m_Quadtree.IsBuilt)
            {
                var quadrants = new List<QuadrantResult>();
                m_Quadtree.QueryVisible(m_VisibleRect, quadrants);
                RenderQuadrants(quadrants);
            }
            else
            {
                // 回退：传统行列遍历
                int sr, er, sc, ec;
                CalcVisibleRange(out sr, out er, out sc, out ec);
                if (er > sr && ec > sc)
                    RenderCells(sr, er, sc, ec);
            }

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
            m_BackgroundGo.transform.SetParent(ContentTransform, false);
            m_BackgroundGo.transform.SetAsFirstSibling();

            RectTransform bgRt = m_BackgroundGo.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0, 1);
            bgRt.anchorMax = new Vector2(0, 1);
            bgRt.pivot = new Vector2(0, 1);
            bgRt.anchoredPosition = Vector2.zero;
            bgRt.sizeDelta = new Vector2(m_TotalWidth, m_TotalHeight);

            var bgImg = m_BackgroundGo.GetComponent<Image>();
            bgImg.color = GetCellBgColor(m_DefaultStyleNamespace);
            bgImg.raycastTarget = false;
        }

        /// <summary>销毁 transform 下所有子对象（用于域重载后清理残留）</summary>
        private void ClearAllChildren()
        {
            Transform t = ContentTransform;
            if (t == null) return;

#if UNITY_EDITOR
            // 编辑器模式下先重置 HideFlags 再销毁，防止 DontSave 对象拒绝销毁
            while (t.childCount > 0)
            {
                var child = t.GetChild(0);
                child.gameObject.hideFlags = HideFlags.None;
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }
#else
			for (int i = t.childCount - 1; i >= 0; i--)
			{
				var child = t.GetChild(i);
				Destroy(child.gameObject);
			}
#endif
        }

        /// <summary>设置所有动态子对象的可见性（仅编辑器）</summary>
        public void SetChildrenVisible(bool visible)
        {
            m_ChildrenVisible = visible;
#if UNITY_EDITOR
            Transform t = ContentTransform;
            if (t == null) return;
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var child = t.GetChild(i);
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

            m_Assembler = new CellAssembler(ContentTransform, m_DefaultStyleNamespace);
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

        /// <summary>
        /// 清空所有数据（单元格文本、合并信息），但保留行列排布和样式。
        /// 等价于"清空内容但不改变表格结构"。
        /// </summary>
        public void ClearData()
        {
            if (m_TableData != null)
            {
                m_TableData.ClearAllMerges();
                m_TableData.BlockMap.Clear();
            }
            ClearPrefabInstances();
            m_Quadtree?.Clear();
            ClearSelection();
            MarkDirty(TableDirtyType.DataChanged);
            RefreshView();
        }

        /// <summary>
        /// 清空表格所有内容——数据、行列、样式，等同 <see cref="FullRebuild"/> 但不创建默认示例数据。
        /// </summary>
        public void ClearTable()
        {
            m_Assembler?.DestroyAll();
            ClearPrefabInstances();
            m_Quadtree?.Clear();
            ClearAllChildren();
            m_BackgroundGo = null;

            m_TableData = new XTableData();
            m_RowHeights = null;
            m_ColWidths = null;
            ClearSelection();

            m_Assembler = new CellAssembler(ContentTransform, m_DefaultStyleNamespace);
            MarkDirty(TableDirtyType.All);
            RecalculateTotalSize();
            CreateBackground();
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

        /// <summary>构建剪贴板数据对象（尺寸 + 单元格文本 + 合并信息）</summary>
        public XTableClipboardData BuildClipboardData()
        {
            return XTableClipboardData.FromTable(this);
        }

        /// <summary>将剪贴板数据应用到表格——覆盖尺寸、命名空间、单元格文本、合并信息并刷新</summary>
        public void ApplyClipboardData(XTableClipboardData data)
        {
            if (data == null) return;

            // 确保数据层存在
            if (m_TableData == null)
                m_TableData = new XTableData();

            // 应用尺寸与默认命名空间
            if (data.RowHeights != null && data.RowHeights.Length > 0)
                m_RowHeights = data.RowHeights;
            if (data.ColWidths != null && data.ColWidths.Length > 0)
                m_ColWidths = data.ColWidths;
            if (!string.IsNullOrEmpty(data.DefaultStyleNamespace))
                m_DefaultStyleNamespace = data.DefaultStyleNamespace;

            // 写回单元格文本与合并信息
            data.PopulateToTable(this);

            MarkDirty(TableDirtyType.DataChanged);
            RecalculateTotalSize();
            RefreshView();
            SetChildrenVisible(m_ChildrenVisible);
        }

        #endregion

        #region 单元格渲染

        private void RenderCells(int sr, int er, int sc, int ec)
        {
            string ns = m_DefaultStyleNamespace;

            // 编辑器模式：从样式表 pull 所有颜色值
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Color selectionColor = GetCellSelectionBgColor(ns);
                Color borderColor = GetCellBorderColor(ns);
                float borderWidth = GetCellBorderWidth(ns);

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
                            selImg.Image.color = (r == SelectionHandle.StartRow && c == SelectionHandle.StartCol)
                                ? focusColor : multiSelectColor;
                        }
                        else if (r == SelectionHandle.StartRow && c == SelectionHandle.StartCol)
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
                            txt.Text.color = GetCellFgColor(cellNS);
                            txt.Text.fontSize = GetCellFontSize(cellNS);
                            txt.Text.alignment = TextAlignmentOptions.Midline;
                            txt.Text.font = GetCellFontAsset(cellNS) ?? txt.Text.font;
                        }

                        // 预制体渲染
                        if (data.Prefab != null)
                            PositionPrefabInstance(r, c, data.Prefab, cellX, cellY, cellW, cellH);
                    }
                }
                return;
            }
#endif

            // 运行时模式
            Color rtSelectionColor = RT_SELECTION_COLOR;
            Color rtMultiSelectColor = RT_MULTI_SELECT_COLOR;
            Color rtFocusColor = RT_FOCUS_COLOR;

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
                        selImg.Image.color = (r == SelectionHandle.StartRow && c == SelectionHandle.StartCol)
                            ? rtFocusColor : rtMultiSelectColor;
                    }
                    else if (r == SelectionHandle.StartRow && c == SelectionHandle.StartCol)
                    {
                        var selImg = m_Assembler.GetImage("cell_select");
                        selImg.Rect.anchoredPosition = new Vector2(cellX, cellY);
                        selImg.Rect.sizeDelta = new Vector2(cellW, cellH);
                        selImg.Image.color = rtSelectionColor;
                    }

                    var hLine = m_Assembler.GetImage("cell_hline");
                    hLine.Rect.anchoredPosition = new Vector2(cellX, cellY - cellH);
                    hLine.Rect.sizeDelta = new Vector2(cellW, RT_BORDER_WIDTH);
                    hLine.Image.color = RT_BORDER_COLOR;

                    var vLine = m_Assembler.GetImage("cell_vline");
                    vLine.Rect.anchoredPosition = new Vector2(cellX + cellW - RT_BORDER_WIDTH, cellY);
                    vLine.Rect.sizeDelta = new Vector2(RT_BORDER_WIDTH, cellH);
                    vLine.Image.color = RT_BORDER_COLOR;

                    if (data == null) continue;

                    if (!string.IsNullOrEmpty(data.Text))
                    {
                        var txt = m_Assembler.GetText("cell_text", cellNS);
                        txt.Rect.anchoredPosition = new Vector2(cellX + 4, cellY);
                        txt.Rect.sizeDelta = new Vector2(cellW - 8, cellH);
                        txt.Text.text = data.Text;
                        // 运行时样式由 StyleBoundElement 被动更新，此处仅设置文本内容
                    }

                    // 预制体渲染
                    if (data.Prefab != null)
                        PositionPrefabInstance(r, c, data.Prefab, cellX, cellY, cellW, cellH);
                }
            }
        }

        #endregion

        #region 预制体管理

        /// <summary>获取或创建指定单元格的预制体实例</summary>
        private GameObject GetOrCreatePrefabInstance(int row, int col, GameObject prefab)
        {
            if (m_PrefabInstances == null)
                m_PrefabInstances = new Dictionary<(int, int), GameObject>();

            var key = (row, col);
            if (m_PrefabInstances.TryGetValue(key, out GameObject existing) && existing != null)
                return existing;

            GameObject instance = Instantiate(prefab, ContentTransform);
            instance.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;
            instance.name = $"_prefab_{row}_{col}";
            m_PrefabInstances[key] = instance;
            return instance;
        }

        /// <summary>定位预制体实例到指定单元格区域（锚点左上角，枢轴左上角）</summary>
        private void PositionPrefabInstance(int row, int col, GameObject prefab, float cellX, float cellY, float cellW, float cellH)
        {
            GameObject instance = GetOrCreatePrefabInstance(row, col, prefab);
            if (instance == null) return;

            RectTransform prt = instance.GetComponent<RectTransform>();
            if (prt == null) prt = instance.AddComponent<RectTransform>();

            prt.anchorMin = new Vector2(0, 1);
            prt.anchorMax = new Vector2(0, 1);
            prt.pivot = new Vector2(0, 1);
            prt.anchoredPosition = new Vector2(cellX, cellY);
            prt.sizeDelta = new Vector2(cellW, cellH);
        }

        /// <summary>销毁所有已缓存的预制体实例</summary>
        private void ClearPrefabInstances()
        {
            if (m_PrefabInstances == null) return;

            foreach (var kv in m_PrefabInstances)
            {
                if (kv.Value != null)
                {
                    if (Application.isPlaying)
                        Destroy(kv.Value);
                    else
                        DestroyImmediate(kv.Value);
                }
            }
            m_PrefabInstances.Clear();
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
            // 检查命名空间是否已存在，避免 HasStyle → GetValue 对不存在的命名空间报错
            bool nsExists = StyleManager.GetAvailableNamespaces()?.Contains(m_DefaultStyleNamespace) == true;

            // 命名空间已存在且样式已加载（来自 .xsss），无需添加回退值
            if (nsExists && StyleManager.HasStyle(m_DefaultStyleNamespace, STYLE_CELL_BG_COLOR))
                return;

            // 回退默认值匹配 DefaultTableStyles.xsss
            // AddDynamicStyle 自动创建命名空间（如果尚不存在）
            StyleManager.AddDynamicStyle(m_DefaultStyleNamespace, STYLE_CELL_BG_COLOR, DEFAULT_BG_COLOR);
            StyleManager.AddDynamicStyle(m_DefaultStyleNamespace, STYLE_CELL_FG_COLOR, DEFAULT_FG_COLOR);
            StyleManager.AddDynamicStyle(m_DefaultStyleNamespace, STYLE_CELL_BORDER_TOP_COLOR, DEFAULT_BORDER_COLOR);
            StyleManager.AddDynamicStyle(m_DefaultStyleNamespace, STYLE_CELL_BORDER_TOP_WIDTH, DEFAULT_BORDER_WIDTH);
            StyleManager.AddDynamicStyle(m_DefaultStyleNamespace, STYLE_CELL_BORDER_BOTTOM_COLOR, DEFAULT_BORDER_COLOR);
            StyleManager.AddDynamicStyle(m_DefaultStyleNamespace, STYLE_CELL_BORDER_BOTTOM_WIDTH, DEFAULT_BORDER_WIDTH);
            StyleManager.AddDynamicStyle(m_DefaultStyleNamespace, STYLE_CELL_BORDER_LEFT_COLOR, DEFAULT_BORDER_COLOR);
            StyleManager.AddDynamicStyle(m_DefaultStyleNamespace, STYLE_CELL_BORDER_LEFT_WIDTH, DEFAULT_BORDER_WIDTH);
            StyleManager.AddDynamicStyle(m_DefaultStyleNamespace, STYLE_CELL_BORDER_RIGHT_COLOR, DEFAULT_BORDER_COLOR);
            StyleManager.AddDynamicStyle(m_DefaultStyleNamespace, STYLE_CELL_BORDER_RIGHT_WIDTH, DEFAULT_BORDER_WIDTH);
            StyleManager.AddDynamicStyle(m_DefaultStyleNamespace, STYLE_CELL_SELECTION_BG_COLOR, DEFAULT_SELECTION_COLOR);
            StyleManager.AddDynamicStyle(m_DefaultStyleNamespace, STYLE_CELL_FONT_SIZE, DEFAULT_FONT_SIZE);
            StyleManager.AddDynamicStyle(m_DefaultStyleNamespace, STYLE_CELL_FONT_ALIGNMENT, DEFAULT_FONT_ALIGNMENT);
            StyleManager.AddDynamicStyle(m_DefaultStyleNamespace, STYLE_CELL_FONT_RICH_TEXT, DEFAULT_FONT_RICH_TEXT);
            StyleManager.AddDynamicStyle(m_DefaultStyleNamespace, STYLE_CELL_DEFAULT_HEIGHT, DEFAULT_CELL_HEIGHT);
            StyleManager.AddDynamicStyle(m_DefaultStyleNamespace, STYLE_CELL_DEFAULT_WIDTH, DEFAULT_CELL_WIDTH);
            StyleManager.ForceRefresh();
        }

        #endregion

        #region 其他

        private void MarkDirty(TableDirtyType type)
        {
            m_DirtyFlag.Mark(type);

            // 数据或布局变更时重建四叉树
            if ((type & (TableDirtyType.DataChanged | TableDirtyType.LayoutChanged)) != 0)
                MaintainQuadtree();
        }

        /// <summary>运行时样式变更回调：标记脏数据，下一帧重绘</summary>
        private void OnPoolStyleChanged() => MarkDirty(TableDirtyType.StyleChanged);

        private void OnScrollValueChanged(Vector2 _) => MarkDirty(TableDirtyType.ViewChanged);

        #endregion

        #region 四叉树

        /// <summary>强制重建四叉树（外部调用入口，适合编辑器调试或手动刷新）</summary>
        public void ForceRebuildQuadtree()
        {
            if (m_Quadtree == null)
                m_Quadtree = new XTableQuadtree();
            MaintainQuadtree();
        }

        /// <summary>从当前 XTableData 收集单元格信息并重建四叉树</summary>
        private void MaintainQuadtree()
        {
            if (m_TableData == null || m_RowHeights == null || m_ColWidths == null)
                return;
            if (m_Quadtree == null)
                m_Quadtree = new XTableQuadtree();

            // 收集所有有数据的单元格（跳过被合并覆盖的子单元格）
            var cells = new List<XTableQuadtreeCell>();
            for (int r = 0; r < m_RowHeights.Length; r++)
            {
                for (int c = 0; c < m_ColWidths.Length; c++)
                {
                    if (m_TableData.IsCellMerged(r, c)) continue;

                    var data = m_TableData.GetCell(r, c);
                    if (data == null) continue;

                    string ns = !string.IsNullOrEmpty(data.StyleNamespace)
                        ? data.StyleNamespace : m_DefaultStyleNamespace;
                    cells.Add(new XTableQuadtreeCell { Row = r, Col = c, Namespace = ns });
                }
            }

            m_Quadtree.Build(m_TotalWidth, m_TotalHeight, m_RowHeights, m_ColWidths, cells);
        }

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