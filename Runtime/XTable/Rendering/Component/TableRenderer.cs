using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using XericUI.XTable.Core;
using XericUI.XTable.Rendering.Elements;

namespace XericUI.XTable.Rendering.Component
{
    /// <summary>指定父级和索引范围的表格区域渲染器。池由表格全局持有，区域仅持有可见对象追踪。</summary>
    public sealed class TableRenderer
    {
        private struct Tracked
        {
            public CellAssembler.PooledText Text;
            public CellAssembler.PooledImage Image;
            public CellAssembler.PooledImage Selection;
        }

        private readonly Transform m_Parent;
        private readonly Dictionary<(int, int), Tracked> m_Tracked = new();
        private readonly HashSet<(int, int)> m_Rendered = new();
        private readonly Dictionary<Texture2D, Sprite> m_SpriteCache = new();
        private GameObject m_BackgroundGo;

        public CellAssembler Assembler;
        public PrefabPrototypePool PrefabPool;
        public Dictionary<(int, int), GameObject> ActivePrefabMap;
        public float CellPadding;
        public XTableConfig Config;

        public int StartRow;
        public int StartCol;
        public int EndRow;
        public int EndCol;

        public System.Func<string, string, Color, Color> GetStyleColor;
        public System.Func<string, string, int, int> GetStyleInt;
        public System.Func<string, string, TMP_FontAsset> GetStyleFontAsset;
        public System.Func<float[]> GetRowHeights;
        public System.Func<float[]> GetColWidths;
        public System.Func<XTableData> GetTableData;
        public System.Func<string> GetDefaultStyleNamespace;
        public System.Func<int, int, bool> IsInSelectionRange;
        public System.Func<int> GetSelectedRow;
        public System.Func<int> GetSelectedCol;

        public Transform Parent => m_Parent;
        public GameObject BackgroundGo => m_BackgroundGo;

        public TableRenderer(Transform parent)
        {
            m_Parent = parent;
        }

        public void DestroyAll()
        {
            ReleaseAll();
            if (m_BackgroundGo == null) return;
            if (Application.isPlaying) Object.Destroy(m_BackgroundGo);
            else Object.DestroyImmediate(m_BackgroundGo);
            m_BackgroundGo = null;
        }

        public void CreateBackground(float width, float height, string styleNamespace, Vector2 position = default)
        {
            if (m_BackgroundGo == null)
            {
                m_BackgroundGo = new GameObject("_TableBackground", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                m_BackgroundGo.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;
                m_BackgroundGo.transform.SetParent(m_Parent, false);
                m_BackgroundGo.transform.SetAsFirstSibling();
                var image = m_BackgroundGo.GetComponent<Image>();
                image.raycastTarget = false;
                image.color = GetStyleColor?.Invoke(styleNamespace, XericUIActionTable.STYLE_CELL_BG_COLOR,
                    XericUIActionTable.BG_COLOR_FALLBACK) ?? XericUIActionTable.BG_COLOR_FALLBACK;
            }
            var rt = m_BackgroundGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = position;
            rt.sizeDelta = new Vector2(width, height);
        }

        public void Render(int sr, int er, int sc, int ec, bool editorMode, bool fullRefresh)
        {
            var rows = GetRowHeights?.Invoke();
            var cols = GetColWidths?.Invoke();
            if (Assembler == null || rows == null || cols == null) return;
            sr = Mathf.Max(sr, StartRow);
            sc = Mathf.Max(sc, StartCol);
            er = Mathf.Min(er, EndRow > 0 ? EndRow : rows.Length);
            ec = Mathf.Min(ec, EndCol > 0 ? EndCol : cols.Length);
            if (sr >= er || sc >= ec)
            {
                if (fullRefresh) ReleaseAll();
                return;
            }

            var target = new HashSet<(int, int)>();
            for (int r = sr; r < er; r++)
            for (int c = sc; c < ec; c++)
                target.Add((r, c));

            if (fullRefresh)
                ReleaseAll();
            else
                ReleaseExcept(target);

            foreach (var cell in target)
                if (fullRefresh || !m_Rendered.Contains(cell))
                    RenderCell(cell.Item1, cell.Item2, rows, cols, editorMode);

            m_Rendered.Clear();
            foreach (var cell in target) m_Rendered.Add(cell);
        }

        public void ReleaseAll()
        {
            foreach (var pair in m_Tracked)
                ReleaseTracked(pair.Value);
            m_Tracked.Clear();
            m_Rendered.Clear();
            if (ActivePrefabMap == null) return;
            var keys = new List<(int, int)>();
            foreach (var pair in ActivePrefabMap) keys.Add(pair.Key);
            foreach (var key in keys) ActivePrefabMap.Remove(key);
        }

        private void ReleaseExcept(HashSet<(int, int)> target)
        {
            var remove = new List<(int, int)>();
            foreach (var pair in m_Tracked)
                if (!target.Contains(pair.Key)) remove.Add(pair.Key);
            foreach (var key in remove)
            {
                ReleaseTracked(m_Tracked[key]);
                m_Tracked.Remove(key);
            }
        }

        private void ReleaseTracked(Tracked tracked)
        {
            Assembler.ReleaseText(tracked.Text);
            Assembler.ReleaseImage(tracked.Image);
            Assembler.ReleaseImage(tracked.Selection);
        }

        private void RenderCell(int row, int col, float[] rows, float[] cols, bool editorMode)
        {
            var data = GetTableData?.Invoke();
            if (data != null && data.IsCellMerged(row, col)) return;
            string defaultNs = GetDefaultStyleNamespace?.Invoke() ?? "xeric_table_default";
            string ns = data?.GetCell(row, col)?.GetEffectiveStyleNamespace(defaultNs) ?? defaultNs;
            float x = CalcColLeftX(col, cols);
            float y = -CalcRowTopY(row, rows);
            float width = cols[col];
            float height = rows[row];
            if (data != null && data.IsMergeSource(row, col, out int mergeRows, out int mergeCols))
            {
                for (int i = 1; i < mergeCols && col + i < cols.Length; i++) width += cols[col + i];
                for (int i = 1; i < mergeRows && row + i < rows.Length; i++) height += rows[row + i];
            }

            int cellId = row * cols.Length + col;
            var tracked = new Tracked();
            bool selected = IsInSelectionRange?.Invoke(row, col) == true ||
                (row == GetSelectedRow?.Invoke() && col == GetSelectedCol?.Invoke());
            if (selected)
            {
                var selection = Assembler.GetImage(cellId, "cell_selection", ns, m_Parent);
                if (selection != null)
                {
                    selection.Rect.anchoredPosition = new Vector2(x, y);
                    selection.Rect.sizeDelta = new Vector2(width, height);
                    selection.Image.color = editorMode
                        ? GetStyleColor(ns, XericUIActionTable.STYLE_CELL_SELECTION_BG_COLOR, XericUIActionTable.SELECTION_COLOR_FALLBACK)
                        : Config.RuntimeSelectionColor;
                    tracked.Selection = selection;
                }
            }

            var cell = data?.GetCell(row, col);
            if (cell != null)
            {
                if (cell.ContentType == CellContentType.Text && !string.IsNullOrEmpty(cell.Text))
                {
                    var text = Assembler.GetText(cellId, "cell_text", ns, m_Parent);
                    if (text != null)
                    {
                        text.Rect.anchoredPosition = new Vector2(x + CellPadding, y);
                        text.Rect.sizeDelta = new Vector2(width - CellPadding * 2f, height);
                        text.Text.SetText(cell.Text);
                        if (editorMode)
                        {
                            text.Text.color = GetStyleColor(ns, XericUIActionTable.STYLE_CELL_FG_COLOR, XericUIActionTable.FG_COLOR_FALLBACK);
                            text.Text.fontSize = GetStyleInt(ns, XericUIActionTable.STYLE_CELL_FONT_SIZE, XericUIActionTable.FONT_SIZE_FALLBACK);
                            text.Text.font = GetStyleFontAsset?.Invoke(ns, XericUIActionTable.STYLE_CELL_FONT_ASSET) ?? text.Text.font;
                        }
                        tracked.Text = text;
                    }
                }
                else if (cell.ContentType == CellContentType.Image && cell.Image != null)
                {
                    var image = Assembler.GetImage(cellId, "cell_image", ns, m_Parent);
                    if (image != null)
                    {
                        image.Rect.anchoredPosition = new Vector2(x + CellPadding, y);
                        image.Rect.sizeDelta = new Vector2(width - CellPadding * 2f, height);
                        image.Image.sprite = GetSprite(cell.Image);
                        tracked.Image = image;
                    }
                }
                else if (cell.ContentType == CellContentType.Prefab && cell.Prefab != null && PrefabPool != null)
                {
                    var prefab = PrefabPool.Acquire(cell.Prefab, cellId, m_Parent);
                    if (prefab != null)
                    {
                        var rt = prefab.GetComponent<RectTransform>();
                        rt.anchoredPosition = new Vector2(x, y);
                        rt.sizeDelta = new Vector2(width, height);
                        if (ActivePrefabMap != null)
                            ActivePrefabMap[(row, col)] = prefab;
                        cell.OnPrefabAcquire?.Invoke(prefab);
                    }
                }
            }
            m_Tracked[(row, col)] = tracked;
        }

        private Sprite GetSprite(Texture2D texture)
        {
            if (!m_SpriteCache.TryGetValue(texture, out var sprite))
            {
                sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f));
                m_SpriteCache.Add(texture, sprite);
            }
            return sprite;
        }

        private static float CalcRowTopY(int row, float[] values)
        {
            float result = 0;
            for (int i = 0; i < row; i++) result += values[i];
            return result;
        }

        private static float CalcColLeftX(int col, float[] values)
        {
            float result = 0;
            for (int i = 0; i < col; i++) result += values[i];
            return result;
        }
    }
}
