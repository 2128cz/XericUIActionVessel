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
        }

        private readonly Transform m_Parent;
        private readonly Dictionary<(int, int), Tracked> m_Tracked = new();
        private readonly HashSet<(int, int)> m_Rendered = new();
        private readonly HashSet<(int, int)> m_Target = new();
        private readonly Dictionary<Texture2D, Sprite> m_SpriteCache = new();
        private GameObject m_BackgroundGo;

        public CellAssembler Assembler;
        public PrefabPrototypePool PrefabPool;
        public Dictionary<(int, int), GameObject> ActivePrefabMap;
        public float CellPadding;
        public XTableConfig Config;
        public XericUIActionTable SelectionTable;

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
            if (m_BackgroundGo == null && m_Parent != null)
            {
                // 背景缓存是普通字段，域重载后为 null；容器里 DontSave 的 _TableBackground 还活着。
                // 三个冻结渲染器各有自己的容器，按名字收养是安全的；不收养每次重载都会多叠一层。
                var existing = m_Parent.Find("_TableBackground");
                if (existing != null) m_BackgroundGo = existing.gameObject;
            }
            if (m_BackgroundGo == null)
            {
                m_BackgroundGo = new GameObject("_TableBackground", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(XTableSelectionInput));
                // NotEditable：编辑期改它不会被记成预制体覆盖。
                m_BackgroundGo.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy | HideFlags.NotEditable;
                m_BackgroundGo.transform.SetParent(m_Parent, false);
                m_BackgroundGo.transform.SetAsFirstSibling();
            }
            // 收养来的对象也要重新配置一次，颜色/选择输入不能沿用上一次的。
            var image = m_BackgroundGo.GetComponent<Image>();
            if (image != null)
            {
                image.raycastTarget = true;
                image.color = GetStyleColor?.Invoke(styleNamespace, XericUIActionTable.STYLE_CELL_BG_COLOR,
                    XericUIActionTable.BG_COLOR_FALLBACK) ?? XericUIActionTable.BG_COLOR_FALLBACK;
            }
            var selectionInput = m_BackgroundGo.GetComponent<XTableSelectionInput>();
            if (selectionInput != null) selectionInput.Configure(SelectionTable);

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

            m_Target.Clear();
            if (sr >= er || sc >= ec)
            {
                ReleaseAll();
                return;
            }
            for (int r = sr; r < er; r++)
            for (int c = sc; c < ec; c++)
                m_Target.Add((r, c));

            foreach (var cell in m_Rendered)
                if (!m_Target.Contains(cell))
                    ReleaseCell(cell.Item1, cell.Item2);

            foreach (var cell in m_Target)
                if (fullRefresh || !m_Rendered.Contains(cell))
                    RenderCell(cell.Item1, cell.Item2, rows, cols, editorMode);

            m_Rendered.Clear();
            foreach (var cell in m_Target) m_Rendered.Add(cell);
        }

        public void ReleaseAll()
        {
            foreach (var cell in m_Rendered)
                ReleaseCell(cell.Item1, cell.Item2);
            m_Tracked.Clear();
            m_Rendered.Clear();
            m_Target.Clear();
        }

        private void ReleaseCell(int row, int col)
        {
            var key = (row, col);
            if (m_Tracked.TryGetValue(key, out var tracked))
            {
                ReleaseTracked(tracked);
                m_Tracked.Remove(key);
            }
            if (ActivePrefabMap != null && ActivePrefabMap.TryGetValue(key, out var instance))
            {
                PrefabPool?.Release(instance, GetTableData?.Invoke()?.GetCell(row, col)?.OnPrefabRelease);
                ActivePrefabMap.Remove(key);
            }
        }

        private void ReleaseTracked(Tracked tracked)
        {
            Assembler.ReleaseText(tracked.Text);
            Assembler.ReleaseImage(tracked.Image);
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
            var key = (row, col);
            m_Tracked.TryGetValue(key, out var previousTracked);
            var tracked = new Tracked();
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
                    key = (row, col);
                    GameObject activePrefab = null;
                    bool wasActive = ActivePrefabMap != null
                        && ActivePrefabMap.TryGetValue(key, out activePrefab);
                    if (wasActive && !PrefabPool.IsInstanceOfPrefab(activePrefab, cell.Prefab))
                    {
                        PrefabPool.Release(activePrefab, cell.OnPrefabRelease);
                        ActivePrefabMap.Remove(key);
                        wasActive = false;
                    }
                    var prefab = wasActive ? activePrefab : PrefabPool.Acquire(cell.Prefab, cellId, m_Parent);
                    if (prefab != null)
                    {
                        var rt = prefab.GetComponent<RectTransform>();
                        rt.anchoredPosition = new Vector2(x, y);
                        rt.sizeDelta = new Vector2(width, height);
                        if (ActivePrefabMap != null)
                            ActivePrefabMap[key] = prefab;
                        if (!wasActive)
                            cell.OnPrefabAcquire?.Invoke(prefab);
                    }
                }
            }
            if (previousTracked.Text != null && previousTracked.Text != tracked.Text)
                Assembler.ReleaseText(previousTracked.Text);
            if (previousTracked.Image != null && previousTracked.Image != tracked.Image)
                Assembler.ReleaseImage(previousTracked.Image);

            if (tracked.Text != null || tracked.Image != null)
                m_Tracked[key] = tracked;
            else
                m_Tracked.Remove(key);
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
