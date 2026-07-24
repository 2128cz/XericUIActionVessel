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
using XericLibrary.Runtime.UIGraph;
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
    public partial class XericUIActionTable : XericUIBehaviour, IPointerClickHandler
    {
        #region 序列化字段

        [SerializeField] private XTableData m_TableData;

        [SerializeField] private float[] m_RowHeights = new float[] { 40f, 30f };

        [SerializeField] private float[] m_ColWidths = new float[] { 100f, 80f, 120f };

        [SerializeField] private string m_DefaultStyleNamespace = "";

        [SerializeField]
        [Tooltip("表格全局配置资源")]
        private XTableConfig m_Config;

        [SerializeField] private ScrollRect m_ScrollRect;

        [SerializeField]
        [Tooltip("冻结行数——顶部固定不滚动的行数")]
        private int m_FreezeRowCount;

        [SerializeField]
        [Tooltip("冻结列数——左侧固定不滚动的列数")]
        private int m_FreezeColCount;

        [SerializeField]
        [Tooltip("在 Hierarchy 中显示动态生成的子对象（仅编辑器，不保存到场景）")]
        private bool m_ChildrenVisible;

        #endregion

        #region 私有字段

        [System.NonSerialized] private CellAssembler m_Assembler;
        [System.NonSerialized] private XTableDirtyFlag m_DirtyFlag;

        /// <summary>Viewport 层渲染器——用于绘制冻结行列单元格</summary>
        [System.NonSerialized] private TableRenderer m_ViewportRenderer;

        [System.NonSerialized] private XTableSelectionHandle m_SelectionHandle;

        [System.NonSerialized] private GameObject m_BackgroundGo;

		/// <summary>预制体对象池——按原型分组管理</summary>
		[System.NonSerialized] private PrefabPrototypePool m_PrefabPool;

		/// <summary>活跃预制体实例映射 (row, col) → instance，用于回调编排</summary>
		[System.NonSerialized] private Dictionary<(int, int), GameObject> m_ActivePrefabMap;

		/// <summary>Texture2D → Sprite 缓存，避免每帧 Sprite.Create 分配</summary>
		[System.NonSerialized] private Dictionary<Texture2D, Sprite> m_SpriteCache;

        /// <summary>表格四叉树——按 StyleNamespace 分区，渲染阶段查询可见象限</summary>
        [System.NonSerialized] private XTableQuadtree m_Quadtree;

        /// <summary>边框线段渲染器——使用 UILineRendererV2 的坐标线段绘制</summary>
        [System.NonSerialized] private UILineRendererV2 m_BorderRenderer;

        private float m_TotalWidth;
        private float m_TotalHeight;

        /// <summary>内容偏移量（边框半宽），避免 viewport 裁切边缘边框</summary>
        private float m_ContentOffset;

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

        /// <summary>冻结行数</summary>
        public int FreezeRowCount => m_FreezeRowCount;
        /// <summary>冻结列数</summary>
        public int FreezeColCount => m_FreezeColCount;

        /// <summary>设置冻结行列数并重建视图</summary>
        public void SetFreezeCount(int rows, int cols)
        {
            m_FreezeRowCount = Mathf.Max(0, rows);
            m_FreezeColCount = Mathf.Max(0, cols);
            CreateViewportRenderer();
            MarkDirty(TableDirtyType.LayoutChanged);
            RefreshView();
        }

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

        /// <summary>表格全局配置资源</summary>
        public XTableConfig Config
        {
            get
            {
                if (m_Config == null)
                    m_Config = XTableConfig.LoadOrDefault();
                return m_Config;
            }
            set => m_Config = value;
        }

        /// <summary>单元格内容水平内边距（单侧）</summary>
        private float CellPadding => Config.CellHorizontalPadding;
        /// <summary>单元格内容宽度扣除量</summary>
        private float CellPadding2 => Config.CellHorizontalPadding * 2f;

        /// <summary>
        /// 基于四叉树查询结果渲染可见象限内的单元格内容（文本/预制体/选择高亮）。
        /// 边框由 <see cref="RebuildBorders"/> 统一绘制。
        /// </summary>
        private void RenderQuadrants(List<QuadrantResult> quadrants)
        {
            string ns = m_DefaultStyleNamespace;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Color selectionColor = GetCellSelectionBgColor(ns);
                Color multiSelectColor = new Color(selectionColor.r, selectionColor.g, selectionColor.b, selectionColor.a * Config.MultiSelectAlphaFactor);
                Color focusColor = new Color(selectionColor.r + Config.FocusRAdd, selectionColor.g + Config.FocusGAdd, selectionColor.b + Config.FocusBAdd, selectionColor.a * Config.FocusAlphaMul);

                foreach (var q in quadrants)
                {
                    if (string.IsNullOrEmpty(q.Namespace)) continue;

                    Rect r = q.Region;
                    string qNs = q.Namespace;

                    // 渲染象限内各单元格的文本/预制体/选择高亮
                    if (q.Cells != null)
                    {
                        foreach (var (row, col) in q.Cells)
                        {
                            // 跳过被合并覆盖的单元格
                            if (TableData.IsCellMerged(row, col)) continue;
                            // 跳过不可见的单元格
                            if (!IsCellVisible(row, col)) continue;

                            float cellX = GetColLeftX(col) + m_ContentOffset;
                            float cellY = -GetRowTopY(row) - m_ContentOffset;
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

                            switch (data.ContentType)
                            {
                                case CellContentType.Text:
                                    if (!string.IsNullOrEmpty(data.Text))
                                    {
                                        var txt = m_Assembler.GetText("cell_text", cellNS);
                                        txt.Rect.anchoredPosition = new Vector2(cellX + CellPadding, cellY);
                                        txt.Rect.sizeDelta = new Vector2(cellW - CellPadding2, cellH);
                                        txt.Text.text = data.Text;
                                        txt.Text.color = GetCellFgColor(cellNS);
                                        txt.Text.fontSize = GetCellFontSize(cellNS);
                                        txt.Text.alignment = TextAlignmentOptions.Midline;
                                        txt.Text.font = GetCellFontAsset(cellNS) ?? txt.Text.font;
                                    }
                                    break;
                                case CellContentType.Image:
                                    if (data.Image != null)
                                    {
                                        Sprite sprite = GetOrCreateSprite(data.Image);
                                        var img = m_Assembler.GetImage("cell_image", cellNS);
                                        img.Rect.anchoredPosition = new Vector2(cellX + CellPadding, cellY);
                                        img.Rect.sizeDelta = new Vector2(cellW - CellPadding2, cellH);
                                        img.Image.sprite = sprite;
                                    }
                                    break;
                                case CellContentType.Prefab:
                                    if (data.Prefab != null)
                                    {
                                        GameObject instance = m_PrefabPool.Acquire(data.Prefab, row, col);
                                        RectTransform prt = instance.GetComponent<RectTransform>();
                                        prt.anchoredPosition = new Vector2(cellX, cellY);
                                        prt.sizeDelta = new Vector2(cellW, cellH);
                                        m_ActivePrefabMap[(row, col)] = instance;
                                        data.OnPrefabAcquire?.Invoke(instance);
                                    }
                                    break;
                            }
                        }
                    }
                }
                return;
            }
#endif

            // 运行时模式
            Color rtSelectionColor = Config.RuntimeSelectionColor;
            Color rtMultiSelectColor = Config.RuntimeMultiSelectColor;
            Color rtFocusColor = Config.RuntimeFocusColor;

            foreach (var q in quadrants)
            {
                if (string.IsNullOrEmpty(q.Namespace)) continue;

                Rect r = q.Region;
                string qNs = q.Namespace;

                if (q.Cells != null)
                {
                    foreach (var (row, col) in q.Cells)
                    {
                        // 跳过被合并覆盖的单元格
                        if (TableData.IsCellMerged(row, col)) continue;
                        // 跳过不可见的单元格
                        if (!IsCellVisible(row, col)) continue;

                        float cellX = GetColLeftX(col) + m_ContentOffset;
                        float cellY = -GetRowTopY(row) - m_ContentOffset;
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

                        switch (data.ContentType)
                        {
                            case CellContentType.Text:
                                if (!string.IsNullOrEmpty(data.Text))
                                {
                                    var txt = m_Assembler.GetText("cell_text", cellNS);
                                    txt.Rect.anchoredPosition = new Vector2(cellX + CellPadding, cellY);
                                    txt.Rect.sizeDelta = new Vector2(cellW - CellPadding2, cellH);
                                    txt.Text.text = data.Text;
                                }
                                break;
                            case CellContentType.Image:
                                if (data.Image != null)
                                {
                                    Sprite sprite = GetOrCreateSprite(data.Image);
                                    var img = m_Assembler.GetImage("cell_image", cellNS);
                                    img.Rect.anchoredPosition = new Vector2(cellX + CellPadding, cellY);
                                    img.Rect.sizeDelta = new Vector2(cellW - CellPadding2, cellH);
                                    img.Image.sprite = sprite;
                                }
                                break;
                            case CellContentType.Prefab:
                                if (data.Prefab != null)
                                {
                                    GameObject instance = m_PrefabPool.Acquire(data.Prefab, row, col);
                                    RectTransform prt = instance.GetComponent<RectTransform>();
                                    prt.anchoredPosition = new Vector2(cellX, cellY);
                                    prt.sizeDelta = new Vector2(cellW, cellH);
                                    m_ActivePrefabMap[(row, col)] = instance;
                                    data.OnPrefabAcquire?.Invoke(instance);
                                }
                                break;
                        }
                    }
                }
            }
        }

        #endregion

        #region 生命周期

        private static bool s_StyleSheetsPreloaded;

        /// <summary>
        /// 预加载 Resources 目录下所有 XericStyleSheet 资产到 StyleManager。
        /// 仅首次调用生效，后续调用直接跳过。
        /// </summary>
        private static void PreloadAllStyleSheets()
        {
            if (s_StyleSheetsPreloaded) return;
            s_StyleSheetsPreloaded = true;

            var sheets = Resources.LoadAll<XericStyleSheet>("");
            foreach (var sheet in sheets)
            {
                if (sheet != null)
                    StyleManager.LoadStyleSheet(sheet);
            }
        }

        protected override void Awake()
        {
            base.Awake();

            // 预加载 Resources 中所有 .xsss 样式表到 StyleManager（仅首次）
            PreloadAllStyleSheets();

            // 域重载后旧子对象可能残留在场景中（DontSave 不自动销毁），先清空再重建
            ClearAllChildren();

            if (m_Config == null)
                m_Config = XTableConfig.LoadOrDefault();

            if (string.IsNullOrEmpty(m_DefaultStyleNamespace))
                m_DefaultStyleNamespace = Config.DefaultStyleNamespace;

            if (m_TableData == null)
                m_TableData = new XTableData();

            m_Assembler = new CellAssembler(ContentTransform, m_DefaultStyleNamespace);
            m_PrefabPool = new PrefabPrototypePool(ContentTransform);
            m_ActivePrefabMap = new Dictionary<(int, int), GameObject>();
            RecalculateTotalSize();
            EnsureDefaultStyleNamespace();
            CreateDefaultSampleData();
            CreateBackground();

            // 创建 Viewport 层渲染器（用于冻结单元格）
            CreateViewportRenderer();

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
            m_PrefabPool?.DestroyAll();
            m_ActivePrefabMap?.Clear();
            DestroyBorderRenderer();
            if (m_BackgroundGo != null)
            {
                if (Application.isPlaying) Destroy(m_BackgroundGo);
                else DestroyImmediate(m_BackgroundGo);
                m_BackgroundGo = null;
            }

            // 销毁 Viewport 冻结渲染器
            m_ViewportRenderer?.DestroyAll();
            m_ViewportRenderer = null;

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

        /// <summary>设置文本内容（ContentType 自动设为 Text）</summary>
        public XTableCellData SetCellText(int row, int col, string text)
        {
            var data = TableData.GetOrCreateCell(row, col);
            data.Text = text;
            data.ContentType = CellContentType.Text;
            MarkDirty(TableDirtyType.DataChanged);
            return data;
        }

        /// <summary>设置图片纹理（ContentType 自动设为 Image）</summary>
        public XTableCellData SetCellImage(int row, int col, Texture2D image)
        {
            var data = TableData.GetOrCreateCell(row, col);
            data.Image = image;
            data.ContentType = CellContentType.Image;
            MarkDirty(TableDirtyType.DataChanged);
            return data;
        }

        /// <summary>设置预制体（ContentType 自动设为 Prefab）</summary>
        /// <remarks>注意是设置预制体而不是实例，设置后订阅单元格预制体生成事件 OnPrefabAcquire 处理获取事件</remarks>
        public XTableCellData SetCellPrefab(int row, int col, GameObject prefab)
        {
            var data = TableData.GetOrCreateCell(row, col);
            data.Prefab = prefab;
            data.ContentType = CellContentType.Prefab;
            MarkDirty(TableDirtyType.DataChanged);
            return data;
        }

        /// <summary>设置单元格的样式命名空间，同时写入边框缓存（谁最后设置归谁）</summary>
        public XTableCellData SetCellStyle(int row, int col, string styleNamespace, string stylePath)
        {
            var data = TableData.GetOrCreateCell(row, col);
            if (data.CellStyle == null)
                data.CellStyle = new XericLibrary.Runtime.SuperStyleSheet.StyleMember();
            data.CellStyle.CurrentNamespace = styleNamespace ?? "";
            data.CellStyle.StylePath = stylePath ?? "";
            MarkDirty(TableDirtyType.StyleChanged);
            return data;
        }

        /// <summary>设置单元格的样式命名空间（兼容旧版，等同于 StyleNamespace 字段）</summary>
        public void SetCellStyleNamespace(int row, int col, string styleNamespace)
        {
            // 合并源：遍历所有被合并单元格，全部写入该样式
            if (TableData.IsMergeSource(row, col, out int rowSpan, out int colSpan))
            {
                for (int r = row; r < row + rowSpan; r++)
                {
                    for (int c = col; c < col + colSpan; c++)
                    {
                        if (r < m_RowHeights.Length && c < m_ColWidths.Length)
                            ApplyCellStyleToCell(r, c, styleNamespace);
                    }
                }
            }
            else
            {
                ApplyCellStyleToCell(row, col, styleNamespace);
            }

            MarkDirty(TableDirtyType.StyleChanged);
        }

        /// <summary>在单个单元格上设置样式，同时写入其右侧和底部的边框缓存</summary>
        private void ApplyCellStyleToCell(int row, int col, string ns)
        {
            var data = TableData.GetOrCreateCell(row, col);
            data.StyleNamespace = ns;
            TableData.SetBorderRightNs(row, col, ns);
            TableData.SetBorderBottomNs(row, col, ns);
        }

        /// <summary>清除文本内容</summary>
        public void ClearCellText(int row, int col)
        {
            var data = TableData.GetCell(row, col);
            if (data == null) return;
            data.Text = null;
            if (data.ContentType == CellContentType.Text)
                data.ContentType = CellContentType.Text; // 保持类型不变，仅清空内容
            MarkDirty(TableDirtyType.DataChanged);
        }

        /// <summary>清除图片纹理</summary>
        public void ClearCellImage(int row, int col)
        {
            var data = TableData.GetCell(row, col);
            if (data == null) return;
            data.Image = null;
            MarkDirty(TableDirtyType.DataChanged);
        }

        /// <summary>清除预制体及委托</summary>
        public void ClearCellPrefab(int row, int col)
        {
            var data = TableData.GetCell(row, col);
            if (data == null) return;
            data.Prefab = null;
            data.OnPrefabAcquire = null;
            data.OnPrefabRelease = null;
            MarkDirty(TableDirtyType.DataChanged);
        }

        /// <summary>清除单元格样式覆盖</summary>
        public void ClearCellStyle(int row, int col)
        {
            var data = TableData.GetCell(row, col);
            if (data == null) return;
            data.CellStyle = null;
            MarkDirty(TableDirtyType.StyleChanged);
        }

        public XTableCellData GetCellData(int row, int col)
            => TableData.GetOrCreateCell(row, col);

        /// <summary>获取单元格的样式命名空间（含回退逻辑）</summary>
        public string GetCellStyleNamespace(int row, int col)
        {
            var data = TableData.GetCell(row, col);
            return data != null ? data.GetEffectiveStyleNamespace(m_DefaultStyleNamespace) : m_DefaultStyleNamespace;
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

        /// <summary>在末尾追加一行，默认行高来自 Config</summary>
        public void AddRow(float height = -1f)
        {
            if (height <= 0) height = Config.DefaultRowHeight;
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

        /// <summary>在末尾追加一列，默认列宽来自 Config</summary>
        public void AddColumn(float width = -1f)
        {
            if (width <= 0) width = Config.DefaultColumnWidth;
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

            // 确保 ScrollRect Content 尺寸正确（含 padding 偏移量，防止 viewport 裁切边缘边框）
            RectTransform ctRt = ContentTransform as RectTransform;
            if (ctRt != null)
            {
                float pad2 = m_ContentOffset * 2f;
                ctRt.sizeDelta = new Vector2(m_TotalWidth + pad2, m_TotalHeight + pad2);
            }

            // 更新背景尺寸（仅覆盖网格区域，无需 padding）
            if (m_BackgroundGo != null)
            {
                var bgRt = m_BackgroundGo.GetComponent<RectTransform>();
                bgRt.sizeDelta = new Vector2(m_TotalWidth, m_TotalHeight);
            }

            // 归还预制体前，触发所有活跃实例的释放回调
            if (m_ActivePrefabMap != null)
            {
                foreach (var kv in m_ActivePrefabMap)
                {
                    var data = TableData.GetCell(kv.Key.Item1, kv.Key.Item2);
                    data?.OnPrefabRelease?.Invoke(kv.Value);
                }
                m_ActivePrefabMap.Clear();
            }

            // 回收预制体池
            m_PrefabPool?.ReturnAll();

            // 回收 CellAssembler 池
            m_Assembler.ReturnAll();

            // 额外清理孤立的直接子对象（防止域重载或异常流程产生的残留）
            Transform poolRoot = m_Assembler.PoolRoot;
            Transform prefabPoolRoot = m_PrefabPool?.PoolRoot;
            Transform contentT = ContentTransform;
            Transform borderT = contentT.Find("_TableBorders");
            for (int i = contentT.childCount - 1; i >= 0; i--)
            {
                var child = contentT.GetChild(i);
                if (poolRoot != null && child == poolRoot) continue;
                if (prefabPoolRoot != null && child == prefabPoolRoot) continue;
                if (m_BackgroundGo != null && child.gameObject == m_BackgroundGo) continue;
                if (borderT != null && child == borderT) continue;

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

            // 重建边框线段
            RebuildBorders();

            // 渲染 Viewport 层冻结单元格
            RenderFrozenCells();

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
            bgRt.anchoredPosition = new Vector2(m_ContentOffset, -m_ContentOffset);
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
            m_PrefabPool?.DestroyAll();
            ClearAllChildren();
            m_BackgroundGo = null;
            m_BorderRenderer = null;

            m_TableData = new XTableData();

            m_Assembler = new CellAssembler(ContentTransform, m_DefaultStyleNamespace);
            m_PrefabPool = new PrefabPrototypePool(ContentTransform);
            m_ActivePrefabMap = new Dictionary<(int, int), GameObject>();
            RecalculateTotalSize();
            CreateDefaultSampleData();
            CreateBackground();
            CreateViewportRenderer();
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
            m_ActivePrefabMap?.Clear();
            m_PrefabPool?.ReturnAll();
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
            m_PrefabPool?.DestroyAll();
            m_ActivePrefabMap?.Clear();
            m_Quadtree?.Clear();
            ClearAllChildren();
            m_BackgroundGo = null;
            m_BorderRenderer = null;

            m_TableData = new XTableData();
            m_RowHeights = null;
            m_ColWidths = null;
            ClearSelection();

            m_Assembler = new CellAssembler(ContentTransform, m_DefaultStyleNamespace);
            m_PrefabPool = new PrefabPrototypePool(ContentTransform);
            m_ActivePrefabMap = new Dictionary<(int, int), GameObject>();
            MarkDirty(TableDirtyType.All);
            RecalculateTotalSize();
            CreateBackground();
            CreateViewportRenderer();
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

                Color multiSelectColor = new Color(selectionColor.r, selectionColor.g, selectionColor.b, selectionColor.a * Config.MultiSelectAlphaFactor);
                Color focusColor = new Color(selectionColor.r + Config.FocusRAdd, selectionColor.g + Config.FocusGAdd, selectionColor.b + Config.FocusBAdd, selectionColor.a * Config.FocusAlphaMul);

                for (int r = sr; r < er && r < m_RowHeights.Length; r++)
                {
                    for (int c = sc; c < ec && c < m_ColWidths.Length; c++)
                    {
                        // 跳过被合并覆盖的单元格
                        if (TableData.IsCellMerged(r, c)) continue;

                        float cellX = GetColLeftX(c) + m_ContentOffset;
                        float cellY = -GetRowTopY(r) - m_ContentOffset;
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

                        switch (data.ContentType)
                        {
                            case CellContentType.Text:
                                if (!string.IsNullOrEmpty(data.Text))
                                {
                                    var txt = m_Assembler.GetText("cell_text", cellNS);
                                    txt.Rect.anchoredPosition = new Vector2(cellX + CellPadding, cellY);
                                    txt.Rect.sizeDelta = new Vector2(cellW - CellPadding2, cellH);
                                    txt.Text.text = data.Text;
                                    txt.Text.color = GetCellFgColor(cellNS);
                                    txt.Text.fontSize = GetCellFontSize(cellNS);
                                    txt.Text.alignment = TextAlignmentOptions.Midline;
                                    txt.Text.font = GetCellFontAsset(cellNS) ?? txt.Text.font;
                                }
                                break;
                            case CellContentType.Image:
                                if (data.Image != null)
                                {
                                    Sprite sprite = GetOrCreateSprite(data.Image);
                                    var img = m_Assembler.GetImage("cell_image", cellNS);
                                    img.Rect.anchoredPosition = new Vector2(cellX + CellPadding, cellY);
                                    img.Rect.sizeDelta = new Vector2(cellW - CellPadding2, cellH);
                                    img.Image.sprite = sprite;
                                }
                                break;
                            case CellContentType.Prefab:
                                if (data.Prefab != null)
                                {
                                    GameObject instance = m_PrefabPool.Acquire(data.Prefab, r, c);
                                    RectTransform prt = instance.GetComponent<RectTransform>();
                                    prt.anchoredPosition = new Vector2(cellX, cellY);
                                    prt.sizeDelta = new Vector2(cellW, cellH);
                                    m_ActivePrefabMap[(r, c)] = instance;
                                    data.OnPrefabAcquire?.Invoke(instance);
                                }
                                break;
                        }
                    }
                }
                return;
            }
#endif

            // 运行时模式
            Color rtSelectionColor = Config.RuntimeSelectionColor;
            Color rtMultiSelectColor = Config.RuntimeMultiSelectColor;
            Color rtFocusColor = Config.RuntimeFocusColor;

            for (int r = sr; r < er && r < m_RowHeights.Length; r++)
            {
                for (int c = sc; c < ec && c < m_ColWidths.Length; c++)
                {
                    // 跳过被合并覆盖的单元格
                    if (TableData.IsCellMerged(r, c)) continue;

                    float cellX = GetColLeftX(c) + m_ContentOffset;
                    float cellY = -GetRowTopY(r) - m_ContentOffset;
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
                    hLine.Rect.sizeDelta = new Vector2(cellW, Config.RuntimeBorderWidth);
                    hLine.Image.color = Config.RuntimeBorderColor;

                    var vLine = m_Assembler.GetImage("cell_vline");
                    vLine.Rect.anchoredPosition = new Vector2(cellX + cellW - Config.RuntimeBorderWidth, cellY);
                    vLine.Rect.sizeDelta = new Vector2(Config.RuntimeBorderWidth, cellH);
                    vLine.Image.color = Config.RuntimeBorderColor;

                    if (data == null) continue;

                    switch (data.ContentType)
                    {
                        case CellContentType.Text:
                            if (!string.IsNullOrEmpty(data.Text))
                            {
                                var txt = m_Assembler.GetText("cell_text", cellNS);
                                txt.Rect.anchoredPosition = new Vector2(cellX + CellPadding, cellY);
                                txt.Rect.sizeDelta = new Vector2(cellW - CellPadding2, cellH);
                                txt.Text.text = data.Text;
                                // 运行时样式由 StyleBoundElement 被动更新，此处仅设置文本内容
                            }
                            break;
                        case CellContentType.Image:
                            if (data.Image != null)
                            {
                                Sprite sprite = GetOrCreateSprite(data.Image);
                                var img = m_Assembler.GetImage("cell_image", cellNS);
                                img.Rect.anchoredPosition = new Vector2(cellX + CellPadding, cellY);
                                img.Rect.sizeDelta = new Vector2(cellW - CellPadding2, cellH);
                                img.Image.sprite = sprite;
                            }
                            break;
                        case CellContentType.Prefab:
                            if (data.Prefab != null)
                            {
                                GameObject instance = m_PrefabPool.Acquire(data.Prefab, r, c);
                                RectTransform prt = instance.GetComponent<RectTransform>();
                                prt.anchoredPosition = new Vector2(cellX, cellY);
                                prt.sizeDelta = new Vector2(cellW, cellH);
                                m_ActivePrefabMap[(r, c)] = instance;
                                data.OnPrefabAcquire?.Invoke(instance);
                            }
                            break;
                    }
                }
            }
        }

        #endregion

        #region 坐标计算

        /// <summary>判断指定单元格是否与当前可见矩形有重叠（用于 RenderQuadrants 裁剪）</summary>
        private bool IsCellVisible(int row, int col)
        {
            float cellTop = GetRowTopY(row);
            float cellBottom = cellTop + m_RowHeights[row];
            float cellLeft = GetColLeftX(col);
            float cellRight = cellLeft + m_ColWidths[col];

            return cellBottom > m_VisibleRect.y && cellTop < m_VisibleRect.yMax &&
                   cellRight > m_VisibleRect.x && cellLeft < m_VisibleRect.xMax;
        }

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

        /// <summary>将 Texture2D 转为 Sprite（带缓存），用于 Image 类型单元格渲染</summary>
        private Sprite GetOrCreateSprite(Texture2D texture)
        {
            if (texture == null) return null;

            if (m_SpriteCache == null)
                m_SpriteCache = new Dictionary<Texture2D, Sprite>();

            if (!m_SpriteCache.TryGetValue(texture, out Sprite sprite))
            {
                sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                m_SpriteCache[texture] = sprite;
            }
            return sprite;
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

            m_ContentOffset = GetCellBorderWidth(m_DefaultStyleNamespace) * 0.5f;
            float pad = m_ContentOffset * 2f;

            if (m_ScrollRect != null && m_ScrollRect.content != null)
                m_ScrollRect.content.sizeDelta = new Vector2(
                    m_TotalWidth + pad, m_TotalHeight + pad);
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
                    float sy = Mathf.Max(0, -ct.anchoredPosition.y);
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

        public void MarkDirty(TableDirtyType type)
        {
            m_DirtyFlag.Mark(type);

            // 数据或布局变更时重建四叉树
            if ((type & (TableDirtyType.DataChanged | TableDirtyType.LayoutChanged)) != 0)
                MaintainQuadtree();
        }

        /// <summary>运行时样式变更回调：标记脏数据，下一帧重绘</summary>
        private void OnPoolStyleChanged() => MarkDirty(TableDirtyType.StyleChanged);

        [System.NonSerialized] private bool m_IsRefreshingFromScroll;

        private void OnScrollValueChanged(Vector2 _)
        {
            // 防重入：RefreshView 中修改 content.sizeDelta 可能触发 ScrollRect 再次回调
            if (m_IsRefreshingFromScroll) return;
            m_IsRefreshingFromScroll = true;

            try
            {
                // 滚动时立即更新可见矩形并刷新视图，不依赖脏标记延迟机制。
                // 表格本身没有移动能力，完全依赖 ScrollRect 的 content 偏移和 Mask 裁剪，
                // 因此必须在 ScrollRect 值变化时立即重算可见范围。
                UpdateVisibleRect();
                RefreshView();
            }
            finally
            {
                m_IsRefreshingFromScroll = false;
            }
        }

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

            m_Quadtree.Build(m_TotalWidth, m_TotalHeight, m_RowHeights, m_ColWidths, cells, Config.QuadtreeMaxCellsPerNode);
        }

        #endregion

        #region 边框渲染

        /// <summary>
        /// 获取或创建边框渲染器 GameObject 和 <see cref="UILineRendererV2"/> 组件。
        /// </summary>
        private UILineRendererV2 GetOrCreateBorderRenderer()
        {
            if (m_BorderRenderer != null)
                return m_BorderRenderer;

            Transform contentT = ContentTransform;
            // 查找已有对象
            var existing = contentT.Find("_TableBorders");
            if (existing != null)
            {
                m_BorderRenderer = existing.GetComponent<UILineRendererV2>();
                if (m_BorderRenderer != null) return m_BorderRenderer;
            }

            var go = new GameObject("_TableBorders",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(UILineRendererV2));
            go.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;
            go.transform.SetParent(contentT, false);

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            float pad = m_ContentOffset * 2f;
            rt.anchoredPosition = new Vector2(m_ContentOffset, -m_ContentOffset);
            rt.sizeDelta = new Vector2(m_TotalWidth + pad, m_TotalHeight + pad);

            m_BorderRenderer = go.GetComponent<UILineRendererV2>();
            return m_BorderRenderer;
        }

        /// <summary>销毁边框渲染器</summary>
        private void DestroyBorderRenderer()
        {
            if (m_BorderRenderer != null)
            {
                if (Application.isPlaying)
                    Destroy(m_BorderRenderer.gameObject);
                else
                    DestroyImmediate(m_BorderRenderer.gameObject);
                m_BorderRenderer = null;
            }
        }

        /// <summary>
        /// 重建表格的全部边框线段。
        /// 第一阶段：从 OuterBorderNs 绘制 -1 行（顶部外框）和 -1 列（左侧外框）。
        /// 第二阶段：遍历非合并覆盖单元格，从边框缓存读取样式命名空间，按 per-cell 样式绘制右侧和底部边框。
        /// 合并源在最外沿绘制，内部边框自动跳过。
        /// </summary>
        private void RebuildBorders()
        {
            if (m_RowHeights == null || m_ColWidths == null) return;
            if (m_RowHeights.Length == 0 || m_ColWidths.Length == 0) return;

            var renderer = GetOrCreateBorderRenderer();
            renderer.ClearAll();

            var borderRt = renderer.GetComponent<RectTransform>();
            borderRt.sizeDelta = new Vector2(m_TotalWidth, m_TotalHeight);

#if UNITY_EDITOR
            Color borderColor;
            float borderWidth;
            if (!Application.isPlaying)
            {
                borderColor = GetCellBorderColor(m_DefaultStyleNamespace);
                borderWidth = GetCellBorderWidth(m_DefaultStyleNamespace);
            }
            else
            {
                borderColor = Config.RuntimeBorderColor;
                borderWidth = Config.RuntimeBorderWidth;
            }
#else
            Color borderColor = Config.RuntimeBorderColor;
            float borderWidth = Config.RuntimeBorderWidth;
#endif

            int rows = m_RowHeights.Length;
            int cols = m_ColWidths.Length;

            // ════════════════════════════════════════════
            // 阶段 1：-1 行（顶部外框）和 -1 列（左侧外框）
            // ════════════════════════════════════════════

            // 顶部外框：从 col=0 到 col=cols-1，每列一段
            float topY = 0;
            for (int c = 0; c < cols; c++)
            {
                string ns = m_TableData?.GetOuterBorderNs(-1, c);
                if (string.IsNullOrEmpty(ns)) ns = m_DefaultStyleNamespace;
                Color color = GetCellBorderColor(ns);
                float width = GetCellBorderWidth(ns);
                float leftX = GetColLeftX(c);
                renderer.DrawLine(
                    new Vector2(leftX, topY),
                    new Vector2(leftX + m_ColWidths[c], topY),
                    color, width, UVMode.ByDistance);
            }

            // 左侧外框：从 row=0 到 row=rows-1，每行一段
            float xEdge = 0;
            for (int r = 0; r < rows; r++)
            {
                string ns = m_TableData?.GetOuterBorderNs(r, -1);
                if (string.IsNullOrEmpty(ns)) ns = m_DefaultStyleNamespace;
                Color color = GetCellBorderColor(ns);
                float width = GetCellBorderWidth(ns);
                float rowTopY = -GetRowTopY(r);
                renderer.DrawLine(
                    new Vector2(xEdge, rowTopY),
                    new Vector2(xEdge, rowTopY - m_RowHeights[r]),
                    color, width, UVMode.ByDistance);
            }

            // ════════════════════════════════════════════
            // 阶段 2：每个单元格的右侧和底部边框
            // ════════════════════════════════════════════

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    // 跳过被合并覆盖的单元格
                    if (m_TableData != null && m_TableData.IsCellMerged(r, c))
                        continue;

                    float cellX = GetColLeftX(c);
                    float cellY = -GetRowTopY(r);
                    float cellW = m_ColWidths[c];
                    float cellH = m_RowHeights[r];

                    int mergeRS = 1, mergeCS = 1;
                    if (m_TableData != null)
                        m_TableData.IsMergeSource(r, c, out mergeRS, out mergeCS);

                    // 扩展合并尺寸
                    for (int i = 1; i < mergeCS && c + i < cols; i++)
                        cellW += m_ColWidths[c + i];
                    for (int i = 1; i < mergeRS && r + i < rows; i++)
                        cellH += m_RowHeights[r + i];

                    float rightX = cellX + cellW;
                    float bottomY = cellY - cellH;

                    // —— 右侧边框 ——
                    string rightNs = m_TableData?.GetBorderRightNs(r, c);
                    if (string.IsNullOrEmpty(rightNs)) rightNs = m_DefaultStyleNamespace;
                    Color rightColor = GetCellBorderColor(rightNs);
                    float rightWidth = GetCellBorderWidth(rightNs);

                    int rightCol = c + mergeCS;
                    if (rightCol <= cols)
                    {
                        renderer.DrawLine(
                            new Vector2(rightX, cellY),
                            new Vector2(rightX, bottomY),
                            rightColor, rightWidth, UVMode.ByDistance);
                    }

                    // —— 底部边框 ——
                    string bottomNs = m_TableData?.GetBorderBottomNs(r, c);
                    if (string.IsNullOrEmpty(bottomNs)) bottomNs = m_DefaultStyleNamespace;
                    Color bottomColor = GetCellBorderColor(bottomNs);
                    float bottomWidth = GetCellBorderWidth(bottomNs);

                    int bottomRow = r + mergeRS;
                    if (bottomRow <= rows)
                    {
                        renderer.DrawLine(
                            new Vector2(cellX, bottomY),
                            new Vector2(rightX, bottomY),
                            bottomColor, bottomWidth, UVMode.ByDistance);
                    }
                }
            }
        }

        #endregion

        #region Viewport 冻结渲染

        /// <summary>创建或重新创建 Viewport 层冻结渲染器</summary>
        private void CreateViewportRenderer()
        {
            if (m_ScrollRect == null || m_ScrollRect.viewport == null) return;
            if (m_FreezeRowCount <= 0 && m_FreezeColCount <= 0) return;

            // 清理旧渲染器
            m_ViewportRenderer?.DestroyAll();

            Transform viewportT = m_ScrollRect.viewport;
            m_ViewportRenderer = new TableRenderer(viewportT, m_DefaultStyleNamespace, OnPoolStyleChanged);

            // 配置委托
            m_ViewportRenderer.GetStyleColor = (ns, path, fb) => GetStyleColor(ns, path, fb);
            m_ViewportRenderer.GetStyleFloat = (ns, path, fb) => GetStyleFloat(ns, path, fb);
            m_ViewportRenderer.GetStyleInt = (ns, path, fb) => GetStyleInt(ns, path, fb);
            m_ViewportRenderer.GetStyleFontAsset = (ns, path) => GetStyleFontAsset(ns, path);
            m_ViewportRenderer.GetRowHeights = () => m_RowHeights;
            m_ViewportRenderer.GetColWidths = () => m_ColWidths;
            m_ViewportRenderer.GetTableData = () => m_TableData;
            m_ViewportRenderer.GetDefaultStyleNamespace = () => m_DefaultStyleNamespace;
            m_ViewportRenderer.IsInSelectionRange = (r, c) => IsInSelectionRange(r, c);
            m_ViewportRenderer.GetSelectedRow = () => SelectionHandle.StartRow;
            m_ViewportRenderer.GetSelectedCol = () => SelectionHandle.StartCol;

            // 仅渲染冻结范围
            m_ViewportRenderer.StartRow = 0;
            m_ViewportRenderer.EndRow = m_FreezeRowCount;
            m_ViewportRenderer.StartCol = 0;
            m_ViewportRenderer.EndCol = m_FreezeColCount;

            // 共享预制体对象池
            m_ViewportRenderer.PrefabPool = m_PrefabPool;
            m_ViewportRenderer.ActivePrefabMap = m_ActivePrefabMap;
            m_ViewportRenderer.CellPadding = Config.CellHorizontalPadding;
            m_ViewportRenderer.Config = Config;
        }

        /// <summary>渲染 Viewport 层冻结单元格，随滚动偏移调整位置</summary>
        private void RenderFrozenCells()
        {
            if (m_FreezeRowCount <= 0 && m_FreezeColCount <= 0)
            {
                // 无冻结需求时清理渲染器
                if (m_ViewportRenderer != null)
                {
                    m_ViewportRenderer.DestroyAll();
                    m_ViewportRenderer = null;
                }
                return;
            }

            // 检测冻结范围变更，重建渲染器
            if (m_ViewportRenderer == null ||
                m_ViewportRenderer.EndRow != m_FreezeRowCount ||
                m_ViewportRenderer.EndCol != m_FreezeColCount)
                CreateViewportRenderer();

            // 计算冻结区域尺寸
            float freezeW = 0, freezeH = 0;
            for (int c = 0; c < m_FreezeColCount && c < m_ColWidths.Length; c++)
                freezeW += m_ColWidths[c];
            for (int r = 0; r < m_FreezeRowCount && r < m_RowHeights.Length; r++)
                freezeH += m_RowHeights[r];

            // 创建/更新背景
            m_ViewportRenderer.CreateBackground(freezeW, freezeH, m_DefaultStyleNamespace);

            // 获取滚动偏移
            Vector2 scrollOffset = Vector2.zero;
            if (m_ScrollRect != null && m_ScrollRect.content != null)
            {
                scrollOffset.x = Mathf.Max(0, -m_ScrollRect.content.anchoredPosition.x);
                scrollOffset.y = Mathf.Max(0, -m_ScrollRect.content.anchoredPosition.y);
            }

            // 调整背景位置（随滚动偏移移动，保持在视口可见范围内）
            if (m_ViewportRenderer.BackgroundGo != null)
            {
                var bgRt = m_ViewportRenderer.BackgroundGo.GetComponent<RectTransform>();
                bgRt.anchoredPosition = new Vector2(scrollOffset.x, -scrollOffset.y);
            }

            // 回收并重新渲染冻结单元格
            m_ViewportRenderer.ReturnAll();
            m_ViewportRenderer.RenderCells(
                0, m_FreezeRowCount,
                0, m_FreezeColCount,
                !Application.isPlaying);

            // 冻结单元格需要跟随滚动偏移（相对于 Viewport 定位）
            if (m_ViewportRenderer.Assembler != null)
            {
                // 调整所有活跃的池对象的 anchoredPosition 以抵消滚动
                // 这通过重新定位背景实现，单元格相对于背景 (0,0) 定位，所以随背景一起移动
            }

            // 背景始终可见
            m_ViewportRenderer.BackgroundGo?.SetActive(true);
        }

        #endregion

        #region IPointerClickHandler

        public void OnPointerClick(PointerEventData eventData)
        {
            // 直接用 ContentTransform 做坐标转换：单元格是 Content 的子对象，
            // 其 anchoredPosition 就在 Content 局部空间中，无需手工换算 scroll 偏移。
            RectTransform contentRt = ContentTransform as RectTransform;
            if (contentRt == null) return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                contentRt, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
                return;

            // 转换为左上角坐标系（Content 的 anchor/pivot 都是 (0,1)）
            float tableX = localPoint.x - contentRt.rect.xMin - m_ContentOffset;
            float tableY = contentRt.rect.yMax - localPoint.y - m_ContentOffset;

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