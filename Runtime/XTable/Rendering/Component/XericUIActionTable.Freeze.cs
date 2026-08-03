using UnityEngine;
using XericUI.XTable.Rendering.Elements;

namespace XericUI.XTable.Rendering.Component
{
    public partial class XericUIActionTable
    {
        private static readonly Vector2 FrozenAnchor = new(0f, 1f);

        private void CreateViewportRenderer()
        {
            if (m_ScrollRect == null || m_ScrollRect.viewport == null) return;
            CreateFrozenContainers();
            CreateFrozenRenderers();
        }

        private void CreateFrozenContainers()
        {
            Transform viewport = m_ScrollRect.viewport;
            m_FrozenColumnsContainer = CreateFrozenContainer(viewport, "_FrozenColumns");
            m_FrozenRowsContainer = CreateFrozenContainer(viewport, "_FrozenRows");
            m_FrozenCornerContainer = CreateFrozenContainer(viewport, "_FrozenCorner");
        }

        private static RectTransform CreateFrozenContainer(Transform parent, string name)
        {
            var existing = parent.Find(name) as RectTransform;
            if (existing != null) return existing;
            var go = new GameObject(name, typeof(RectTransform));
            go.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = FrozenAnchor;
            rt.anchorMax = FrozenAnchor;
            rt.pivot = FrozenAnchor;
            return rt;
        }

        private void CreateFrozenRenderers()
        {
            if (m_FrozenColumnsContainer == null) return;
            m_FrozenColumnRenderer?.DestroyAll();
            m_FrozenRowRenderer?.DestroyAll();
            m_FrozenCornerRenderer?.DestroyAll();
            DestroyFrozenBorderRenderers();
            m_FrozenColumnRenderer = CreateFrozenRenderer(m_FrozenColumnsContainer);
            m_FrozenRowRenderer = CreateFrozenRenderer(m_FrozenRowsContainer);
            m_FrozenCornerRenderer = CreateFrozenRenderer(m_FrozenCornerContainer);
        }

        private TableRenderer CreateFrozenRenderer(Transform parent)
        {
            var renderer = new TableRenderer(parent)
            {
                Assembler = m_Assembler,
                PrefabPool = m_PrefabPool,
                ActivePrefabMap = m_ActivePrefabMap,
                CellPadding = CellPadding,
                Config = Config,
                SelectionTable = this,
                GetStyleColor = (ns, path, fallback) => GetStyleColor(ns, path, fallback),
                GetStyleInt = (ns, path, fallback) => GetStyleInt(ns, path, fallback),
                GetStyleFontAsset = (ns, path) => GetStyleFontAsset(ns, path),
                GetRowHeights = () => m_ResolvedRowHeights,
                GetColWidths = () => m_ResolvedColWidths,
                GetTableData = () => m_TableData,
                GetDefaultStyleNamespace = () => m_DefaultStyleNamespace,
            };
            return renderer;
        }

        private void DestroyFrozenRenderers()
        {
            m_FrozenColumnRenderer?.DestroyAll();
            m_FrozenRowRenderer?.DestroyAll();
            m_FrozenCornerRenderer?.DestroyAll();
            DestroyFrozenBorderRenderers();
            m_FrozenColumnRenderer = null;
            m_FrozenRowRenderer = null;
            m_FrozenCornerRenderer = null;
            DestroySelectionOverlay(ref m_FrozenColumnSelectionOverlay);
            DestroySelectionOverlay(ref m_FrozenRowSelectionOverlay);
            DestroySelectionOverlay(ref m_FrozenCornerSelectionOverlay);
            DestroyFrozenContainer(ref m_FrozenColumnsContainer);
            DestroyFrozenContainer(ref m_FrozenRowsContainer);
            DestroyFrozenContainer(ref m_FrozenCornerContainer);
        }

        private static void DestroySelectionOverlay(ref XTableSelectionOverlay overlay)
        {
            if (overlay == null) return;
            if (Application.isPlaying) Object.Destroy(overlay.gameObject);
            else Object.DestroyImmediate(overlay.gameObject);
            overlay = null;
        }

        private static void DestroyFrozenContainer(ref RectTransform container)
        {
            if (container == null) return;
            if (Application.isPlaying) Object.Destroy(container.gameObject);
            else Object.DestroyImmediate(container.gameObject);
            container = null;
        }

        private void ReleaseFrozenCells()
        {
            m_FrozenColumnRenderer?.ReleaseAll();
            m_FrozenRowRenderer?.ReleaseAll();
            m_FrozenCornerRenderer?.ReleaseAll();
        }

        private void RenderFrozenCells(bool fullRefresh = true)
        {
            int rows = m_RowHeights?.Length ?? 0;
            int cols = m_ColWidths?.Length ?? 0;
            int freezeRows = Mathf.Clamp(m_FreezeRowCount, 0, rows);
            int freezeCols = Mathf.Clamp(m_FreezeColCount, 0, cols);
            if (freezeRows == 0 && freezeCols == 0)
            {
                DestroyFrozenRenderers();
                if (m_RowHeights != null && m_ColWidths != null)
                    CreateOrUpdateSelectionOverlays(0, 0);
                return;
            }

            if (m_FrozenColumnRenderer == null) CreateViewportRenderer();
            if (m_FrozenColumnRenderer == null) return;
            CreateOrUpdateSelectionOverlays(freezeRows, freezeCols);
            CreateFrozenBackgrounds(freezeRows, freezeCols);
            SyncFrozenContainers();
            CalcVisibleRange(out int visibleStartRow, out int visibleEndRow, out int visibleStartCol, out int visibleEndCol);
#if UNITY_EDITOR
            bool editorMode = !Application.isPlaying;
#else
            bool editorMode = false;
#endif
            // 固定顺序：Content 已先完成，然后冻结列、冻结行、左上交叉区。
            if (freezeCols > 0)
                m_FrozenColumnRenderer.Render(Mathf.Max(freezeRows, visibleStartRow), visibleEndRow,
                    0, freezeCols, editorMode, fullRefresh);
            if (freezeRows > 0)
                m_FrozenRowRenderer.Render(0, freezeRows, Mathf.Max(freezeCols, visibleStartCol), visibleEndCol,
                    editorMode, fullRefresh);
            if (freezeRows > 0 && freezeCols > 0)
                m_FrozenCornerRenderer.Render(0, freezeRows, 0, freezeCols, editorMode, fullRefresh);
        }

        private void CreateFrozenBackgrounds(int freezeRows, int freezeCols)
        {
            float freezeWidth = GetColLeftX(freezeCols);
            float freezeHeight = GetRowTopY(freezeRows);
            if (freezeCols > 0)
                m_FrozenColumnRenderer.CreateBackground(freezeWidth, m_TotalHeight, m_DefaultStyleNamespace);
            if (freezeRows > 0)
                m_FrozenRowRenderer.CreateBackground(m_TotalWidth - freezeWidth, freezeHeight,
                    m_DefaultStyleNamespace, new Vector2(freezeWidth, 0f));
            if (freezeRows > 0 && freezeCols > 0)
                m_FrozenCornerRenderer.CreateBackground(freezeWidth, freezeHeight, m_DefaultStyleNamespace);
        }

        private void CreateOrUpdateSelectionOverlays(int freezeRows, int freezeCols)
        {
            int rows = m_RowHeights.Length;
            int cols = m_ColWidths.Length;
            m_NormalSelectionOverlay = CreateOrUpdateSelectionOverlay(ContentTransform, m_NormalSelectionOverlay, freezeRows, rows, freezeCols, cols, "_NormalSelectionOverlay");
            m_FrozenColumnSelectionOverlay = CreateOrUpdateSelectionOverlay(m_FrozenColumnsContainer, m_FrozenColumnSelectionOverlay, freezeRows, rows, 0, freezeCols, "_FrozenColumnSelectionOverlay");
            m_FrozenRowSelectionOverlay = CreateOrUpdateSelectionOverlay(m_FrozenRowsContainer, m_FrozenRowSelectionOverlay, 0, freezeRows, freezeCols, cols, "_FrozenRowSelectionOverlay");
            m_FrozenCornerSelectionOverlay = CreateOrUpdateSelectionOverlay(m_FrozenCornerContainer, m_FrozenCornerSelectionOverlay, 0, freezeRows, 0, freezeCols, "_FrozenCornerSelectionOverlay");
        }

        private XTableSelectionOverlay CreateOrUpdateSelectionOverlay(Transform parent, XTableSelectionOverlay overlay,
            int rowStart, int rowEnd, int colStart, int colEnd, string name)
        {
            if (parent == null) return overlay;
            if (overlay == null)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image), typeof(XTableSelectionOverlay));
                go.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;
                go.transform.SetParent(parent, false);
                overlay = go.GetComponent<XTableSelectionOverlay>();
            }
            // Overlay 必须处于当前区域最上层，才能先于边框和其他 Graphic 接收 PointerDown/Drag。
            overlay.Configure(this, rowStart, rowEnd, colStart, colEnd);
            overlay.transform.SetAsLastSibling();
            return overlay;
        }

        private void RefreshSelectionOverlays()
        {
            m_NormalSelectionOverlay?.RefreshVisual();
            m_FrozenColumnSelectionOverlay?.RefreshVisual();
            m_FrozenRowSelectionOverlay?.RefreshVisual();
            m_FrozenCornerSelectionOverlay?.RefreshVisual();
        }

        private void SyncFrozenContainers()
        {
            if (m_ScrollRect == null || m_ScrollRect.content == null) return;
            Vector2 contentPosition = m_ScrollRect.content.anchoredPosition;
            SyncFrozenContainer(m_FrozenColumnsContainer, new Vector2(0f, contentPosition.y));
            SyncFrozenContainer(m_FrozenRowsContainer, new Vector2(contentPosition.x, 0f));
            SyncFrozenContainer(m_FrozenCornerContainer, Vector2.zero);
        }

        private void SyncFrozenContainer(RectTransform container, Vector2 position)
        {
            if (container == null) return;
            container.anchoredPosition = position;
            container.sizeDelta = new Vector2(m_TotalWidth, m_TotalHeight);
        }
    }
}
