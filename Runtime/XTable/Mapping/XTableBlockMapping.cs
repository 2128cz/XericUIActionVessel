using System.Runtime.CompilerServices;

using XericLibrary.Runtime.MacroLibrary;

namespace XericUI.XTable.Mapping
{
	/// <summary>
	/// 块映射工具——将块坐标与 Z 曲线索引进行相互转换。
	/// 内部调用 <see cref="MacroCurveMapping"/> 的 Morton Code 编码/解码方法。
	/// Z 曲线索引避免纯横向排列产生的空缺问题，提升空间局部性。
	/// </summary>
	public static class XTableBlockMapping
	{
		/// <summary>
		/// 块坐标 → Z 曲线索引 (Morton Code)
		/// </summary>
		/// <param name="blockRow">块行号 (非负)</param>
		/// <param name="blockCol">块列号 (非负)</param>
		/// <returns>64 位 Z 曲线索引，用作块字典的 key</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong BlockCoordToZIndex(int blockRow, int blockCol)
		{
			return MacroCurveMapping.ZOrderEncode(blockCol, blockRow);
		}

		/// <summary>
		/// Z 曲线索引 → 块坐标
		/// </summary>
		/// <param name="zIndex">Z 曲线索引</param>
		/// <param name="blockRow">解码后的块行号</param>
		/// <param name="blockCol">解码后的块列号</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void ZIndexToBlockCoord(ulong zIndex, out int blockRow, out int blockCol)
		{
			MacroCurveMapping.ZOrderDecode(zIndex, out blockCol, out blockRow);
		}
	}
}
