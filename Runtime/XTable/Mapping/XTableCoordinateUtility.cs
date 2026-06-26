#if ENABLE_BURST
using Unity.Burst;
#endif

using System.Runtime.CompilerServices;

namespace XericUI.XTable.Mapping
{
	/// <summary>
	/// 表格坐标计算工具——提供单元格坐标 ↔ 块坐标 的转换方法。
	/// 所有方法支持 Burst 编译加速。
	/// </summary>
#if ENABLE_BURST
	[BurstCompile]
#endif
	public static class XTableCoordinateUtility
	{
		/// <summary>
		/// 将全局单元格坐标 (row, col) 转换为 块坐标 + 块内局部坐标。
		/// blockRow = row / blockSizeY, blockCol = col / blockSizeX
		/// localRow = row % blockSizeY, localCol = col % blockSizeX
		/// </summary>
#if ENABLE_BURST
		[BurstCompile]
#endif
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CellToBlockCoordinate(
			int row, int col, int blockSizeX, int blockSizeY,
			out int blockRow, out int blockCol,
			out int localRow, out int localCol)
		{
			blockRow = row / blockSizeY;
			blockCol = col / blockSizeX;
			localRow = row % blockSizeY;
			localCol = col % blockSizeX;
		}

		/// <summary>
		/// 计算块内的线性索引。
		/// index = localRow * blockSizeX + localCol
		/// </summary>
#if ENABLE_BURST
		[BurstCompile]
#endif
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int LocalCellIndex(int localRow, int localCol, int blockSizeX)
		{
			return localRow * blockSizeX + localCol;
		}

		/// <summary>
		/// 从块内线性索引反算局部行列坐标。
		/// </summary>
#if ENABLE_BURST
		[BurstCompile]
#endif
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void LocalCellFromIndex(int index, int blockSizeX,
			out int localRow, out int localCol)
		{
			localRow = index / blockSizeX;
			localCol = index % blockSizeX;
		}

		/// <summary>
		/// 从全局行列坐标直接计算块内线性索引。
		/// </summary>
#if ENABLE_BURST
		[BurstCompile]
#endif
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int GlobalCellToLocalIndex(int row, int col, int blockSizeX, int blockSizeY)
		{
			int localRow = row % blockSizeY;
			int localCol = col % blockSizeX;
			return localRow * blockSizeX + localCol;
		}
	}
}
