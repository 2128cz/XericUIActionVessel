using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using XericUI.XTable.Core;
using XericUI.XTable.Rendering.Elements;
using XericLibrary.Runtime.SuperStyleSheet;

namespace XericUI.XTable.Rendering.Component
{
    /// <summary>
    /// 表格渲染管线封装——将 CellAssembler 对象池、背景创建、单元格渲染抽离为可复用单元。
    /// 同一表格可持有多个 TableRenderer 实例，分别渲染到不同父节点（如 Content 和 Viewport）。
    /// </summary>
    public class TableRenderer
    {
        #region 内部字段

        private Transform m_Parent;
		private CellAssembler m_Assembler;
		private GameObject m_BackgroundGo;

		/// <summary>共享预制体对象池引用（由 XericUIActionTable 传入）</summary>
		public PrefabPrototypePool PrefabPool;

		/// <summary>共享活跃预制体映射表（由 XericUIActionTable 传入）</summary>
		public Dictionary<(int, int), GameObject> ActivePrefabMap;

		/// <summary>Texture2D → Sprite 缓存，避免每帧分配</summary>
		private Dictionary<Texture2D, Sprite> m_SpriteCache;

		/// <summary>单元格内容水平内边距（单侧），由 XericUIActionTable 设置</summary>
		public float CellPadding;
		private float CellPadding2 => CellPadding * 2f;

		/// <summary>表格全局配置资源，由 XericUIActionTable 设置</summary>
		public XTableConfig Config;

        #endregion

        #region 公开属性

        public CellAssembler Assembler => m_Assembler;
        public GameObject BackgroundGo => m_BackgroundGo;
        public Transform Parent => m_Parent;

        /// <summary>起始行偏移（Viewport 渲染器通常为 0；Content 渲染器通常为 freezeRowCount）</summary>
        public int StartRow;
        /// <summary>起始列偏移</summary>
        public int StartCol;
        /// <summary>终止行（不含），0 表示不受限</summary>
        public int EndRow;
        /// <summary>终止列（不含），0 表示不受限</summary>
        public int EndCol;

        #endregion

        #region 样式委托

        /// <summary>从样式表读取颜色值</summary>
        public System.Func<string, string, Color, Color> GetStyleColor;
        /// <summary>从样式表读取浮点值</summary>
        public System.Func<string, string, float, float> GetStyleFloat;
        /// <summary>从样式表读取整数值</summary>
        public System.Func<string, string, int, int> GetStyleInt;
        /// <summary>从样式表读取 TMP_FontAsset</summary>
        public System.Func<string, string, TMP_FontAsset> GetStyleFontAsset;

        /// <summary>获取行高数组</summary>
        public System.Func<float[]> GetRowHeights;
        /// <summary>获取列宽数组</summary>
        public System.Func<float[]> GetColWidths;
        /// <summary>获取表格数据</summary>
        public System.Func<XTableData> GetTableData;
        /// <summary>默认样式命名空间</summary>
        public System.Func<string> GetDefaultStyleNamespace;
        /// <summary>判断单元格是否在多选范围内</summary>
        public System.Func<int, int, bool> IsInSelectionRange;
        /// <summary>获取选中的起始行列</summary>
        public System.Func<int> GetSelectedRow;
        /// <summary>获取选中的起始列</summary>
        public System.Func<int> GetSelectedCol;

        /// <summary>运行时样式变更回调</summary>
        public event System.Action OnStyleChangeDetected;

        #endregion

        #region 构造 / 销毁

        public TableRenderer(Transform parent, string defaultStyleNamespace = null,
            System.Action styleChangeHandler = null)
        {
            m_Parent = parent;
            m_Assembler = new CellAssembler(parent, defaultStyleNamespace);
            if (styleChangeHandler != null)
                m_Assembler.OnStyleChangeDetected += styleChangeHandler;
        }

        /// <summary>彻底销毁渲染器中所有动态生成的对象</summary>
        public void DestroyAll()
        {
            m_Assembler?.DestroyAll();

            if (m_BackgroundGo != null)
            {
                if (Application.isPlaying)
                    Object.Destroy(m_BackgroundGo);
                else
                    Object.DestroyImmediate(m_BackgroundGo);
                m_BackgroundGo = null;
            }
        }

        #endregion

        #region 背景

        /// <summary>创建或获取背景 Image（锚定左上角，覆盖整个渲染范围）</summary>
        public void CreateBackground(float totalWidth, float totalHeight, string styleNamespace)
        {
            if (m_BackgroundGo != null)
            {
                var rt = m_BackgroundGo.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(totalWidth, totalHeight);
                return;
            }

            m_BackgroundGo = new GameObject("_TableBackground",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            m_BackgroundGo.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;
            m_BackgroundGo.transform.SetParent(m_Parent, false);
            m_BackgroundGo.transform.SetAsFirstSibling();

            RectTransform bgRt = m_BackgroundGo.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0, 1);
            bgRt.anchorMax = new Vector2(0, 1);
            bgRt.pivot = new Vector2(0, 1);
            bgRt.anchoredPosition = Vector2.zero;
            bgRt.sizeDelta = new Vector2(totalWidth, totalHeight);

            var bgImg = m_BackgroundGo.GetComponent<Image>();
            bgImg.color = GetStyleColor?.Invoke(styleNamespace,
                XericUIActionTable.STYLE_CELL_BG_COLOR,
                XericUIActionTable.BG_COLOR_FALLBACK)
                ?? XericUIActionTable.BG_COLOR_FALLBACK;
            bgImg.raycastTarget = false;
        }

        #endregion

        #region 单元格渲染

        /// <summary>回收所有池对象</summary>
        public void ReturnAll() => m_Assembler.ReturnAll();

        /// <summary>
        /// 渲染指定范围内的单元格。
        /// 行范围为 [sr, er)，列范围为 [sc, ec)。
        /// </summary>
        public void RenderCells(int sr, int er, int sc, int ec, bool isEditor)
        {
            float[] rowHeights = GetRowHeights?.Invoke();
            float[] colWidths = GetColWidths?.Invoke();
            XTableData tableData = GetTableData?.Invoke();
            string defaultNs = GetDefaultStyleNamespace?.Invoke() ?? "xeric_table_default";

            if (rowHeights == null || colWidths == null) return;
            if (rowHeights.Length == 0 || colWidths.Length == 0) return;

            // 应用渲染范围限制
            int renderStartRow = StartRow;
            int renderEndRow = EndRow > 0 ? Mathf.Min(EndRow, rowHeights.Length) : rowHeights.Length;
            int renderStartCol = StartCol;
            int renderEndCol = EndCol > 0 ? Mathf.Min(EndCol, colWidths.Length) : colWidths.Length;

            sr = Mathf.Max(sr, renderStartRow);
            er = Mathf.Min(er, renderEndRow);
            sc = Mathf.Max(sc, renderStartCol);
            ec = Mathf.Min(ec, renderEndCol);

            if (sr >= er || sc >= ec) return;

            if (isEditor)
            {
                RenderCellsEditor(sr, er, sc, ec, rowHeights, colWidths, tableData, defaultNs);
            }
            else
            {
                RenderCellsRuntime(sr, er, sc, ec, rowHeights, colWidths, tableData, defaultNs);
            }
        }

        private void RenderCellsEditor(int sr, int er, int sc, int ec,
            float[] rowHeights, float[] colWidths, XTableData tableData, string defaultNs)
        {
            Color selectionColor = GetStyleColor(defaultNs, XericUIActionTable.STYLE_CELL_SELECTION_BG_COLOR,
                XericUIActionTable.SELECTION_COLOR_FALLBACK);
            Color borderColor = GetStyleColor(defaultNs, XericUIActionTable.STYLE_CELL_BORDER_BOTTOM_COLOR,
                XericUIActionTable.BORDER_COLOR_FALLBACK);
            float borderWidth = GetStyleFloat(defaultNs, XericUIActionTable.STYLE_CELL_BORDER_BOTTOM_WIDTH,
                XericUIActionTable.BORDER_WIDTH_FALLBACK);

            Color multiSelectColor = new Color(selectionColor.r, selectionColor.g, selectionColor.b, selectionColor.a * Config.MultiSelectAlphaFactor);
            Color focusColor = new Color(selectionColor.r + Config.FocusRAdd, selectionColor.g + Config.FocusGAdd, selectionColor.b + Config.FocusBAdd, selectionColor.a * Config.FocusAlphaMul);

            int selRow = GetSelectedRow();
            int selCol = GetSelectedCol();

            for (int r = sr; r < er && r < rowHeights.Length; r++)
            {
                for (int c = sc; c < ec && c < colWidths.Length; c++)
                {
                    if (tableData != null && tableData.IsCellMerged(r, c)) continue;

                    float cellX = CalcColLeftX(c, colWidths);
                    float cellY = -CalcRowTopY(r, rowHeights);
                    float cellW = colWidths[c];
                    float cellH = rowHeights[r];

                    if (tableData != null && tableData.IsMergeSource(r, c, out int mergeRS, out int mergeCS))
                    {
                        for (int i = 1; i < mergeCS && c + i < colWidths.Length; i++)
                            cellW += colWidths[c + i];
                        for (int i = 1; i < mergeRS && r + i < rowHeights.Length; i++)
                            cellH += rowHeights[r + i];
                    }

                    XTableCellData data = tableData?.GetCell(r, c);
                    string cellNS = (data != null && !string.IsNullOrEmpty(data.StyleNamespace))
                        ? data.StyleNamespace : defaultNs;

                    // 选择高亮
                    if (IsInSelectionRange != null && IsInSelectionRange(r, c))
                    {
                        var selImg = m_Assembler.GetImage("cell_multiselect");
                        if (selImg != null)
                        {
                            selImg.Rect.anchoredPosition = new Vector2(cellX, cellY);
                            selImg.Rect.sizeDelta = new Vector2(cellW, cellH);
                            selImg.Image.color = (r == selRow && c == selCol) ? focusColor : multiSelectColor;
                        }
                    }
                    else if (r == selRow && c == selCol)
                    {
                        var selImg = m_Assembler.GetImage("cell_select");
                        if (selImg != null)
                        {
                            selImg.Rect.anchoredPosition = new Vector2(cellX, cellY);
                            selImg.Rect.sizeDelta = new Vector2(cellW, cellH);
                            selImg.Image.color = selectionColor;
                        }
                    }

                    // 网格线
                    var hLine = m_Assembler.GetImage("cell_hline");
                    if (hLine != null)
                    {
                        hLine.Rect.anchoredPosition = new Vector2(cellX, cellY - cellH);
                        hLine.Rect.sizeDelta = new Vector2(cellW, borderWidth);
                        hLine.Image.color = borderColor;
                    }

                    var vLine = m_Assembler.GetImage("cell_vline");
                    if (vLine != null)
                    {
                        vLine.Rect.anchoredPosition = new Vector2(cellX + cellW - borderWidth, cellY);
                        vLine.Rect.sizeDelta = new Vector2(borderWidth, cellH);
                        vLine.Image.color = borderColor;
                    }

                    if (data == null) continue;

                    switch (data.ContentType)
                    {
                        case CellContentType.Text:
                            if (!string.IsNullOrEmpty(data.Text))
                            {
                                var txt = m_Assembler.GetText("cell_text", cellNS);
                                if (txt != null)
                                {
                                    txt.Rect.anchoredPosition = new Vector2(cellX + CellPadding, cellY);
                                    txt.Rect.sizeDelta = new Vector2(cellW - CellPadding2, cellH);
                                    txt.Text.SetText(data.Text);
                                    txt.Text.color = GetStyleColor(cellNS, XericUIActionTable.STYLE_CELL_FG_COLOR,
                                        XericUIActionTable.FG_COLOR_FALLBACK);
                                    txt.Text.fontSize = GetStyleInt(cellNS, XericUIActionTable.STYLE_CELL_FONT_SIZE,
                                        XericUIActionTable.FONT_SIZE_FALLBACK);
                                    txt.Text.alignment = TextAlignmentOptions.Midline;
                                    txt.Text.font = GetStyleFontAsset?.Invoke(cellNS,
                                        XericUIActionTable.STYLE_CELL_FONT_ASSET) ?? txt.Text.font;
                                }
                            }
                            break;
                        case CellContentType.Image:
                            if (data.Image != null)
                            {
                                Sprite sprite = GetOrCreateSprite(data.Image);
                                var img = m_Assembler.GetImage("cell_image", cellNS);
                                if (img != null)
                                {
                                    img.Rect.anchoredPosition = new Vector2(cellX + CellPadding, cellY);
                                    img.Rect.sizeDelta = new Vector2(cellW - CellPadding2, cellH);
                                    img.Image.sprite = sprite;
                                }
                            }
                            break;
                        case CellContentType.Prefab:
                            if (data.Prefab != null && PrefabPool != null)
                            {
                                GameObject instance = PrefabPool.Acquire(data.Prefab, r, c);
                                if (instance != null)
                                {
                                    RectTransform prt = instance.GetComponent<RectTransform>();
                                    prt.anchoredPosition = new Vector2(cellX, cellY);
                                    prt.sizeDelta = new Vector2(cellW, cellH);
                                    if (ActivePrefabMap != null)
                                        ActivePrefabMap[(r, c)] = instance;
                                    data.OnPrefabAcquire?.Invoke(instance);
                                }
                            }
                            break;
                    }
                }
            }
        }

        private void RenderCellsRuntime(int sr, int er, int sc, int ec,
            float[] rowHeights, float[] colWidths, XTableData tableData, string defaultNs)
        {
            Color rtSelectionColor = Config.RuntimeSelectionColor;
            Color rtMultiSelectColor = Config.RuntimeMultiSelectColor;
            Color rtFocusColor = Config.RuntimeFocusColor;

            int selRow = GetSelectedRow();
            int selCol = GetSelectedCol();

            for (int r = sr; r < er && r < rowHeights.Length; r++)
            {
                for (int c = sc; c < ec && c < colWidths.Length; c++)
                {
                    if (tableData != null && tableData.IsCellMerged(r, c)) continue;

                    float cellX = CalcColLeftX(c, colWidths);
                    float cellY = -CalcRowTopY(r, rowHeights);
                    float cellW = colWidths[c];
                    float cellH = rowHeights[r];

                    if (tableData != null && tableData.IsMergeSource(r, c, out int mergeRS, out int mergeCS))
                    {
                        for (int i = 1; i < mergeCS && c + i < colWidths.Length; i++)
                            cellW += colWidths[c + i];
                        for (int i = 1; i < mergeRS && r + i < rowHeights.Length; i++)
                            cellH += rowHeights[r + i];
                    }

                    XTableCellData data = tableData?.GetCell(r, c);
                    string cellNS = (data != null && !string.IsNullOrEmpty(data.StyleNamespace))
                        ? data.StyleNamespace : defaultNs;

                    if (IsInSelectionRange != null && IsInSelectionRange(r, c))
                    {
                        var selImg = m_Assembler.GetImage("cell_multiselect");
                        if (selImg != null)
                        {
                            selImg.Rect.anchoredPosition = new Vector2(cellX, cellY);
                            selImg.Rect.sizeDelta = new Vector2(cellW, cellH);
                            selImg.Image.color = (r == selRow && c == selCol) ? rtFocusColor : rtMultiSelectColor;
                        }
                    }
                    else if (r == selRow && c == selCol)
                    {
                        var selImg = m_Assembler.GetImage("cell_select");
                        if (selImg != null)
                        {
                            selImg.Rect.anchoredPosition = new Vector2(cellX, cellY);
                            selImg.Rect.sizeDelta = new Vector2(cellW, cellH);
                            selImg.Image.color = rtSelectionColor;
                        }
                    }

                    var hLine = m_Assembler.GetImage("cell_hline");
                    if (hLine != null)
                    {
                        hLine.Rect.anchoredPosition = new Vector2(cellX, cellY - cellH);
                        hLine.Rect.sizeDelta = new Vector2(cellW, Config.RuntimeBorderWidth);
                        hLine.Image.color = Config.RuntimeBorderColor;
                    }

                    var vLine = m_Assembler.GetImage("cell_vline");
                    if (vLine != null)
                    {
                        vLine.Rect.anchoredPosition = new Vector2(cellX + cellW - Config.RuntimeBorderWidth, cellY);
                        vLine.Rect.sizeDelta = new Vector2(Config.RuntimeBorderWidth, cellH);
                        vLine.Image.color = Config.RuntimeBorderColor;
                    }

                    if (data == null) continue;

                    switch (data.ContentType)
                    {
                        case CellContentType.Text:
                            if (!string.IsNullOrEmpty(data.Text))
                            {
                                var txt = m_Assembler.GetText("cell_text", cellNS);
                                if (txt != null)
                                {
                                    txt.Rect.anchoredPosition = new Vector2(cellX + CellPadding, cellY);
                                    txt.Rect.sizeDelta = new Vector2(cellW - CellPadding2, cellH);
                                    txt.Text.SetText(data.Text);
                                }
                            }
                            break;
                        case CellContentType.Image:
                            if (data.Image != null)
                            {
                                Sprite sprite = GetOrCreateSprite(data.Image);
                                var img = m_Assembler.GetImage("cell_image", cellNS);
                                if (img != null)
                                {
                                    img.Rect.anchoredPosition = new Vector2(cellX + CellPadding, cellY);
                                    img.Rect.sizeDelta = new Vector2(cellW - CellPadding2, cellH);
                                    img.Image.sprite = sprite;
                                }
                            }
                            break;
                        case CellContentType.Prefab:
                            if (data.Prefab != null && PrefabPool != null)
                            {
                                GameObject instance = PrefabPool.Acquire(data.Prefab, r, c);
                                if (instance != null)
                                {
                                    RectTransform prt = instance.GetComponent<RectTransform>();
                                    prt.anchoredPosition = new Vector2(cellX, cellY);
                                    prt.sizeDelta = new Vector2(cellW, cellH);
                                    if (ActivePrefabMap != null)
                                        ActivePrefabMap[(r, c)] = instance;
                                    data.OnPrefabAcquire?.Invoke(instance);
                                }
                            }
                            break;
                    }
                }
            }
        }

        #endregion

        #region 辅助方法

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

        #endregion

        #region 坐标计算

        private float CalcRowTopY(int row, float[] rowHeights)
        {
            float y = 0;
            for (int i = 0; i < row && i < rowHeights.Length; i++)
                y += rowHeights[i];
            return y;
        }

        private float CalcColLeftX(int col, float[] colWidths)
        {
            float x = 0;
            for (int i = 0; i < col && i < colWidths.Length; i++)
                x += colWidths[i];
            return x;
        }

        #endregion
    }
}
