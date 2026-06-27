using System;
using System.Collections.Generic;

using UnityEngine;

using XericUI.XTable.Mapping;

namespace XericUI.XTable.Core
{
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
		public int BlockSizeX { get; private set; }

		/// <summary>块内行数（默认 <see cref="DefaultBlockSize"/>）</summary>
		public int BlockSizeY { get; private set; }

		/// <summary>
		/// 块字典——key = Z 曲线索引(ulong)，value = 数据块。
		/// 具有哈希表性质，按需创建，键不连续（离散存储）。
		/// </summary>
		public Dictionary<ulong, XTableBlock> BlockMap { get; private set; }

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

		public void OnBeforeSerialize() { }

		public void OnAfterDeserialize()
		{
			if (BlockSizeX <= 0) BlockSizeX = DefaultBlockSize;
			if (BlockSizeY <= 0) BlockSizeY = DefaultBlockSize;
			if (BlockMap == null) BlockMap = new Dictionary<ulong, XTableBlock>();
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

					// 查找或创建此块的合并描述（相同源）
					XTableMergeDescriptor descriptor = FindOrCreateMergeDescriptor(block,
						zIndex == srcZIndex ? srcLocalRow : -1,
						zIndex == srcZIndex ? srcLocalCol : -1,
						srcZIndex, srcLocalRow, srcLocalCol,
						zIndex != srcZIndex);

					descriptor.MergedCellSet.Add((localRow, localCol));
				}
			}
		}

		/// <summary>
		/// 查找或创建合并描述
		/// </summary>
		private XTableMergeDescriptor FindOrCreateMergeDescriptor(
			XTableBlock block, int checkLocalRow, int checkLocalCol,
			ulong srcZIndex, int srcLocalRow, int srcLocalCol, bool isCrossBlock)
		{
			// 查找已有描述
			for (int i = 0; i < block.MergeDescriptors.Count; i++)
			{
				var desc = block.MergeDescriptors[i];
				if (desc.MergeRowSpan > 0 && desc.MergeColSpan > 0)
				{
					// 简化的同源判断：跨块引用共享 sourceBlockIndex
					if (isCrossBlock && desc.IsCrossBlock && desc.SourceBlockIndex == srcZIndex)
						return desc;
				}
			}

			// 创建新描述
			var newDesc = new XTableMergeDescriptor
			{
				IsCrossBlock = isCrossBlock,
				SourceBlockIndex = srcZIndex,
				SourceLocalRow = srcLocalRow,
				SourceLocalCol = srcLocalCol
			};
			block.MergeDescriptors.Add(newDesc);
			return newDesc;
		}

		/// <summary>
		/// 取消指定区域的合并
		/// </summary>
		public void UnmergeCells(int startRow, int startCol)
		{
			XTableCoordinateUtility.CellToBlockCoordinate(
				startRow, startCol, BlockSizeX, BlockSizeY,
				out int blockRow, out int blockCol,
				out int localRow, out int localCol);

			ulong zIndex = XTableBlockMapping.BlockCoordToZIndex(blockRow, blockCol);

			if (!BlockMap.TryGetValue(zIndex, out XTableBlock block))
				return;

			// 移除包含此单元格的所有合并描述
			for (int i = block.MergeDescriptors.Count - 1; i >= 0; i--)
			{
				if (block.MergeDescriptors[i].Contains(localRow, localCol))
				{
					block.MergeDescriptors.RemoveAt(i);
				}
			}
		}

		#endregion

		#region 查询方法

		/// <summary>
		/// 判断指定块是否存在
		/// </summary>
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
	}
}
