using System;
using System.Collections.Generic;

namespace XericUI.XTable.Core
{
	/// <summary>
	/// 合并单元格描述——所有被合并单元格的 id 指向合并源（左上角单元格）。
	/// 同一类型支持本地块内引用和跨块引用，通过 <see cref="IsCrossBlock"/> 区分。
	/// </summary>
	[Serializable]
	public class XTableMergeDescriptor
	{
		/// <summary>合并区域在本地块内的起始行</summary>
		public int LocalStartRow;

		/// <summary>合并区域在本地块内的起始列</summary>
		public int LocalStartCol;

		/// <summary>合并跨越的行数</summary>
		public int MergeRowSpan;

		/// <summary>合并跨越的列数</summary>
		public int MergeColSpan;

		/// <summary>是否为跨块引用（true=合并源在另一个块上）</summary>
		public bool IsCrossBlock;

		/// <summary>
		/// 跨块引用时：源块在字典中的 key（Z 曲线索引）
		/// 本地引用时：忽略此字段
		/// </summary>
		public ulong SourceBlockIndex;

		/// <summary>源单元格在块内的行号（跨块时=源块的行、本地时=本块的行）</summary>
		public int SourceLocalRow;

		/// <summary>源单元格在块内的列号（跨块时=源块的列、本地时=本块的列）</summary>
		public int SourceLocalCol;

		/// <summary>本块内被合并的 (row, col) 集合，查询时快速判断是否需要重定向</summary>
		public HashSet<(int row, int col)> MergedCellSet;

		public XTableMergeDescriptor()
		{
			MergedCellSet = new HashSet<(int, int)>();
		}

		/// <summary>
		/// 判断指定本地坐标是否在此合并区域内
		/// </summary>
		public bool Contains(int localRow, int localCol)
		{
			return MergedCellSet.Contains((localRow, localCol));
		}

		/// <summary>
		/// 获取重定向目标——返回 (目标块Z索引, 目标块内行, 目标块内列)
		/// 本地引用时目标块索引为 0（由调用方忽略）
		/// </summary>
		public void GetRedirectTarget(out ulong targetBlockIndex, out int targetRow, out int targetCol)
		{
			targetBlockIndex = IsCrossBlock ? SourceBlockIndex : 0;
			targetRow = SourceLocalRow;
			targetCol = SourceLocalCol;
		}
	}
}
