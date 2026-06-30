using System;
using System.Collections.Generic;

using XericUI.XTable.Mapping;
using XericUI.XTable.Rendering.Component;

namespace XericUI.XTable.Core
{
	/// <summary>
	/// 表格粘贴复制传输对象——将表格的尺寸、单元格文本、合并信息打包为可序列化结构，
	/// 配合 MacroFile.SetClipboardObject / GetClipboardObject 实现表格内容的导入导出。
	/// </summary>
	[Serializable]
	public class XTableClipboardData
	{
		[Serializable]
		public struct CellEntry
		{
			public int Row;
			public int Col;
			public string Text;
			public string StyleNamespace;

			public CellEntry(int row, int col, string text, string styleNamespace = null)
			{
				Row = row;
				Col = col;
				Text = text;
				StyleNamespace = styleNamespace;
			}
		}

		[Serializable]
		public struct MergeEntry
		{
			/// <summary>合并起始行（全局坐标）</summary>
			public int StartRow;

			/// <summary>合并起始列（全局坐标）</summary>
			public int StartCol;

			/// <summary>合并行数</summary>
			public int RowSpan;

			/// <summary>合并列数</summary>
			public int ColSpan;

			public MergeEntry(int startRow, int startCol, int rowSpan, int colSpan)
			{
				StartRow = startRow;
				StartCol = startCol;
				RowSpan = rowSpan;
				ColSpan = colSpan;
			}
		}

		/// <summary>行高数组</summary>
		public float[] RowHeights;

		/// <summary>列宽数组</summary>
		public float[] ColWidths;

		/// <summary>默认样式命名空间</summary>
		public string DefaultStyleNamespace;

		/// <summary>单元格数据列表（仅序列化有文本内容的单元格）</summary>
		public List<CellEntry> Cells;

		/// <summary>合并单元格描述列表</summary>
		public List<MergeEntry> Merges;

		public XTableClipboardData()
		{
			Cells = new List<CellEntry>();
			Merges = new List<MergeEntry>();
		}

		/// <summary>
		/// 从表格收集所有数据——尺寸、单元格文本、合并信息。
		/// 调用方可将返回的 DTO 传给 MacroFile.SetClipboardObject 写入剪贴板。
		/// </summary>
		public static XTableClipboardData FromTable(XericUIActionTable table)
		{
			var data = new XTableClipboardData
			{
				RowHeights = table.RowHeights,
				ColWidths = table.ColWidths,
				DefaultStyleNamespace = table.DefaultStyleNamespace,
			};

			int rows = table.RowHeights?.Length ?? 0;
			int cols = table.ColWidths?.Length ?? 0;
			var tableData = table.TableData;
			for (int r = 0; r < rows; r++)
			{
				for (int c = 0; c < cols; c++)
				{
					var cell = tableData.GetCell(r, c);
					if (cell == null) continue;

					bool hasContent = !string.IsNullOrEmpty(cell.Text)
					               || !string.IsNullOrEmpty(cell.StyleNamespace);
					if (!hasContent) continue;

					data.Cells.Add(new CellEntry(r, c, cell.Text, cell.StyleNamespace));
				}
			}

			// 收集合并信息（全局坐标）
			if (tableData != null)
			{
				foreach (var kv in tableData.BlockMap)
				{
					var block = kv.Value;
					if (block.MergeDescriptors == null || block.MergeDescriptors.Count == 0) continue;

					XTableBlockMapping.ZIndexToBlockCoord(kv.Key, out int blockRow, out int blockCol);

					foreach (var desc in block.MergeDescriptors)
					{
						if (desc.IsCrossBlock) continue;

						int globalStartRow = blockRow * tableData.BlockSizeY + desc.LocalStartRow;
						int globalStartCol = blockCol * tableData.BlockSizeX + desc.LocalStartCol;

						data.Merges.Add(new MergeEntry(globalStartRow, globalStartCol,
							desc.MergeRowSpan, desc.MergeColSpan));
					}
				}
			}

			return data;
		}

		/// <summary>
		/// 将 DTO 中的单元格文本和合并信息写回表格数据层。
		/// 尺寸与命名空间的设置由表格组件在 ApplyClipboardData 中处理。
		/// </summary>
		public void PopulateToTable(XericUIActionTable table)
		{
			var td = table.TableData;
			if (td == null) return;

			int rows = table.RowHeights?.Length ?? 0;
			int cols = table.ColWidths?.Length ?? 0;

			// 单元格文本
			if (Cells != null)
			{
				foreach (var entry in Cells)
				{
					if (entry.Row < 0 || entry.Row >= rows || entry.Col < 0 || entry.Col >= cols)
						continue;

					var cell = td.GetOrCreateCell(entry.Row, entry.Col);
					cell.Text = entry.Text;
					cell.StyleNamespace = entry.StyleNamespace;
				}
			}

			// 合并信息
			if (Merges != null)
			{
				foreach (var merge in Merges)
				{
					if (merge.RowSpan > 1 || merge.ColSpan > 1)
					{
						int endRow = merge.StartRow + merge.RowSpan - 1;
						int endCol = merge.StartCol + merge.ColSpan - 1;
						if (endRow < rows && endCol < cols)
							td.MergeCells(merge.StartRow, merge.StartCol, endRow, endCol);
					}
				}
			}
		}
	}
}
