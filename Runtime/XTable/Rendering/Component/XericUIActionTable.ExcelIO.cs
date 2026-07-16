using System;
using System.IO;

using OfficeOpenXml;
using OfficeOpenXml.Style;

using UnityEngine;

using XericUI.XTable.Core;

namespace XericUI.XTable.Rendering.Component
{
    /// <summary>
    /// XTable Excel 导入导出——通过 EPPlus 实现 .xlsx 文件的读写。
    /// </summary>
    public partial class XericUIActionTable
    {
        #region 导入

        /// <summary>
        /// 从 Excel .xlsx 文件加载数据到表格。
        /// 支持文本、合并单元格、列宽、行高的导入。
        /// </summary>
        /// <param name="filePath">Excel 文件路径</param>
        /// <param name="sheetIndex">工作表索引（0-based），默认 0 即第一个工作表</param>
        public void LoadFromExcelFile(string filePath, int sheetIndex = 0)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                Debug.LogWarning("[XTable] LoadFromExcelFile: 文件路径为空");
                return;
            }

            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"[XTable] LoadFromExcelFile: 文件不存在: {filePath}");
                return;
            }

            try
            {
                using (var package = new ExcelPackage(new FileInfo(filePath)))
                {
                    if (sheetIndex < 0 || sheetIndex >= package.Workbook.Worksheets.Count)
                    {
                        Debug.LogWarning($"[XTable] LoadFromExcelFile: 工作表索引 {sheetIndex} 超出范围（共 {package.Workbook.Worksheets.Count} 个工作表）");
                        return;
                    }
                    ExcelWorksheet ws = package.Workbook.Worksheets[sheetIndex];
                    if (ws == null)
                    {
                        Debug.LogWarning("[XTable] LoadFromExcelFile: 工作簿中没有工作表");
                        return;
                    }

                    // 确定行列数
                    int rows = 0, cols = 0;
                    if (ws.Dimension != null)
                    {
                        rows = ws.Dimension.Rows;
                        cols = ws.Dimension.Columns;
                    }

                    if (rows <= 0 || cols <= 0)
                    {
                        Debug.LogWarning("[XTable] LoadFromExcelFile: Excel 文件为空或无法识别行列数");
                        return;
                    }

                    // 确保数据层存在
                    if (m_TableData == null)
                        m_TableData = new XTableData();

                    // 设置行高
                    m_RowHeights = new float[rows];
                    for (int r = 0; r < rows; r++)
                    {
                        float h = (float)ws.Row(r + 1).Height;
                        m_RowHeights[r] = h > 0 ? h : Config.DefaultCellHeight;
                    }

                    // 设置列宽
                    m_ColWidths = new float[cols];
                    for (int c = 0; c < cols; c++)
                    {
                        float w = (float)ws.Column(c + 1).Width;
                        m_ColWidths[c] = w > 0 ? w : Config.DefaultCellWidth;
                    }

                    // 读取单元格文本
                    for (int r = 0; r < rows; r++)
                    {
                        for (int c = 0; c < cols; c++)
                        {
                            object val = ws.Cells[r + 1, c + 1].Value;
                            if (val != null)
                            {
                                string text = val.ToString();
                                if (!string.IsNullOrEmpty(text))
                                {
                                    var cell = m_TableData.GetOrCreateCell(r, c);
                                    cell.Text = text;
                                }
                            }
                        }
                    }

                    // 读取合并单元格
                    if (ws.MergedCells.Count > 0)
                    {
                        m_TableData.ClearAllMerges();
                        foreach (string mergeAddr in ws.MergedCells)
                        {
                            try
                            {
                                var addr = new ExcelAddressBase(mergeAddr);
                                int startR = addr.Start.Row - 1;  // 1-based → 0-based
                                int startC = addr.Start.Column - 1;
                                int endR = addr.End.Row - 1;
                                int endC = addr.End.Column - 1;
                                int rowSpan = endR - startR + 1;
                                int colSpan = endC - startC + 1;

                                if (rowSpan > 1 || colSpan > 1)
                                {
                                    if (endR < rows && endC < cols)
                                        m_TableData.MergeCells(startR, startC, rowSpan, colSpan);
                                }
                            }
                            catch
                            {
                                // 忽略无法解析的合并地址
                            }
                        }
                    }

                    // 标记脏并刷新
                    MarkDirty(TableDirtyType.DataChanged | TableDirtyType.LayoutChanged);
                    RecalculateTotalSize();
                    RefreshView();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[XTable] LoadFromExcelFile 失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        #endregion

        #region 导出

        /// <summary>
        /// 将表格数据导出为 Excel .xlsx 文件。
        /// 支持文本、合并单元格、列宽、行高、样式（颜色/字体/填充）、边框缓存的导出。
        /// </summary>
        /// <param name="filePath">Excel 文件路径</param>
        /// <param name="sheetName">工作表名称，默认 "Sheet1"</param>
        public void SaveToExcelFile(string filePath, string sheetName = "Sheet1")
        {
            if (string.IsNullOrEmpty(filePath))
            {
                Debug.LogWarning("[XTable] SaveToExcelFile: 文件路径为空");
                return;
            }

            // 确保目录存在
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            try
            {
                using (var package = new ExcelPackage())
                {
                    if (string.IsNullOrEmpty(sheetName))
                        sheetName = "Sheet1";
                    ExcelWorksheet ws = package.Workbook.Worksheets.Add(sheetName);

                    int rows = m_RowHeights?.Length ?? 0;
                    int cols = m_ColWidths?.Length ?? 0;

                    if (rows == 0 || cols == 0)
                    {
                        // 空表格：创建空文件
                        package.SaveAs(new FileInfo(filePath));
                        return;
                    }

                    // 设置列宽
                    for (int c = 0; c < cols; c++)
                        ws.Column(c + 1).Width = m_ColWidths[c];

                    // 设置行高
                    for (int r = 0; r < rows; r++)
                    {
                        float h = m_RowHeights[r];
                        if (h > 0)
                            ws.Row(r + 1).Height = h;
                    }

                    // 写入单元格数据和样式
                    for (int r = 0; r < rows; r++)
                    {
                        for (int c = 0; c < cols; c++)
                        {
                            // 跳过被合并覆盖的单元格（稍后通过合并区域统一处理）
                            if (m_TableData != null && m_TableData.IsCellMerged(r, c))
                                continue;

                            var cell = m_TableData?.GetCell(r, c);
                            if (cell == null) continue;

                            var excelCell = ws.Cells[r + 1, c + 1];

                            // 写入文本
                            if (!string.IsNullOrEmpty(cell.Text))
                                excelCell.Value = cell.Text;

                            // 应用样式
                            string ns = !string.IsNullOrEmpty(cell.StyleNamespace)
                                ? cell.StyleNamespace
                                : m_DefaultStyleNamespace;
                            if (!string.IsNullOrEmpty(ns))
                                ApplyStyleToExcelCell(excelCell, ns);
                        }
                    }

                    // 写入合并单元格
                    if (m_TableData != null)
                    {
                        WriteMergedCellsToExcel(ws, rows, cols);
                    }

                    // 写入边框缓存
                    if (m_TableData != null)
                    {
                        WriteBorderCacheToExcel(ws, rows, cols);
                    }

                    // 写入表格外边框
                    if (m_TableData != null && m_TableData.OuterBorderNs != null && m_TableData.OuterBorderNs.Count > 0)
                    {
                        var outerRange = ws.Cells[1, 1, rows, cols];
                        var border = outerRange.Style.Border;
                        border.BorderAround(ExcelBorderStyle.Thin,
                            System.Drawing.Color.FromArgb(180, 180, 180));
                    }

                    // 保存
                    package.SaveAs(new FileInfo(filePath));
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[XTable] SaveToExcelFile 失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 将 XTable 的 StyleNamespace 映射为 Excel 单元格样式（字体颜色、背景、字号、对齐）。
        /// </summary>
        private void ApplyStyleToExcelCell(ExcelRange excelCell, string ns)
        {
            if (string.IsNullOrEmpty(ns)) return;

            var style = excelCell.Style;

            // 字体颜色
            Color fgColor = GetCellFgColor(ns);
            style.Font.Color.SetColor(System.Drawing.Color.FromArgb(
                (byte)(fgColor.a * 255),
                (byte)(fgColor.r * 255),
                (byte)(fgColor.g * 255),
                (byte)(fgColor.b * 255)));

            // 背景填充
            Color bgColor = GetCellBgColor(ns);
            style.Fill.PatternType = ExcelFillStyle.Solid;
            style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(
                (byte)(bgColor.a * 255),
                (byte)(bgColor.r * 255),
                (byte)(bgColor.g * 255),
                (byte)(bgColor.b * 255)));

            // 字体大小
            int fontSize = GetCellFontSize(ns);
            if (fontSize > 0)
                style.Font.Size = fontSize;

            // 对齐
            style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            style.VerticalAlignment = ExcelVerticalAlignment.Center;
        }

        /// <summary>
        /// 将表格中的合并描述写入 Excel 工作表。
        /// </summary>
        private void WriteMergedCellsToExcel(ExcelWorksheet ws, int rows, int cols)
        {
            if (m_TableData == null) return;

            foreach (var kv in m_TableData.BlockMap)
            {
                var block = kv.Value;
                if (block.MergeDescriptors == null || block.MergeDescriptors.Count == 0) continue;

                foreach (var desc in block.MergeDescriptors)
                {
                    if (desc.IsCrossBlock) continue; // 跨块合并暂不处理

                    // 计算全局坐标
                    int blockRow = block.BlockRow;
                    int blockCol = block.BlockCol;
                    int globalStartRow = blockRow * m_TableData.BlockSizeY + desc.LocalStartRow;
                    int globalStartCol = blockCol * m_TableData.BlockSizeX + desc.LocalStartCol;

                    int endR = globalStartRow + desc.MergeRowSpan - 1;
                    int endC = globalStartCol + desc.MergeColSpan - 1;

                    // 检查边界
                    if (endR >= rows || endC >= cols) continue;

                    var range = ws.Cells[globalStartRow + 1, globalStartCol + 1, endR + 1, endC + 1];
                    range.Merge = true;
                }
            }
        }

        /// <summary>
        /// 将表格的边框缓存（右侧/底部边框样式）写入 Excel 单元格边框。
        /// </summary>
        private void WriteBorderCacheToExcel(ExcelWorksheet ws, int rows, int cols)
        {
            if (m_TableData == null) return;

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    // 跳过被合并覆盖的单元格
                    if (m_TableData.IsCellMerged(r, c)) continue;

                    var excelCell = ws.Cells[r + 1, c + 1];

                    // 右侧边框
                    string rightNs = m_TableData.GetBorderRightNs(r, c);
                    if (!string.IsNullOrEmpty(rightNs))
                    {
                        Color color = GetCellBorderColor(rightNs);
                        float width = GetCellBorderWidth(rightNs);
                        excelCell.Style.Border.Right.Style = BorderWidthToStyle(width);
                        excelCell.Style.Border.Right.Color.SetColor(
                            System.Drawing.Color.FromArgb(
                                (byte)(color.a * 255),
                                (byte)(color.r * 255),
                                (byte)(color.g * 255),
                                (byte)(color.b * 255)));
                    }

                    // 底部边框
                    string bottomNs = m_TableData.GetBorderBottomNs(r, c);
                    if (!string.IsNullOrEmpty(bottomNs))
                    {
                        Color color = GetCellBorderColor(bottomNs);
                        float width = GetCellBorderWidth(bottomNs);
                        excelCell.Style.Border.Bottom.Style = BorderWidthToStyle(width);
                        excelCell.Style.Border.Bottom.Color.SetColor(
                            System.Drawing.Color.FromArgb(
                                (byte)(color.a * 255),
                                (byte)(color.r * 255),
                                (byte)(color.g * 255),
                                (byte)(color.b * 255)));
                    }
                }
            }

            // 顶部外框（第一行顶部）
            for (int c = 0; c < cols; c++)
            {
                string topNs = m_TableData.GetBorderBottomNs(-1, c);
                if (!string.IsNullOrEmpty(topNs))
                {
                    Color color = GetCellBorderColor(topNs);
                    float width = GetCellBorderWidth(topNs);
                    ws.Cells[1, c + 1].Style.Border.Top.Style = BorderWidthToStyle(width);
                    ws.Cells[1, c + 1].Style.Border.Top.Color.SetColor(
                        System.Drawing.Color.FromArgb(
                            (byte)(color.a * 255),
                            (byte)(color.r * 255),
                            (byte)(color.g * 255),
                            (byte)(color.b * 255)));
                }
            }

            // 左侧外框（第一列左侧）
            for (int r = 0; r < rows; r++)
            {
                string leftNs = m_TableData.GetBorderRightNs(r, -1);
                if (!string.IsNullOrEmpty(leftNs))
                {
                    Color color = GetCellBorderColor(leftNs);
                    float width = GetCellBorderWidth(leftNs);
                    ws.Cells[r + 1, 1].Style.Border.Left.Style = BorderWidthToStyle(width);
                    ws.Cells[r + 1, 1].Style.Border.Left.Color.SetColor(
                        System.Drawing.Color.FromArgb(
                            (byte)(color.a * 255),
                            (byte)(color.r * 255),
                            (byte)(color.g * 255),
                            (byte)(color.b * 255)));
                }
            }
        }

        /// <summary>
        /// 将边框宽度值映射到 Excel 边框样式枚举。
        /// </summary>
        private static ExcelBorderStyle BorderWidthToStyle(float width)
        {
            if (width <= 0.5f) return ExcelBorderStyle.Hair;
            if (width <= 1.0f) return ExcelBorderStyle.Thin;
            if (width <= 1.5f) return ExcelBorderStyle.Medium;
            if (width <= 2.0f) return ExcelBorderStyle.Medium;
            return ExcelBorderStyle.Thick;
        }

        #endregion
    }
}
