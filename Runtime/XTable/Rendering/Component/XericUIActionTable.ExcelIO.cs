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
        #region 导入（公开入口）

        /// <summary>
        /// 从 Excel .xlsx 文件加载数据到表格。
        /// </summary>
        /// <param name="filePath">Excel 文件路径</param>
        /// <param name="sheetIndex">工作表索引（0-based），默认 0 即第一个工作表</param>
        public void LoadFromExcelFile(string filePath, int sheetIndex = 0)
        {
            ThrowIfCheckingUpdate(nameof(LoadFromExcelFile));
            if (!TryOpenPackage(filePath, out ExcelPackage package)) return;
            using (package)
            {
                if (sheetIndex < 0 || sheetIndex >= package.Workbook.Worksheets.Count)
                {
                    Debug.LogWarning($"[XTable] LoadFromExcelFile: 工作表索引 {sheetIndex} 超出范围（共 {package.Workbook.Worksheets.Count} 个工作表）");
                    return;
                }
                ExcelWorksheet ws = package.Workbook.Worksheets[sheetIndex];
                if (!TryGetWorksheetDimension(ws, out int rows, out int cols)) return;
                EnsureTableData();
                LoadFromExcelFileCore(ws, rows, cols);
            }
        }

        /// <summary>
        /// 从 Excel .xlsx 文件加载数据到表格，按工作表名称选择。
        /// </summary>
        /// <param name="filePath">Excel 文件路径</param>
        /// <param name="sheetName">工作表名称</param>
        public void LoadFromExcelFile(string filePath, string sheetName)
        {
            ThrowIfCheckingUpdate(nameof(LoadFromExcelFile));
            if (string.IsNullOrEmpty(sheetName))
            {
                Debug.LogWarning("[XTable] LoadFromExcelFile: 工作表名称为空");
                return;
            }
            if (!TryOpenPackage(filePath, out ExcelPackage package)) return;
            using (package)
            {
                ExcelWorksheet ws = package.Workbook.Worksheets[sheetName];
                if (ws == null)
                {
                    Debug.LogWarning($"[XTable] LoadFromExcelFile: 找不到工作表 \"{sheetName}\"");
                    return;
                }
                if (!TryGetWorksheetDimension(ws, out int rows, out int cols)) return;
                EnsureTableData();
                LoadFromExcelFileCore(ws, rows, cols);
            }
        }

        #endregion

        #region 导出（公开入口）

        /// <summary>
        /// 将表格数据导出为 Excel .xlsx 文件，写入指定名称的工作表。
        /// 文件不存在时自动创建；已存在时查找或追加同名工作表。
        /// </summary>
        /// <param name="filePath">Excel 文件路径</param>
        /// <param name="sheetName">工作表名称，默认 "Sheet1"</param>
        public void SaveToExcelFile(string filePath, string sheetName = "Sheet1")
        {
            if (string.IsNullOrEmpty(sheetName)) sheetName = "Sheet1";
            if (!EnsureDirectory(filePath)) return;

            try
            {
                if (File.Exists(filePath))
                {
                    // 追加到已有文件
                    using (var package = new ExcelPackage(new FileInfo(filePath)))
                    {
                        ExcelWorksheet ws = package.Workbook.Worksheets[sheetName]
                                          ?? package.Workbook.Worksheets.Add(sheetName);
                        SaveToExcelFileCore(ws);
                        package.Save();
                    }
                }
                else
                {
                    // 创建新文件
                    using (var package = new ExcelPackage())
                    {
                        ExcelWorksheet ws = package.Workbook.Worksheets.Add(sheetName);
                        SaveToExcelFileCore(ws);
                        package.SaveAs(new FileInfo(filePath));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[XTable] SaveToExcelFile 失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 将表格数据导出到 Excel .xlsx 文件，按索引覆盖指定工作表。
        /// 文件不存在时自动创建。
        /// </summary>
        /// <param name="filePath">Excel 文件路径</param>
        /// <param name="sheetIndex">工作表索引（0-based）</param>
        public void SaveToExcelFile(string filePath, int sheetIndex)
        {
            if (sheetIndex < 0)
            {
                Debug.LogWarning($"[XTable] SaveToExcelFile: 工作表索引不能为负数");
                return;
            }
            if (!EnsureDirectory(filePath)) return;

            try
            {
                if (File.Exists(filePath))
                {
                    // 写入已有文件的指定 sheet
                    using (var package = new ExcelPackage(new FileInfo(filePath)))
                    {
                        int count = package.Workbook.Worksheets.Count;
                        ExcelWorksheet ws;
                        if (sheetIndex < count)
                        {
                            ws = package.Workbook.Worksheets[sheetIndex];
                        }
                        else
                        {
                            // 超出范围则追加，中间空缺补空白 sheet
                            while (package.Workbook.Worksheets.Count <= sheetIndex)
                                package.Workbook.Worksheets.Add("Sheet" + (package.Workbook.Worksheets.Count + 1));
                            ws = package.Workbook.Worksheets[sheetIndex];
                        }
                        SaveToExcelFileCore(ws);
                        package.Save();
                    }
                }
                else
                {
                    // 创建新文件
                    using (var package = new ExcelPackage())
                    {
                        // 补齐到目标索引
                        while (package.Workbook.Worksheets.Count <= sheetIndex)
                            package.Workbook.Worksheets.Add("Sheet" + (package.Workbook.Worksheets.Count + 1));
                        ExcelWorksheet ws = package.Workbook.Worksheets[sheetIndex];
                        SaveToExcelFileCore(ws);
                        package.SaveAs(new FileInfo(filePath));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[XTable] SaveToExcelFile 失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        #endregion

        #region 导入核心

        /// <summary>
        /// 从已打开的工作表读取数据填入表格。
        /// </summary>
        private void LoadFromExcelFileCore(ExcelWorksheet ws, int rows, int cols)
        {
            // 设置行高
            m_RowHeights = new float[rows];
            for (int r = 0; r < rows; r++)
            {
                float h = (float)ws.Row(r + 1).Height;
                m_RowHeights[r] = h > 0 ? h : Config.DefaultRowHeight;
            }

            // 设置列宽
            m_ColWidths = new float[cols];
            for (int c = 0; c < cols; c++)
            {
                float w = (float)ws.Column(c + 1).Width;
                m_ColWidths[c] = w > 0 ? w : Config.DefaultColumnWidth;
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
                        int startR = addr.Start.Row - 1;
                        int startC = addr.Start.Column - 1;
                        int endR = addr.End.Row - 1;
                        int endC = addr.End.Column - 1;
                        int rowSpan = endR - startR + 1;
                        int colSpan = endC - startC + 1;

                        if ((rowSpan > 1 || colSpan > 1) && endR < rows && endC < cols)
                            m_TableData.MergeCells(startR, startC, rowSpan, colSpan);
                    }
                    catch
                    {
                        // 忽略无法解析的合并地址
                    }
                }
            }

            // 读取冻结窗格
            // FreezePanes 是 EPPlus 的写入方法，读取需通过工作表 XML 中的 d:pane 节点
            try
            {
                var nsMgr = new System.Xml.XmlNamespaceManager(ws.WorksheetXml.NameTable);
                nsMgr.AddNamespace("d", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");
                var paneNode = ws.WorksheetXml.SelectSingleNode("//d:pane[@state='frozen']", nsMgr);
                if (paneNode?.Attributes != null)
                {
                    string xSplit = paneNode.Attributes["xSplit"]?.Value;
                    string ySplit = paneNode.Attributes["ySplit"]?.Value;
                    int freezeRows = int.TryParse(ySplit, out var fr) ? fr : 0;
                    int freezeCols = int.TryParse(xSplit, out var fc) ? fc : 0;
                    if (freezeRows > 0 || freezeCols > 0)
                        SetFreezeCount(freezeRows, freezeCols);
                }
            }
            catch
            {
                // 忽略解析失败的冻结信息
            }

            // 标记脏并刷新
            UpdateResolvedCellSizes();
            RecalculateTotalSize();
            RequestRefresh(TableDirtyType.DataChanged | TableDirtyType.LayoutChanged, true);
        }

        #endregion

        #region 导出核心

        /// <summary>
        /// 将表格数据写入已准备好的工作表。
        /// </summary>
        private void SaveToExcelFileCore(ExcelWorksheet ws)
        {
            int rows = m_RowHeights?.Length ?? 0;
            int cols = m_ColWidths?.Length ?? 0;

            if (rows == 0 || cols == 0) return;

            // 设置列宽
            for (int c = 0; c < cols; c++)
                ws.Column(c + 1).Width = m_ColWidths[c];

            // 设置行高
            for (int r = 0; r < rows; r++)
            {
                float h = m_RowHeights[r];
                if (h > 0) ws.Row(r + 1).Height = h;
            }

            // 写入冻结窗格
            if (m_FreezeRowCount > 0 || m_FreezeColCount > 0)
            {
                int freezeRow = m_FreezeRowCount + 1;
                int freezeCol = m_FreezeColCount + 1;
                ws.View.FreezePanes(freezeRow, freezeCol);
            }

            // 写入单元格数据和样式
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    if (m_TableData != null && m_TableData.IsCellMerged(r, c))
                        continue;

                    var cell = m_TableData?.GetCell(r, c);
                    if (cell == null) continue;

                    var excelCell = ws.Cells[r + 1, c + 1];

                    if (!string.IsNullOrEmpty(cell.Text))
                        excelCell.Value = cell.Text;

                    string ns = ResolveCellNamespace(cell);
                    if (!string.IsNullOrEmpty(ns))
                        ApplyStyleToExcelCell(excelCell, ns);
                }
            }

            // 写入合并单元格
            if (m_TableData != null)
                WriteMergedCellsToExcel(ws, rows, cols);

            // 写入边框缓存
            if (m_TableData != null)
                WriteBorderCacheToExcel(ws, rows, cols);

            // 外边框样式已由范围边框写入，无需额外的统一 BorderAround。
        }

        #endregion

        #region 辅助方法

        /// <summary>打开 Excel 包，失败时 LogWarning 并返回 false。</summary>
        private static bool TryOpenPackage(string filePath, out ExcelPackage package)
        {
            package = null;
            if (string.IsNullOrEmpty(filePath))
            {
                Debug.LogWarning("[XTable] LoadFromExcelFile: 文件路径为空");
                return false;
            }
            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"[XTable] LoadFromExcelFile: 文件不存在: {filePath}");
                return false;
            }
            try
            {
                package = new ExcelPackage(new FileInfo(filePath));
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[XTable] 无法打开 Excel 文件: {ex.Message}");
                return false;
            }
        }

        /// <summary>获取工作表的有效维度，空表返回 false。</summary>
        private static bool TryGetWorksheetDimension(ExcelWorksheet ws, out int rows, out int cols)
        {
            rows = 0; cols = 0;
            if (ws == null)
            {
                Debug.LogWarning("[XTable] 工作表为空");
                return false;
            }
            if (ws.Dimension != null)
            {
                rows = ws.Dimension.Rows;
                cols = ws.Dimension.Columns;
            }
            if (rows <= 0 || cols <= 0)
            {
                Debug.LogWarning("[XTable] Excel 文件为空或无法识别行列数");
                return false;
            }
            return true;
        }

        /// <summary>确保数据层存在。</summary>
        private void EnsureTableData()
        {
            if (m_TableData == null)
                m_TableData = new XTableData();
        }

        /// <summary>确保导出目录存在，路径为空时返回 false。</summary>
        private static bool EnsureDirectory(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                Debug.LogWarning("[XTable] SaveToExcelFile: 文件路径为空");
                return false;
            }
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return true;
        }

        /// <summary>
        /// 将 XTable 的 StyleNamespace 映射为 Excel 单元格样式（字体颜色、背景、字号、对齐）。
        /// </summary>
        private void ApplyStyleToExcelCell(ExcelRange excelCell, string ns)
        {
            // ns 已确保非空
            var style = excelCell.Style;
            Color fgColor = GetCellFgColor(ns);
            style.Font.Color.SetColor(System.Drawing.Color.FromArgb(
                (byte)(fgColor.a * 255),
                (byte)(fgColor.r * 255),
                (byte)(fgColor.g * 255),
                (byte)(fgColor.b * 255)));

            Color bgColor = GetCellBgColor(ns);
            style.Fill.PatternType = ExcelFillStyle.Solid;
            style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(
                (byte)(bgColor.a * 255),
                (byte)(bgColor.r * 255),
                (byte)(bgColor.g * 255),
                (byte)(bgColor.b * 255)));

            int fontSize = GetCellFontSize(ns);
            if (fontSize > 0)
                style.Font.Size = fontSize;

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
                    if (desc.IsCrossBlock) continue;

                    int blockRow = block.BlockRow;
                    int blockCol = block.BlockCol;
                    int globalStartRow = blockRow * m_TableData.BlockSizeY + desc.LocalStartRow;
                    int globalStartCol = blockCol * m_TableData.BlockSizeX + desc.LocalStartCol;
                    int endR = globalStartRow + desc.MergeRowSpan - 1;
                    int endC = globalStartCol + desc.MergeColSpan - 1;

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
                    if (m_TableData.IsCellMerged(r, c)) continue;

                    var excelCell = ws.Cells[r + 1, c + 1];

                    string rightNs = m_TableData.GetBorderRightNs(r, c);
                    if (!string.IsNullOrEmpty(rightNs))
                    {
                        Color color = GetCellBorderColor(rightNs);
                        float width = GetCellBorderWidth(rightNs);
                        excelCell.Style.Border.Right.Style = BorderWidthToStyle(width);
                        excelCell.Style.Border.Right.Color.SetColor(
                            System.Drawing.Color.FromArgb(
                                (byte)(color.a * 255), (byte)(color.r * 255),
                                (byte)(color.g * 255), (byte)(color.b * 255)));
                    }

                    string bottomNs = m_TableData.GetBorderBottomNs(r, c);
                    if (!string.IsNullOrEmpty(bottomNs))
                    {
                        Color color = GetCellBorderColor(bottomNs);
                        float width = GetCellBorderWidth(bottomNs);
                        excelCell.Style.Border.Bottom.Style = BorderWidthToStyle(width);
                        excelCell.Style.Border.Bottom.Color.SetColor(
                            System.Drawing.Color.FromArgb(
                                (byte)(color.a * 255), (byte)(color.r * 255),
                                (byte)(color.g * 255), (byte)(color.b * 255)));
                    }
                }
            }

            // 顶部外框（第一行顶部）
            for (int c = 0; c < cols; c++)
            {
                string topNs = m_TableData.GetOuterBorderNs(-1, c);
                if (string.IsNullOrEmpty(topNs)) continue;
                Color color = GetCellBorderColor(topNs);
                float width = GetCellBorderWidth(topNs);
                ws.Cells[1, c + 1].Style.Border.Top.Style = BorderWidthToStyle(width);
                ws.Cells[1, c + 1].Style.Border.Top.Color.SetColor(
                    System.Drawing.Color.FromArgb(
                        (byte)(color.a * 255), (byte)(color.r * 255),
                        (byte)(color.g * 255), (byte)(color.b * 255)));
            }

            // 左侧外框（第一列左侧）
            for (int r = 0; r < rows; r++)
            {
                string leftNs = m_TableData.GetOuterBorderNs(r, -1);
                if (string.IsNullOrEmpty(leftNs)) continue;
                Color color = GetCellBorderColor(leftNs);
                float width = GetCellBorderWidth(leftNs);
                ws.Cells[r + 1, 1].Style.Border.Left.Style = BorderWidthToStyle(width);
                ws.Cells[r + 1, 1].Style.Border.Left.Color.SetColor(
                    System.Drawing.Color.FromArgb(
                        (byte)(color.a * 255), (byte)(color.r * 255),
                        (byte)(color.g * 255), (byte)(color.b * 255)));
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
