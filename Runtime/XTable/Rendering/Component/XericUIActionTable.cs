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

        /// <summary>动态子对象挂载的父节点。
        /// 若绑定了 ScrollRect 则使用其 Content，否则回退到自身的 rectTransform。</summary>
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

        /// <summary>判断单元格是否在多选范围内</summary>
        public bool IsInSelectionRange(int row, int col)
        {
            return SelectionHandle.IsInRange(row, col);
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

            // 强制 Content 的 anchor/pivot 为 (0,1)，确保 anchoredPosition 语义一致。
            // 若不固定，ScrollRect 可能改变 anchor 导致坐标计算错误。
            if (m_ScrollRect != null && m_ScrollRect.content != null)
            {
                RectTransform ct = m_ScrollRect.content as RectTransform;
                if (ct != null)
                {
                    ct.anchorMin = new Vector2(0, 1);
                    ct.anchorMax = new Vector2(0, 1);
                    ct.pivot = new Vector2(0, 1);
                }
            }

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

        /// <summary>是否正在同步重建中，此时 Update / EditorUpdate 应跳过以避免重入</summary>
        [System.NonSerialized] private bool m_IsRebuilding;

        private void Update()
        {
            if (m_IsRebuilding || !m_DirtyFlag.IsDirty) return;

            var flags = m_DirtyFlag.Flags;
            // 仅滚动或选区变更 → 轻刷新（不重建边框/背景/预制体全回收）
            if ((flags & ~(TableDirtyType.ScrollChanged | TableDirtyType.SelectionChanged)) == 0)
                RefreshViewLight();
            else
                RefreshView();
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

        #region 脏标记与滚动回调

        public void MarkDirty(TableDirtyType type)
        {
            m_DirtyFlag.Mark(type);

            // 仅数据或布局变更时重建四叉树；滚动/选区变更无需重建
            if ((type & (TableDirtyType.DataChanged | TableDirtyType.LayoutChanged | TableDirtyType.StyleChanged)) != 0)
                MaintainQuadtree();
        }

        /// <summary>运行时样式变更回调：标记脏数据，下一帧重绘</summary>
        private void OnPoolStyleChanged() => MarkDirty(TableDirtyType.StyleChanged);

        [System.NonSerialized] private bool m_IsRefreshingFromScroll;

        private void OnScrollValueChanged(Vector2 _)
        {
            // 防重入：RefreshViewLight 中操作 content 可能触发 ScrollRect 再次回调
            if (m_IsRefreshingFromScroll) return;
            m_IsRefreshingFromScroll = true;

            try
            {
                // 滚动时仅做轻刷新（RefreshViewLight 内部会调用 UpdateVisibleRect），
                // 不重建背景、边框、不重置 content.sizeDelta、不做孤儿清理。
                RefreshViewLight();
            }
            finally
            {
                m_IsRefreshingFromScroll = false;
            }
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
