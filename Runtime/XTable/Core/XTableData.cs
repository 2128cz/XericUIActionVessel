using System;
using System.Collections.Generic;

using UnityEngine;

using XericUI.XTable.Mapping;

namespace XericUI.XTable.Core
{
	public enum XTableBorderAxis : byte
	{
		Horizontal,
		Vertical,
	}

	[Serializable]
	public struct XTableBorderRange
	{
		public XTableBorderAxis Axis;
		public int FixedIndex;
		public int StartIndex;
		public int Length;
		public int StyleIndex;

		public int EndIndex => StartIndex + Length;
	}

	/// <summary>
	/// 表格数据主类——持有块字典，提供单元格级别的读写接口。
	/// 块按需创建（SetCell 时自动创建不存在的块），通过 Z 曲线索引映射。
	/// </summary>
	[Serializable]
	public class XTableData : ISerializationCallbackReceiver
	{
		/// <summary>全系统唯一默认块尺寸——禁止硬编码字面量</summary>
		public const int DefaultBlockSize = 32;

		#region 属性

		/// <summary>块内列数（默认 <see cref="DefaultBlockSize"/>）</summary>
		[field: SerializeField] public int BlockSizeX { get; private set; }

		/// <summary>块内行数（默认 <see cref="DefaultBlockSize"/>）</summary>
		[field: SerializeField] public int BlockSizeY { get; private set; }

		/// <summary>
		/// 块字典——key = Z 曲线索引(ulong)，value = 数据块。
		/// 具有哈希表性质，按需创建，键不连续（离散存储）。
		/// 注意：Dictionary 不被 Unity 序列化器原生支持，通过 <see cref="m_BlockKeys"/> / <see cref="m_BlockValues"/> 代理序列化。
		/// </summary>
		[NonSerialized] public Dictionary<ulong, XTableBlock> BlockMap;

		// ── Dictionary 序列化代理 ──
		// Unity 无法直接序列化 Dictionary<ulong, XTableBlock>，
		// 使用两个平行列表在 OnBeforeSerialize/OnAfterDeserialize 中转换。
		[SerializeField] private List<ulong> m_BlockKeys = new List<ulong>();
		[SerializeField] private List<XTableBlock> m_BlockValues = new List<XTableBlock>();

		[SerializeField] private List<string> m_BorderStyles = new List<string>();
		[SerializeField] private List<XTableBorderRange> m_BorderRanges = new List<XTableBorderRange>();

		// 仅用于读取旧资产的外边框序列化代理；迁移后不再写入。
		[SerializeField] private List<int> m_OuterBorderRows = new List<int>();
		[SerializeField] private List<int> m_OuterBorderCols = new List<int>();
		[SerializeField] private List<string> m_OuterBorderValues = new List<string>();

		[NonSerialized] private Dictionary<(XTableBorderAxis axis, int fixedIndex), List<XTableBorderRange>> m_BorderRangeIndex;

		/// <summary>
		/// 获取或创建单元格——如果单元格不存在则自动创建空数据。
		/// 在编辑器和运行时都是安全的，保证不会返回 null。
		/// </summary>
		public XTableCellData GetOrCreateCell(int row, int col)
		{
			var data = GetCell(row, col);
			if (data == null)
			{
				data = new XTableCellData();
				SetCell(row, col, data);
			}
			return data;
		}

		/// <summary>
		/// 判断指定单元格是否有数据
		/// </summary>
		public bool HasCell(int row, int col)
		{
			return GetCell(row, col) != null;
		}

		#endregion

		#region 构造函数

		/// <summary>
		/// 创建表格数据实例
		/// </summary>
		/// <param name="blockSizeX">块内列数，默认 <see cref="DefaultBlockSize"/></param>
		/// <param name="blockSizeY">块内行数，默认 <see cref="DefaultBlockSize"/></param>
		public XTableData(int blockSizeX = DefaultBlockSize, int blockSizeY = DefaultBlockSize)
		{
			BlockSizeX = blockSizeX > 0 ? blockSizeX : DefaultBlockSize;
			BlockSizeY = blockSizeY > 0 ? blockSizeY : DefaultBlockSize;
			BlockMap = new Dictionary<ulong, XTableBlock>();
		}

		#endregion

		#region 序列化回调

		public void OnBeforeSerialize()
		{
			if (m_BorderRangeIndex != null)
				RebuildSerializedBorderRanges();
			// Dictionary → 平行列表
			m_BlockKeys.Clear();
			m_BlockValues.Clear();
			if (BlockMap != null)
			{
				foreach (var kv in BlockMap)
				{
					m_BlockKeys.Add(kv.Key);
					m_BlockValues.Add(kv.Value);
				}
			}
		}

		public void OnAfterDeserialize()
		{
			if (m_BorderStyles == null) m_BorderStyles = new List<string>();
			if (m_BorderRanges == null) m_BorderRanges = new List<XTableBorderRange>();
			m_BorderRangeIndex = null;
			if (BlockSizeX <= 0) BlockSizeX = DefaultBlockSize;
			if (BlockSizeY <= 0) BlockSizeY = DefaultBlockSize;

			// 平行列表 → Dictionary
			BlockMap = new Dictionary<ulong, XTableBlock>();
			int count = System.Math.Min(
				m_BlockKeys?.Count ?? 0,
				m_BlockValues?.Count ?? 0);
			for (int i = 0; i < count; i++)
			{
				if (m_BlockValues[i] != null)
					BlockMap[m_BlockKeys[i]] = m_BlockValues[i];
			}
			MigrateLegacyBorderCache();
		}

		#endregion

		#region 核心访问方法

		/// <summary>
		/// 通过全局行列坐标获取单元格数据。
		/// 先计算块坐标和 Z 索引，查找块字典，再检查合并重定向。
		/// </summary>
		/// <returns>单元格数据，不存在则返回 null</returns>
		public XTableCellData GetCell(int row, int col)
		{
			XTableCoordinateUtility.CellToBlockCoordinate(
				row, col, BlockSizeX, BlockSizeY,
				out int blockRow, out int blockCol,
				out int localRow, out int localCol);

			ulong zIndex = XTableBlockMapping.BlockCoordToZIndex(blockRow, blockCol);

			if (!BlockMap.TryGetValue(zIndex, out XTableBlock block))
				return null;

			// 检查合并重定向
			XTableMergeDescriptor merge = block.GetMergeRedirect(localRow, localCol);
			if (merge != null)
			{
				merge.GetRedirectTarget(out ulong targetBlockIndex, out int targetRow, out int targetCol);

				if (merge.IsCrossBlock)
				{
					// 跨块引用：跳转到目标块
					if (!BlockMap.TryGetValue(targetBlockIndex, out XTableBlock targetBlock))
						return null;
					return targetBlock.GetCell(targetRow, targetCol);
				}
				else
				{
					// 本地引用：同块内重定向
					return block.GetCell(targetRow, targetCol);
				}
			}

			return block.GetCell(localRow, localCol);
		}

		/// <summary>
		/// 尝试获取单元格数据
		/// </summary>
		public bool TryGetCell(int row, int col, out XTableCellData data)
		{
			data = GetCell(row, col);
			return data != null;
		}

		/// <summary>
		/// 通过全局行列坐标设置单元格数据。
		/// 如果对应的块不存在，则按需创建。
		/// </summary>
		public void SetCell(int row, int col, XTableCellData data)
		{
			XTableCoordinateUtility.CellToBlockCoordinate(
				row, col, BlockSizeX, BlockSizeY,
				out int blockRow, out int blockCol,
				out int localRow, out int localCol);

			ulong zIndex = XTableBlockMapping.BlockCoordToZIndex(blockRow, blockCol);

			if (!BlockMap.TryGetValue(zIndex, out XTableBlock block))
			{
				block = new XTableBlock(BlockSizeX, BlockSizeY, blockRow, blockCol);
				BlockMap[zIndex] = block;
			}

			block.SetCell(localRow, localCol, data);
		}

		#endregion

		#region 合并单元格

		/// <summary>
		/// 合并从 (startRow, startCol) 开始、跨越 rowSpan 行和 colSpan 列的矩形区域。
		/// 合并源为左上角单元格 (startRow, startCol)。
		/// 如果合并区域跨越多个块，则每个涉及的块都会创建对应的 MergeDescriptor。
		/// </summary>
		public void MergeCells(int startRow, int startCol, int rowSpan, int colSpan)
		{
			// 确保合并源单元格存在
			XTableCoordinateUtility.CellToBlockCoordinate(
				startRow, startCol, BlockSizeX, BlockSizeY,
				out int srcBlockRow, out int srcBlockCol,
				out int srcLocalRow, out int srcLocalCol);

			ulong srcZIndex = XTableBlockMapping.BlockCoordToZIndex(srcBlockRow, srcBlockCol);

			// 遍历合并区域内的所有单元格，分块处理
			for (int r = startRow; r < startRow + rowSpan; r++)
			{
				for (int c = startCol; c < startCol + colSpan; c++)
				{
					// 跳过合并源自身
					if (r == startRow && c == startCol) continue;

					XTableCoordinateUtility.CellToBlockCoordinate(
						r, c, BlockSizeX, BlockSizeY,
						out int blockRow, out int blockCol,
						out int localRow, out int localCol);

					ulong zIndex = XTableBlockMapping.BlockCoordToZIndex(blockRow, blockCol);

					// 确保块存在
					if (!BlockMap.TryGetValue(zIndex, out XTableBlock block))
					{
						block = new XTableBlock(BlockSizeX, BlockSizeY, blockRow, blockCol);
						BlockMap[zIndex] = block;
					}

					// 计算本块内合并区域的局部起始和跨度
					int localStartRow = Math.Max(0, startRow - blockRow * BlockSizeY);
					int localStartCol = Math.Max(0, startCol - blockCol * BlockSizeX);
					int blockEndRow = Math.Min((blockRow + 1) * BlockSizeY, startRow + rowSpan) - blockRow * BlockSizeY;
					int blockEndCol = Math.Min((blockCol + 1) * BlockSizeX, startCol + colSpan) - blockCol * BlockSizeX;

					// 查找或创建此块的合并描述
					XTableMergeDescriptor descriptor = FindOrCreateMergeDescriptor(block,
						srcZIndex, srcLocalRow, srcLocalCol,
						zIndex != srcZIndex,
						localStartRow, localStartCol,
						blockEndRow - localStartRow, blockEndCol - localStartCol,
						rowSpan, colSpan);

					descriptor.MergedCellSet.Add((localRow, localCol));
				}
			}
		}

		/// <summary>
		/// 查找或创建合并描述
		/// </summary>
		private XTableMergeDescriptor FindOrCreateMergeDescriptor(
			XTableBlock block,
			ulong srcZIndex, int srcLocalRow, int srcLocalCol, bool isCrossBlock,
			int localStartRow, int localStartCol, int blockRowSpan, int blockColSpan,
			int totalRowSpan, int totalColSpan)
		{
			// 查找已有描述：匹配源坐标和跨块标记
			for (int i = 0; i < block.MergeDescriptors.Count; i++)
			{
				var desc = block.MergeDescriptors[i];
				if (desc.IsCrossBlock == isCrossBlock &&
				    desc.SourceBlockIndex == srcZIndex &&
				    desc.SourceLocalRow == srcLocalRow &&
				    desc.SourceLocalCol == srcLocalCol)
					return desc;
			}

			// 创建新描述，设置完整跨度信息
			var newDesc = new XTableMergeDescriptor
			{
				IsCrossBlock = isCrossBlock,
				SourceBlockIndex = srcZIndex,
				SourceLocalRow = srcLocalRow,
				SourceLocalCol = srcLocalCol,
				LocalStartRow = localStartRow,
				LocalStartCol = localStartCol,
				MergeRowSpan = totalRowSpan,
				MergeColSpan = totalColSpan
			};
			block.MergeDescriptors.Add(newDesc);
			return newDesc;
		}

		/// <summary>
		/// 清除所有块的全部合并描述
		/// </summary>
		public void ClearAllMerges()
		{
			foreach (var kv in BlockMap)
			{
				if (kv.Value.MergeDescriptors != null)
					kv.Value.MergeDescriptors.Clear();
			}
		}

		/// <summary>
		/// 取消指定坐标所在合并——同时处理"点击源坐标"和"点击被合并坐标"两种情况。
		/// 遍历所有块，移除所有指向该源的合并描述。
		/// </summary>
		public void UnmergeCells(int startRow, int startCol)
		{
			XTableCoordinateUtility.CellToBlockCoordinate(
				startRow, startCol, BlockSizeX, BlockSizeY,
				out int blockRow, out int blockCol,
				out int localRow, out int localCol);

			ulong zIndex = XTableBlockMapping.BlockCoordToZIndex(blockRow, blockCol);

			// 1) 从这个单元格所在的块开始：移除包含此坐标的合并描述（被合并格）
			if (BlockMap.TryGetValue(zIndex, out XTableBlock block))
			{
				for (int i = block.MergeDescriptors.Count - 1; i >= 0; i--)
				{
					if (block.MergeDescriptors[i].Contains(localRow, localCol))
						block.MergeDescriptors.RemoveAt(i);
				}
			}

			// 2) 遍历所有块：移除源坐标为此单元格的合并描述（合并源 / 锚点）
			//    处理非跨块和跨块两种情况
			foreach (var kv in BlockMap)
			{
				var b = kv.Value;
				if (b.MergeDescriptors.Count == 0) continue;

				for (int i = b.MergeDescriptors.Count - 1; i >= 0; i--)
				{
					var desc = b.MergeDescriptors[i];

					// 非跨块：源在同一个块内 → 直接比较本地坐标
					if (!desc.IsCrossBlock &&
					    desc.SourceLocalRow == localRow &&
					    desc.SourceLocalCol == localCol &&
					    kv.Key == zIndex)
					{
						b.MergeDescriptors.RemoveAt(i);
					}
					// 跨块：源在另一个块 → 比较 SourceBlockIndex + 源本地坐标
					else if (desc.IsCrossBlock &&
					         desc.SourceBlockIndex == zIndex &&
					         desc.SourceLocalRow == localRow &&
					         desc.SourceLocalCol == localCol)
					{
						b.MergeDescriptors.RemoveAt(i);
					}
				}
			}
		}

		#endregion

		#region 查询方法

		/// <summary>判断指定单元格是否被合并覆盖（非源，会被重定向）</summary>
		public bool IsCellMerged(int row, int col)
		{
			XTableCoordinateUtility.CellToBlockCoordinate(
				row, col, BlockSizeX, BlockSizeY,
				out int blockRow, out int blockCol,
				out int localRow, out int localCol);
			ulong zIndex = XTableBlockMapping.BlockCoordToZIndex(blockRow, blockCol);
			if (!BlockMap.TryGetValue(zIndex, out XTableBlock block)) return false;
			return block.GetMergeRedirect(localRow, localCol) != null;
		}

		/// <summary>获取任意合并区域单元格所属的完整合并矩形。</summary>
		public bool TryGetMergeRect(int row, int col, out int sourceRow, out int sourceCol,
			out int rowSpan, out int colSpan)
		{
			sourceRow = row;
			sourceCol = col;
			rowSpan = 1;
			colSpan = 1;
			XTableCoordinateUtility.CellToBlockCoordinate(row, col, BlockSizeX, BlockSizeY,
				out int blockRow, out int blockCol, out int localRow, out int localCol);
			ulong blockIndex = XTableBlockMapping.BlockCoordToZIndex(blockRow, blockCol);
			if (!BlockMap.TryGetValue(blockIndex, out XTableBlock block)) return false;

			XTableMergeDescriptor descriptor = block.GetMergeRedirect(localRow, localCol);
			if (descriptor == null)
			{
				foreach (var pair in BlockMap)
				{
					foreach (var candidate in pair.Value.MergeDescriptors)
					{
						ulong sourceBlock = candidate.IsCrossBlock ? candidate.SourceBlockIndex : pair.Key;
						if (sourceBlock == blockIndex && candidate.SourceLocalRow == localRow && candidate.SourceLocalCol == localCol)
						{
							descriptor = candidate;
							break;
						}
					}
					if (descriptor != null) break;
				}
			}
			if (descriptor == null) return false;

			ulong sourceIndex = descriptor.IsCrossBlock ? descriptor.SourceBlockIndex : blockIndex;
			XTableBlockMapping.ZIndexToBlockCoord(sourceIndex, out int sourceBlockRow, out int sourceBlockCol);
			sourceRow = sourceBlockRow * BlockSizeY + descriptor.SourceLocalRow;
			sourceCol = sourceBlockCol * BlockSizeX + descriptor.SourceLocalCol;
			rowSpan = descriptor.MergeRowSpan;
			colSpan = descriptor.MergeColSpan;
			return true;
		}

		/// <summary>判断指定单元格是否为合并源（锚点），并返回合并跨度</summary>
		public bool IsMergeSource(int row, int col, out int rowSpan, out int colSpan)
		{
			rowSpan = 1; colSpan = 1;
			XTableCoordinateUtility.CellToBlockCoordinate(
				row, col, BlockSizeX, BlockSizeY,
				out int blockRow, out int blockCol,
				out int localRow, out int localCol);
			ulong thisZIndex = XTableBlockMapping.BlockCoordToZIndex(blockRow, blockCol);

			foreach (var kv in BlockMap)
			{
				foreach (var desc in kv.Value.MergeDescriptors)
				{
					ulong srcBlockIdx = desc.IsCrossBlock ? desc.SourceBlockIndex : kv.Key;
					if (srcBlockIdx == thisZIndex &&
					    desc.SourceLocalRow == localRow &&
					    desc.SourceLocalCol == localCol)
					{
						rowSpan = desc.MergeRowSpan;
						colSpan = desc.MergeColSpan;
						return true;
					}
				}
			}
			return false;
		}

		/// <summary>判断指定块是否存在</summary>
		public bool HasBlock(int blockRow, int blockCol)
		{
			ulong zIndex = XTableBlockMapping.BlockCoordToZIndex(blockRow, blockCol);
			return BlockMap.ContainsKey(zIndex);
		}

		/// <summary>
		/// 获取块数量
		/// </summary>
		public int BlockCount => BlockMap.Count;

		#endregion

		#region 边框范围

		public IReadOnlyList<XTableBorderRange> BorderRanges => m_BorderRanges;

		/// <summary>对矩形四条外边写入显式样式范围；空样式恢复隐式默认值。</summary>
		public void SetBorderRect(int rowStart, int rowEnd, int colStart, int colEnd, string ns)
		{
			if (rowStart >= rowEnd || colStart >= colEnd) return;
			SetBorderRange(XTableBorderAxis.Horizontal, rowStart, colStart, colEnd - colStart, ns);
			SetBorderRange(XTableBorderAxis.Horizontal, rowEnd, colStart, colEnd - colStart, ns);
			SetBorderRange(XTableBorderAxis.Vertical, colStart, rowStart, rowEnd - rowStart, ns);
			SetBorderRange(XTableBorderAxis.Vertical, colEnd, rowStart, rowEnd - rowStart, ns);
		}

		public string GetBorderRightNs(int row, int col) =>
			row < 0 || col < 0 ? GetOuterBorderNs(row, col) : GetBorderNamespace(XTableBorderAxis.Vertical, col + 1, row);
		public string GetBorderBottomNs(int row, int col) =>
			row < 0 || col < 0 ? GetOuterBorderNs(row, col) : GetBorderNamespace(XTableBorderAxis.Horizontal, row + 1, col);
		public void SetBorderRightNs(int row, int col, string ns)
		{
			if (row < 0 || col < 0) SetOuterBorderNs(row, col, ns);
			else SetBorderRange(XTableBorderAxis.Vertical, col + 1, row, 1, ns);
		}
		public void SetBorderBottomNs(int row, int col, string ns)
		{
			if (row < 0 || col < 0) SetOuterBorderNs(row, col, ns);
			else SetBorderRange(XTableBorderAxis.Horizontal, row + 1, col, 1, ns);
		}

		public string GetOuterBorderNs(int row, int col)
		{
			if (row < 0 && col >= 0) return GetBorderNamespace(XTableBorderAxis.Horizontal, 0, col);
			if (col < 0 && row >= 0) return GetBorderNamespace(XTableBorderAxis.Vertical, 0, row);
			return null;
		}

		public void SetOuterBorderNs(int row, int col, string ns)
		{
			if (row < 0 && col >= 0) SetBorderRange(XTableBorderAxis.Horizontal, 0, col, 1, ns);
			else if (col < 0 && row >= 0) SetBorderRange(XTableBorderAxis.Vertical, 0, row, 1, ns);
		}

		public void SetBorderRange(XTableBorderAxis axis, int fixedIndex, int startIndex, int length, string ns)
		{
			if (length <= 0) return;
			EnsureBorderIndex();
			var key = (axis, fixedIndex);
			if (!m_BorderRangeIndex.TryGetValue(key, out var ranges))
			{
				ranges = new List<XTableBorderRange>();
				m_BorderRangeIndex[key] = ranges;
			}

			int end = startIndex + length;
			var updated = new List<XTableBorderRange>(ranges.Count + 1);
			for (int i = 0; i < ranges.Count; i++)
			{
				var old = ranges[i];
				if (old.EndIndex <= startIndex || old.StartIndex >= end)
				{
					updated.Add(old);
					continue;
				}
				if (old.StartIndex < startIndex)
				{
					old.Length = startIndex - old.StartIndex;
					updated.Add(old);
				}
				if (old.EndIndex > end)
				{
					int oldEnd = old.EndIndex;
					old.StartIndex = end;
					old.Length = oldEnd - end;
					updated.Add(old);
				}
			}

			if (!string.IsNullOrEmpty(ns))
			{
				updated.Add(new XTableBorderRange
				{
					Axis = axis, FixedIndex = fixedIndex, StartIndex = startIndex,
					Length = length, StyleIndex = GetOrAddBorderStyle(ns)
				});
			}
			updated.Sort((a, b) => a.StartIndex.CompareTo(b.StartIndex));
			MergeAdjacentRanges(updated);
			ranges.Clear();
			ranges.AddRange(updated);
			RebuildSerializedBorderRanges();
		}

		public string GetBorderNamespace(XTableBorderAxis axis, int fixedIndex, int index)
		{
			EnsureBorderIndex();
			if (!m_BorderRangeIndex.TryGetValue((axis, fixedIndex), out var ranges)) return null;
			for (int i = 0; i < ranges.Count; i++)
				if (index >= ranges[i].StartIndex && index < ranges[i].EndIndex)
					return GetBorderStyle(ranges[i].StyleIndex);
			return null;
		}

		public void ClearAllBorderCache()
		{
			m_BorderStyles.Clear();
			m_BorderRanges.Clear();
			m_BorderRangeIndex?.Clear();
		}

		private void MigrateLegacyBorderCache()
		{
			if (m_BorderRanges.Count > 0 || BlockMap == null) return;
			int outerCount = Math.Min(m_OuterBorderRows?.Count ?? 0, Math.Min(m_OuterBorderCols?.Count ?? 0, m_OuterBorderValues?.Count ?? 0));
			for (int i = 0; i < outerCount; i++)
				SetOuterBorderNs(m_OuterBorderRows[i], m_OuterBorderCols[i], m_OuterBorderValues[i]);
			m_OuterBorderRows?.Clear();
			m_OuterBorderCols?.Clear();
			m_OuterBorderValues?.Clear();
			foreach (var pair in BlockMap)
			{
				var block = pair.Value;
				if (block == null) continue;
				int count = block.BlockSizeX * block.BlockSizeY;
				for (int i = 0; i < count; i++)
				{
					int row = block.BlockRow * block.BlockSizeY + i / block.BlockSizeX;
					int col = block.BlockCol * block.BlockSizeX + i % block.BlockSizeX;
					if (block.BorderRightNs != null && i < block.BorderRightNs.Length && !string.IsNullOrEmpty(block.BorderRightNs[i]))
						SetBorderRange(XTableBorderAxis.Vertical, col + 1, row, 1, block.BorderRightNs[i]);
					if (block.BorderBottomNs != null && i < block.BorderBottomNs.Length && !string.IsNullOrEmpty(block.BorderBottomNs[i]))
						SetBorderRange(XTableBorderAxis.Horizontal, row + 1, col, 1, block.BorderBottomNs[i]);
				}
				block.BorderRightNs = null;
				block.BorderBottomNs = null;
			}
		}

		private int GetOrAddBorderStyle(string ns)
		{
			for (int i = 0; i < m_BorderStyles.Count; i++)
				if (m_BorderStyles[i] == ns) return i;
			m_BorderStyles.Add(ns);
			return m_BorderStyles.Count - 1;
		}

		private string GetBorderStyle(int styleIndex)
		{
			return styleIndex >= 0 && styleIndex < m_BorderStyles.Count ? m_BorderStyles[styleIndex] : null;
		}

		private void EnsureBorderIndex()
		{
			if (m_BorderRangeIndex != null) return;
			m_BorderRangeIndex = new Dictionary<(XTableBorderAxis axis, int fixedIndex), List<XTableBorderRange>>();
			for (int i = 0; i < m_BorderRanges.Count; i++)
			{
				var range = m_BorderRanges[i];
				var key = (range.Axis, range.FixedIndex);
				if (!m_BorderRangeIndex.TryGetValue(key, out var ranges))
				{
					ranges = new List<XTableBorderRange>();
					m_BorderRangeIndex[key] = ranges;
				}
				ranges.Add(range);
			}
		}

		private void RebuildSerializedBorderRanges()
		{
			EnsureBorderIndex();
			m_BorderRanges.Clear();
			foreach (var pair in m_BorderRangeIndex)
				m_BorderRanges.AddRange(pair.Value);
		}

		private static void MergeAdjacentRanges(List<XTableBorderRange> ranges)
		{
			for (int i = ranges.Count - 2; i >= 0; i--)
			{
				var current = ranges[i];
				var next = ranges[i + 1];
				if (current.EndIndex == next.StartIndex && current.StyleIndex == next.StyleIndex)
				{
					current.Length += next.Length;
					ranges[i] = current;
					ranges.RemoveAt(i + 1);
				}
			}
		}

		#endregion
	}
}
