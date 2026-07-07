using System;
using System.Collections.Generic;

using UnityEngine;

namespace XericUI.XTable.Core
{
	/// <summary>
	/// 单个数据块——保存 BlockSizeX × BlockSizeY 个单元格数据。
	/// 数据使用线性数组存储，索引 = localRow * BlockSizeX + localCol。
	/// </summary>
	[Serializable]
	public class XTableBlock : ISerializationCallbackReceiver
	{
		/// <summary>块在全局块坐标系中的行号</summary>
		public int BlockRow;

		/// <summary>块在全局块坐标系中的列号</summary>
		public int BlockCol;

		/// <summary>块内单元格列数</summary>
		public int BlockSizeX;

		/// <summary>块内单元格行数</summary>
		public int BlockSizeY;

		/// <summary>
		/// 数据存储——线性数组。
		/// 索引 = localRow * BlockSizeX + localCol。
		/// </summary>
		public XTableCellData[] Cells;

		/// <summary>合并单元格描述列表</summary>
		public List<XTableMergeDescriptor> MergeDescriptors;

		/// <summary>每个单元格右侧边框的样式命名空间缓存（线性数组，同 Cells[] 布局）</summary>
		public string[] BorderRightNs;
		/// <summary>每个单元格底部边框的样式命名空间缓存（线性数组，同 Cells[] 布局）</summary>
		public string[] BorderBottomNs;

		/// <summary>
		/// 创建指定尺寸的块
		/// </summary>
		public XTableBlock(int blockSizeX, int blockSizeY)
		{
			BlockSizeX = blockSizeX;
			BlockSizeY = blockSizeY;
			Cells = new XTableCellData[blockSizeX * blockSizeY];
			MergeDescriptors = new List<XTableMergeDescriptor>();
			BorderRightNs = new string[blockSizeX * blockSizeY];
			BorderBottomNs = new string[blockSizeX * blockSizeY];
		}

		/// <summary>
		/// 创建指定尺寸和位置的块
		/// </summary>
		public XTableBlock(int blockSizeX, int blockSizeY, int blockRow, int blockCol)
			: this(blockSizeX, blockSizeY)
		{
			BlockRow = blockRow;
			BlockCol = blockCol;
		}

		/// <summary>
		/// 通过块内局部坐标获取单元格数据
		/// </summary>
		public XTableCellData GetCell(int localRow, int localCol)
		{
			int index = localRow * BlockSizeX + localCol;
			if (index < 0 || index >= Cells.Length) return null;
			return Cells[index];
		}

		/// <summary>
		/// 通过块内局部坐标设置单元格数据
		/// </summary>
		public void SetCell(int localRow, int localCol, XTableCellData data)
		{
			int index = localRow * BlockSizeX + localCol;
			if (index < 0 || index >= Cells.Length) return;
			Cells[index] = data;
		}

		/// <summary>通过块内局部坐标获取右侧边框缓存</summary>
		public string GetBorderRight(int localRow, int localCol)
		{
			int index = localRow * BlockSizeX + localCol;
			if (index < 0 || index >= (BorderRightNs?.Length ?? 0)) return null;
			return BorderRightNs[index];
		}

		/// <summary>通过块内局部坐标设置右侧边框缓存</summary>
		public void SetBorderRight(int localRow, int localCol, string ns)
		{
			int index = localRow * BlockSizeX + localCol;
			if (index < 0 || index >= (BorderRightNs?.Length ?? 0)) return;
			BorderRightNs[index] = ns;
		}

		/// <summary>通过块内局部坐标获取底部边框缓存</summary>
		public string GetBorderBottom(int localRow, int localCol)
		{
			int index = localRow * BlockSizeX + localCol;
			if (index < 0 || index >= (BorderBottomNs?.Length ?? 0)) return null;
			return BorderBottomNs[index];
		}

		/// <summary>通过块内局部坐标设置底部边框缓存</summary>
		public void SetBorderBottom(int localRow, int localCol, string ns)
		{
			int index = localRow * BlockSizeX + localCol;
			if (index < 0 || index >= (BorderBottomNs?.Length ?? 0)) return;
			BorderBottomNs[index] = ns;
		}

		/// <summary>
		/// 检查是否有合并描述重定向此单元格
		/// </summary>
		/// <returns>合并描述，若无重定向则返回 null</returns>
		public XTableMergeDescriptor GetMergeRedirect(int localRow, int localCol)
		{
			if (MergeDescriptors == null || MergeDescriptors.Count == 0)
				return null;

			for (int i = 0; i < MergeDescriptors.Count; i++)
			{
				if (MergeDescriptors[i].Contains(localRow, localCol))
					return MergeDescriptors[i];
			}
			return null;
		}

		#region 序列化回调

		public void OnBeforeSerialize() { }

		public void OnAfterDeserialize()
		{
			// 反序列化时确保 BlockSize 有效（构造函数被跳过）
			if (BlockSizeX <= 0) BlockSizeX = XTableData.DefaultBlockSize;
			if (BlockSizeY <= 0) BlockSizeY = XTableData.DefaultBlockSize;
			// 确保数组和列表非空
			if (Cells == null) Cells = new XTableCellData[BlockSizeX * BlockSizeY];
			if (MergeDescriptors == null) MergeDescriptors = new List<XTableMergeDescriptor>();
			if (BorderRightNs == null || BorderRightNs.Length != BlockSizeX * BlockSizeY)
				BorderRightNs = new string[BlockSizeX * BlockSizeY];
			if (BorderBottomNs == null || BorderBottomNs.Length != BlockSizeX * BlockSizeY)
				BorderBottomNs = new string[BlockSizeX * BlockSizeY];
		}

		#endregion
	}
}
