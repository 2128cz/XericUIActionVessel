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
        /// <summary>
        /// 设置冻结行列数。
        /// 行为：普通调用立即重建冻结视图；CheckUpdate 内延后到提交时执行。
        /// 建议：与行列结构调整一起修改时放入同一 CheckUpdate。
        /// </summary>
        public void SetFreezeCount(int rows, int cols)
        {
            if (TryEnqueueUpdateCommand(new UpdateCommand(UpdateCommandType.SetFreezeCount, rows, cols))) return;
            SetFreezeCountImmediate(rows, cols);
        }

        private void SetFreezeCountImmediate(int rows, int cols, bool refresh = true)
        {
            m_FreezeRowCount = Mathf.Max(0, rows);
            m_FreezeColCount = Mathf.Max(0, cols);
            CreateViewportRenderer();
            MarkDirty(TableDirtyType.LayoutChanged);
            if (refresh && !IsCheckingUpdate)
                RefreshView();
        }

        /// <summary>
        /// 替换单元格完整数据。
        /// 行为：普通调用立即替换；CheckUpdate 内记录引用并在提交时写入。
        /// 建议：批量设置时确保传入的数据对象在 Scope 结束前仍有效，且不要在 Scope 内依赖其已写入表格。
        /// </summary>
        public void SetCellData(int row, int col, XTableCellData data)
        {
            if (TryEnqueueUpdateCommand(new UpdateCommand(UpdateCommandType.SetCellData, row, col, cellData: data))) return;
            SetCellDataImmediate(row, col, data);
        }

        private void SetCellDataImmediate(int row, int col, XTableCellData data)
        {
            TableData.SetCell(row, col, data);
            MarkDirty(TableDirtyType.DataChanged);
        }

        /// <summary>
        /// 设置文本内容，并将类型设为 Text。
        /// 行为：普通调用立即创建或更新并返回单元格；CheckUpdate 内仅入队，返回 <c>null</c>。
        /// 建议：Scope 内不要通过返回值继续配置单元格；需完整配置时使用 SetCellData。
        /// </summary>
        public XTableCellData SetCellText(int row, int col, string text)
        {
            if (TryEnqueueUpdateCommand(new UpdateCommand(UpdateCommandType.SetCellText, row, col, stringValue1: text))) return null;
            return SetCellTextImmediate(row, col, text);
        }

        private XTableCellData SetCellTextImmediate(int row, int col, string text)
        {
            var data = TableData.GetOrCreateCell(row, col);
            data.Text = text;
            data.ContentType = CellContentType.Text;
            MarkDirty(TableDirtyType.DataChanged);
            return data;
        }

        /// <summary>
        /// 设置图片纹理，并将类型设为 Image。
        /// 行为：普通调用立即创建或更新；CheckUpdate 内仅入队，返回 <c>null</c>。
        /// 建议：连续设置多格图片时使用 CheckUpdate，避免每格分别刷新。
        /// </summary>
        public XTableCellData SetCellImage(int row, int col, Texture2D image)
        {
            if (TryEnqueueUpdateCommand(new UpdateCommand(UpdateCommandType.SetCellImage, row, col, texture: image))) return null;
            return SetCellImageImmediate(row, col, image);
        }

        private XTableCellData SetCellImageImmediate(int row, int col, Texture2D image)
        {
            var data = TableData.GetOrCreateCell(row, col);
            data.Image = image;
            data.ContentType = CellContentType.Image;
            MarkDirty(TableDirtyType.DataChanged);
            return data;
        }

        /// <summary>
        /// 设置预制体，并将类型设为 Prefab。
        /// 行为：普通调用立即创建或更新；CheckUpdate 内仅入队，返回 <c>null</c>。
        /// 建议：需要生命周期委托时使用带 onPrefabAcquire/onPrefabRelease 参数的重载。
        /// </summary>
        public XTableCellData SetCellPrefab(int row, int col, GameObject prefab)
            => SetCellPrefab(row, col, prefab, null, null);

        /// <summary>
        /// 设置预制体及其获取、归还事件。
        /// 功能：在实例显示前调用 onPrefabAcquire，归还待命池前调用 onPrefabRelease。
        /// 行为：CheckUpdate 内委托随命令提交，首次创建单元格也安全；返回值仍为 <c>null</c>。
        /// 建议：在批量填充 Prefab 时优先使用此重载，不要依赖 SetCellPrefab 的返回数据设置委托。
        /// </summary>
        public XTableCellData SetCellPrefab(int row, int col, GameObject prefab,
            Action<GameObject> onPrefabAcquire, Action<GameObject> onPrefabRelease)
        {
            if (TryEnqueueUpdateCommand(new UpdateCommand(UpdateCommandType.SetCellPrefab, row, col, prefab: prefab,
                    onPrefabAcquire: onPrefabAcquire, onPrefabRelease: onPrefabRelease)))
                return null;
            return SetCellPrefabImmediate(row, col, prefab, onPrefabAcquire, onPrefabRelease);
        }

        private XTableCellData SetCellPrefabImmediate(int row, int col, GameObject prefab,
            Action<GameObject> onPrefabAcquire = null, Action<GameObject> onPrefabRelease = null)
        {
            var data = TableData.GetOrCreateCell(row, col);
            data.Prefab = prefab;
            data.ContentType = CellContentType.Prefab;
            data.OnPrefabAcquire = onPrefabAcquire;
            data.OnPrefabRelease = onPrefabRelease;
            MarkDirty(TableDirtyType.DataChanged);
            return data;
        }

        /// <summary>
        /// 设置单元格或合并矩形的样式，并覆写四条外边范围。
        /// 行为：普通调用立即应用；CheckUpdate 内延后执行且返回 <c>null</c>。
        /// 建议：批量样式修改应使用 CheckUpdate；Scope 内不要依赖返回的样式单元格。
        /// </summary>
        public XTableCellData SetCellStyle(int row, int col, string styleNamespace, string stylePath)
        {
            if (TryEnqueueUpdateCommand(new UpdateCommand(UpdateCommandType.SetCellStyle, row, col, stringValue1: styleNamespace, stringValue2: stylePath))) return null;
            return SetCellStyleImmediate(row, col, styleNamespace, stylePath);
        }

        private XTableCellData SetCellStyleImmediate(int row, int col, string styleNamespace, string stylePath)
        {
            ResolveStyleRect(row, col, out int rowStart, out int rowEnd, out int colStart, out int colEnd);
            XTableCellData result = null;
            for (int r = rowStart; r < rowEnd; r++)
            for (int c = colStart; c < colEnd; c++)
            {
                var data = TableData.GetOrCreateCell(r, c);
                data.CellStyle ??= new XericLibrary.Runtime.SuperStyleSheet.StyleMember();
                data.CellStyle.CurrentNamespace = styleNamespace ?? "";
                data.CellStyle.StylePath = stylePath ?? "";
                data.StyleNamespace = styleNamespace ?? "";
                result ??= data;
            }
            TableData.SetBorderRect(rowStart, rowEnd, colStart, colEnd, styleNamespace);
            MarkDirty(TableDirtyType.StyleChanged);
            return result;
        }

        /// <summary>
        /// 设置单元格的样式命名空间（兼容旧版）。
        /// 行为：普通调用立即写入；CheckUpdate 内仅记录，提交后统一刷新样式。
        /// 建议：同一批数据填充后的样式设置应放入同一 Scope。
        /// </summary>
        public void SetCellStyleNamespace(int row, int col, string styleNamespace)
        {
            if (TryEnqueueUpdateCommand(new UpdateCommand(UpdateCommandType.SetCellStyleNamespace, row, col, stringValue1: styleNamespace))) return;
            SetCellStyleNamespaceImmediate(row, col, styleNamespace);
        }

        private void SetCellStyleNamespaceImmediate(int row, int col, string styleNamespace)
        {
            ResolveStyleRect(row, col, out int rowStart, out int rowEnd, out int colStart, out int colEnd);
            for (int r = rowStart; r < rowEnd; r++)
            for (int c = colStart; c < colEnd; c++)
                TableData.GetOrCreateCell(r, c).StyleNamespace = styleNamespace ?? string.Empty;
            TableData.SetBorderRect(rowStart, rowEnd, colStart, colEnd, styleNamespace);
            MarkDirty(TableDirtyType.StyleChanged);
        }

        private void ResolveStyleRect(int row, int col, out int rowStart, out int rowEnd, out int colStart, out int colEnd)
        {
            rowStart = row; colStart = col; rowEnd = row + 1; colEnd = col + 1;
            if (TableData.TryGetMergeRect(row, col, out int sourceRow, out int sourceCol, out int rowSpan, out int colSpan))
            {
                rowStart = sourceRow; colStart = sourceCol;
                rowEnd = Mathf.Min(sourceRow + rowSpan, m_RowHeights.Length);
                colEnd = Mathf.Min(sourceCol + colSpan, m_ColWidths.Length);
            }
        }

        /// <summary>
        /// 清除文本内容。
        /// 行为：普通调用立即清空；CheckUpdate 内延后到提交时清空。
        /// 建议：与同批写入或删除操作放入同一 Scope，保持命令顺序。
        /// </summary>
        public void ClearCellText(int row, int col)
        {
            if (TryEnqueueUpdateCommand(new UpdateCommand(UpdateCommandType.ClearCellText, row, col))) return;
            ClearCellTextImmediate(row, col);
        }

        private void ClearCellTextImmediate(int row, int col)
        {
            var data = TableData.GetCell(row, col);
            if (data == null) return;
            data.Text = null;
            if (data.ContentType == CellContentType.Text)
                data.ContentType = CellContentType.Text; // 保持类型不变，仅清空内容
            MarkDirty(TableDirtyType.DataChanged);
        }

        /// <summary>
        /// 清除图片纹理。
        /// 行为：普通调用立即清空；CheckUpdate 内延后到提交时清空。
        /// 建议：连续清理图片时使用 CheckUpdate，避免重复刷新。
        /// </summary>
        public void ClearCellImage(int row, int col)
        {
            if (TryEnqueueUpdateCommand(new UpdateCommand(UpdateCommandType.ClearCellImage, row, col))) return;
            ClearCellImageImmediate(row, col);
        }

        private void ClearCellImageImmediate(int row, int col)
        {
            var data = TableData.GetCell(row, col);
            if (data == null) return;
            data.Image = null;
            MarkDirty(TableDirtyType.DataChanged);
        }

        /// <summary>
        /// 清除预制体及其生命周期委托。
        /// 行为：提交或即时刷新时，旧实例会触发 release 后归还待命池。
        /// 建议：批量移除 Prefab 时使用 CheckUpdate，以一次性回收旧实例。
        /// </summary>
        public void ClearCellPrefab(int row, int col)
        {
            if (TryEnqueueUpdateCommand(new UpdateCommand(UpdateCommandType.ClearCellPrefab, row, col))) return;
            ClearCellPrefabImmediate(row, col);
        }

        private void ClearCellPrefabImmediate(int row, int col)
        {
            var data = TableData.GetCell(row, col);
            if (data == null) return;
            data.Prefab = null;
            data.OnPrefabAcquire = null;
            data.OnPrefabRelease = null;
            MarkDirty(TableDirtyType.DataChanged);
        }

        /// <summary>
        /// 清除单元格或合并矩形的样式覆盖及外边范围。
        /// 行为：普通调用立即恢复默认样式；CheckUpdate 内在提交时统一恢复。
        /// 建议：批量重置样式时使用 CheckUpdate。
        /// </summary>
        public void ClearCellStyle(int row, int col)
        {
            if (TryEnqueueUpdateCommand(new UpdateCommand(UpdateCommandType.ClearCellStyle, row, col))) return;
            ClearCellStyleImmediate(row, col);
        }

        private void ClearCellStyleImmediate(int row, int col)
        {
            ResolveStyleRect(row, col, out int rowStart, out int rowEnd, out int colStart, out int colEnd);
            for (int r = rowStart; r < rowEnd; r++)
            for (int c = colStart; c < colEnd; c++)
            {
                var data = TableData.GetCell(r, c);
                if (data == null) continue;
                data.CellStyle = null;
                data.StyleNamespace = string.Empty;
            }
            TableData.SetBorderRect(rowStart, rowEnd, colStart, colEnd, null);
            MarkDirty(TableDirtyType.StyleChanged);
        }

        /// <summary>
        /// 获取或创建单元格数据。
        /// 行为：会立即创建数据；CheckUpdate 内禁止调用，避免绕过命令缓冲。
        /// 建议：Scope 内需要完整数据时先构造 XTableCellData，再调用 SetCellData。
        /// </summary>
        public XTableCellData GetCellData(int row, int col)
        {
            ThrowIfCheckingUpdate(nameof(GetCellData));
            return TableData.GetOrCreateCell(row, col);
        }

        /// <summary>获取单元格的样式命名空间（含回退逻辑）</summary>
        public string GetCellStyleNamespace(int row, int col)
        {
            var data = TableData.GetCell(row, col);
            return data != null ? data.GetEffectiveStyleNamespace(m_DefaultStyleNamespace) : m_DefaultStyleNamespace;
        }

        /// <summary>
        /// 合并从起点开始的矩形单元格。
        /// 行为：普通调用立即改变合并关系；CheckUpdate 内保持命令顺序，提交后再合并。
        /// 建议：与行列删除混用时必须放入同一 Scope，索引按调用顺序解释。
        /// </summary>
        public void MergeCells(int startRow, int startCol, int rowSpan, int colSpan)
        {
            if (TryEnqueueUpdateCommand(new UpdateCommand(UpdateCommandType.MergeCells, startRow, startCol, rowSpan, colSpan))) return;
            MergeCellsImmediate(startRow, startCol, rowSpan, colSpan);
        }

        private void MergeCellsImmediate(int startRow, int startCol, int rowSpan, int colSpan)
        {
            TableData.MergeCells(startRow, startCol, rowSpan, colSpan);
            MarkDirty(TableDirtyType.DataChanged);
        }

        /// <summary>
        /// 取消起点所属的合并区域。
        /// 行为：普通调用立即取消；CheckUpdate 内在提交时按前序命令后的坐标执行。
        /// 建议：批量调整合并关系时使用 CheckUpdate。
        /// </summary>
        public void UnmergeCells(int startRow, int startCol)
        {
            if (TryEnqueueUpdateCommand(new UpdateCommand(UpdateCommandType.UnmergeCells, startRow, startCol))) return;
            UnmergeCellsImmediate(startRow, startCol);
        }

        private void UnmergeCellsImmediate(int startRow, int startCol)
        {
            TableData.UnmergeCells(startRow, startCol);
            MarkDirty(TableDirtyType.DataChanged);
        }

        /// <summary>判断指定单元格是否被合并覆盖（非源，会被重定向）</summary>
        public bool IsCellMerged(int row, int col) => TableData.IsCellMerged(row, col);

        /// <summary>判断指定单元格是否为合并源（锚点），并返回合并跨度</summary>
        public bool IsMergeSource(int row, int col, out int rowSpan, out int colSpan)
            => TableData.IsMergeSource(row, col, out rowSpan, out colSpan);

        /// <summary>
        /// 在末尾追加一行，默认行高来自 Config。
        /// 行为：普通调用立即扩展行数组；CheckUpdate 内记录追加，结束时只重建一次。
        /// 建议：循环追加或随后填充新行时使用 CheckUpdate。
        /// </summary>
        public void AddRow(float height = -1f)
        {
            if (TryEnqueueUpdateCommand(new UpdateCommand(UpdateCommandType.AddRow, floatValue: height))) return;
            AddRowImmediate(height);
        }

        private void AddRowImmediate(float height)
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

        /// <summary>
        /// 在末尾追加一列，默认列宽来自 Config。
        /// 行为：普通调用立即扩展列数组；CheckUpdate 内记录追加，结束时只重建一次。
        /// 建议：循环追加或随后填充新列时使用 CheckUpdate。
        /// </summary>
        public void AddColumn(float width = -1f)
        {
            if (TryEnqueueUpdateCommand(new UpdateCommand(UpdateCommandType.AddColumn, floatValue: width))) return;
            AddColumnImmediate(width);
        }

        private void AddColumnImmediate(float width)
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

        /// <summary>
        /// 设置指定行的高度。
        /// 行为：普通调用立即更新布局；CheckUpdate 内延后，最终按命令顺序应用。
        /// 建议：批量调整行高时使用 CheckUpdate。
        /// </summary>
        public void SetRowHeight(int row, float height)
        {
            if (TryEnqueueUpdateCommand(new UpdateCommand(UpdateCommandType.SetRowHeight, row, floatValue: height))) return;
            SetRowHeightImmediate(row, height);
        }

        private void SetRowHeightImmediate(int row, float height)
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

        /// <summary>
        /// 设置指定列的宽度。
        /// 行为：普通调用立即更新布局；CheckUpdate 内延后，最终按命令顺序应用。
        /// 建议：批量调整列宽时使用 CheckUpdate。
        /// </summary>
        public void SetColWidth(int col, float width)
        {
            if (TryEnqueueUpdateCommand(new UpdateCommand(UpdateCommandType.SetColWidth, col: col, floatValue: width))) return;
            SetColWidthImmediate(col, width);
        }

        private void SetColWidthImmediate(int col, float width)
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

        /// <summary>
        /// 删除指定行，并将下方单元格数据上移。
        /// 行为：普通调用立即变更并走普通刷新；CheckUpdate 内仅记录，提交后回收旧渲染槽位并按最终状态重建。
        /// 建议：循环删除、删除后追加或混合结构操作时必须使用 CheckUpdate；重复索引按前一条删除后的行号解释。
        /// </summary>
        public void RemoveRow(int row)
        {
            if (TryEnqueueUpdateCommand(new UpdateCommand(UpdateCommandType.RemoveRow, row))) return;
            RemoveRowImmediate(row);
        }

        private void RemoveRowImmediate(int row)
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
            TableData.ClearAllBorderCache();

            // 裁剪选区到新尺寸
            ClampSelectionToBounds();

            MarkDirty(TableDirtyType.LayoutChanged | TableDirtyType.DataChanged | TableDirtyType.SelectionChanged);
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>
        /// 删除指定列，并将右侧单元格数据左移。
        /// 行为：普通调用立即变更并走普通刷新；CheckUpdate 内仅记录，提交后回收旧渲染槽位并按最终状态重建。
        /// 建议：循环删除、删除后追加或混合结构操作时必须使用 CheckUpdate；重复索引按前一条删除后的列号解释。
        /// </summary>
        public void RemoveColumn(int col)
        {
            if (TryEnqueueUpdateCommand(new UpdateCommand(UpdateCommandType.RemoveColumn, col: col))) return;
            RemoveColumnImmediate(col);
        }

        private void RemoveColumnImmediate(int col)
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
            TableData.ClearAllBorderCache();

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
            NormalizeAndSelect(row, col, row, col);
        }

        /// <summary>多选一个范围</summary>
        public void SelectCell(int startRow, int startCol, int endRow, int endCol)
        {
            NormalizeAndSelect(startRow, startCol, endRow, endCol);
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
            if (!IsCheckingUpdate)
                RefreshSelectionOverlays();
        }

        /// <summary>裁剪选区到当前行列尺寸范围内</summary>
        private void ClampSelectionToBounds()
        {
            int maxR = (m_RowHeights?.Length ?? 0) - 1;
            int maxC = (m_ColWidths?.Length ?? 0) - 1;
            SelectionHandle.ClampTo(maxR, maxC);
        }

        /// <summary>
        /// 完全重建表格，销毁动态对象并创建默认示例数据。
        /// 行为：立即销毁池与渲染器；CheckUpdate 内禁止调用。
        /// 建议：仅用于初始化或故障恢复，不要与批量业务更新混用。
        /// </summary>
        [ContextMenu("Xeric UI/完全重建表格")]
        public void FullRebuild()
        {
            ThrowIfCheckingUpdate(nameof(FullRebuild));
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

        /// <summary>
        /// 重新生成表格并回填已有文本数据。
        /// 行为：立即销毁并重建池与渲染器；CheckUpdate 内禁止调用。
        /// 建议：需要保留完整单元格数据、样式或 Prefab 时不要使用本方法。
        /// </summary>
        public void FullRebuildPreservingData()
        {
            ThrowIfCheckingUpdate(nameof(FullRebuildPreservingData));
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
        /// 清空单元格数据与合并信息，但保留行列排布和样式。
        /// 行为：立即清空并刷新；CheckUpdate 内禁止调用，因为它会替换整块数据状态。
        /// 建议：需要与其他操作合并时，逐项使用 ClearCell 系列 API。
        /// </summary>
        public void ClearData()
        {
            ThrowIfCheckingUpdate(nameof(ClearData));
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
        /// 清空表格所有内容，但不创建默认示例数据。
        /// 行为：立即销毁池与渲染器；CheckUpdate 内禁止调用。
        /// 建议：仅用于整体重置，不应与批量增删或写入操作混用。
        /// </summary>
        public void ClearTable()
        {
            ThrowIfCheckingUpdate(nameof(ClearTable));
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

        /// <summary>
        /// 强制刷新样式，不重建数据结构。
        /// 行为：普通调用立即刷新；CheckUpdate 内合并到最终刷新。
        /// 建议：批量修改样式数据后调用，通常无需单独调用多次。
        /// </summary>
        public void ForceStyleRefresh()
        {
            if (TryEnqueueUpdateCommand(new UpdateCommand(UpdateCommandType.ForceStyleRefresh))) return;
            ForceStyleRefreshImmediate();
        }

        private void ForceStyleRefreshImmediate()
        {
            StyleManager.ForceRefresh();
            MarkDirty(TableDirtyType.StyleChanged);
            if (!IsCheckingUpdate)
                RefreshView();
        }

        /// <summary>构建剪贴板数据对象（尺寸 + 单元格文本 + 合并信息）</summary>
        public XTableClipboardData BuildClipboardData()
        {
            return XTableClipboardData.FromTable(this);
        }

        /// <summary>
        /// 应用剪贴板数据，覆盖尺寸、命名空间、文本与合并信息并刷新。
        /// 行为：会整体替换多个状态；CheckUpdate 内禁止调用。
        /// 建议：先结束当前 CheckUpdate，再单独应用剪贴板数据。
        /// </summary>
        public void ApplyClipboardData(XTableClipboardData data)
        {
            ThrowIfCheckingUpdate(nameof(ApplyClipboardData));
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

            RecalculateTotalSize();
            RequestRefresh(TableDirtyType.DataChanged | TableDirtyType.LayoutChanged, true);
            SetChildrenVisible(m_ChildrenVisible);
        }
    }
}
