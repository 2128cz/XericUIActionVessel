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
    public partial class XericUIActionTable
    {
        /// <summary>设置冻结行列数并重建视图</summary>
        public void SetFreezeCount(int rows, int cols)
        {
            m_FreezeRowCount = Mathf.Max(0, rows);
            m_FreezeColCount = Mathf.Max(0, cols);
            CreateViewportRenderer();
            MarkDirty(TableDirtyType.LayoutChanged);
            RefreshView();
        }

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
            if (TableData.IsMergeSource(row, col, out int rowSpan, out int colSpan))
            {
                for (int r = row; r < row + rowSpan && r < m_RowHeights.Length; r++)
                for (int c = col; c < col + colSpan && c < m_ColWidths.Length; c++)
                    TableData.GetOrCreateCell(r, c).StyleNamespace = styleNamespace ?? string.Empty;

                ApplyCellStyleBorders(row, col, rowSpan, colSpan, styleNamespace);
            }
            else
            {
                ApplyCellStyleToCell(row, col, styleNamespace);
            }

            MarkDirty(TableDirtyType.StyleChanged);
        }

        /// <summary>设置单元格样式并写入其可见右侧、底部边框范围。</summary>
        private void ApplyCellStyleToCell(int row, int col, string ns)
        {
            var data = TableData.GetOrCreateCell(row, col);
            data.StyleNamespace = ns ?? string.Empty;

            int rowSpan = 1;
            int colSpan = 1;
            if (TableData.IsMergeSource(row, col, out int mergedRows, out int mergedCols))
            {
                rowSpan = mergedRows;
                colSpan = mergedCols;
            }
            ApplyCellStyleBorders(row, col, rowSpan, colSpan, ns);
        }

        private void ApplyCellStyleBorders(int row, int col, int rowSpan, int colSpan, string ns)
        {
            TableData.SetBorderRange(XTableBorderAxis.Vertical, col + colSpan, row, rowSpan, ns);
            TableData.SetBorderRange(XTableBorderAxis.Horizontal, row + rowSpan, col, colSpan, ns);
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
            RefreshSelectionOverlays();
        }

        /// <summary>裁剪选区到当前行列尺寸范围内</summary>
        private void ClampSelectionToBounds()
        {
            int maxR = (m_RowHeights?.Length ?? 0) - 1;
            int maxC = (m_ColWidths?.Length ?? 0) - 1;
            SelectionHandle.ClampTo(maxR, maxC);
        }

        /// <summary>完全重建表格——销毁所有动态生成的对象并重新初始化</summary>
        [ContextMenu("Xeric UI/完全重建表格")]
        public void FullRebuild()
        {
            m_IsRebuilding = true;
            try
            {
                // 清理跟踪状态（在销毁旧 Assembler 前先解除引用）
                ReleaseAllTrackedCells();
                m_ActivePrefabMap?.Clear();

                m_Assembler?.DestroyAll();
                m_PrefabPool?.DestroyAll();
                ClearAllChildren();
                m_BackgroundRenderer = null;
                m_BorderRenderer = null;

                m_TableData = new XTableData();
                m_Quadtree?.Clear();
                m_QuadtreeDirty = true;
                m_StyleNamespaceDirty = true;

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
            finally
            {
                m_IsRebuilding = false;
            }
        }

        /// <summary>重新生成表格但保留已有文本数据——先保存，重建后再回填</summary>
        public void FullRebuildPreservingData()
        {
            m_IsRebuilding = true;
            try
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

                // 先清理跟踪状态再销毁旧对象
                ReleaseAllTrackedCells();
                m_ActivePrefabMap?.Clear();

                m_Assembler?.DestroyAll();
                m_PrefabPool?.DestroyAll();
                ClearAllChildren();
                m_BackgroundRenderer = null;
                m_BorderRenderer = null;

                m_TableData = new XTableData();
                m_Quadtree?.Clear();
                m_QuadtreeDirty = true;
                m_StyleNamespaceDirty = true;

                m_Assembler = new CellAssembler(ContentTransform, m_DefaultStyleNamespace);
                m_PrefabPool = new PrefabPrototypePool(ContentTransform);
                m_ActivePrefabMap = new Dictionary<(int, int), GameObject>();
                RecalculateTotalSize();

                // 恢复文本数据到新 TableData
                foreach (var kv in savedTexts)
                {
                    int r = kv.Key.Item1, c = kv.Key.Item2;
                    if (r < (m_RowHeights?.Length ?? 0) && c < (m_ColWidths?.Length ?? 0))
                        m_TableData.GetOrCreateCell(r, c).Text = kv.Value;
                }

                // 如果没有恢复任何数据，则创建默认示例
                if (savedTexts.Count == 0)
                    CreateDefaultSampleData();

                CreateBackground();
                CreateViewportRenderer();
                MarkDirty(TableDirtyType.All);
                RefreshView();
                SetChildrenVisible(m_ChildrenVisible);
            }
            finally
            {
                m_IsRebuilding = false;
            }
        }

        /// <summary>
        /// 清空所有数据（单元格文本、合并信息），但保留行列排布和样式。
        /// 等价于"清空内容但不改变表格结构"。
        /// </summary>
        public void ClearData()
        {
            if (m_TableData != null)
            {
                // 清理预制体：先调用 OnPrefabRelease 再清理映射
                if (m_ActivePrefabMap != null)
                {
                    foreach (var kv in m_ActivePrefabMap)
                    {
                        var cellData = m_TableData.GetCell(kv.Key.Item1, kv.Key.Item2);
                        cellData?.OnPrefabRelease?.Invoke(kv.Value);
                    }
                    m_ActivePrefabMap.Clear();
                }
                m_PrefabPool?.ReturnAll();

                m_TableData.ClearAllMerges();
                m_TableData.BlockMap.Clear();
                m_StyleNamespaceDirty = true;
            }
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
            m_IsRebuilding = true;
            try
            {
                // 清理预制体回调
                if (m_ActivePrefabMap != null && m_TableData != null)
                {
                    foreach (var kv in m_ActivePrefabMap)
                    {
                        var cellData = m_TableData.GetCell(kv.Key.Item1, kv.Key.Item2);
                        cellData?.OnPrefabRelease?.Invoke(kv.Value);
                    }
                }

                ReleaseAllTrackedCells();
                m_ActivePrefabMap?.Clear();
                m_Quadtree?.Clear();

                m_Assembler?.DestroyAll();
                m_PrefabPool?.DestroyAll();
                ClearAllChildren();
                m_BackgroundRenderer = null;
                m_BorderRenderer = null;

                m_TableData = new XTableData();
                m_QuadtreeDirty = true;
                m_StyleNamespaceDirty = true;
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
            finally
            {
                m_IsRebuilding = false;
            }
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
    }
}
