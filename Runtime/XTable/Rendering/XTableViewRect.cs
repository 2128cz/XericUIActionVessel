using UnityEngine;

namespace XericUI.XTable.Rendering
{
	/// <summary>
	/// 左上角坐标系的视图矩形。
	/// Unity 的 Rect 是左下角原点(Y-up)，表格数据索引是左上角原点(Y-down)。
	/// 此类负责坐标转换和可见单元格范围计算。
	/// </summary>
	public struct XTableViewRect
	{
		/// <summary>左上角 X 坐标</summary>
		public float X;

		/// <summary>左上角 Y 坐标（从上到下递增）</summary>
		public float Y;

		/// <summary>视图宽度</summary>
		public float Width;

		/// <summary>视图高度</summary>
		public float Height;

		/// <summary>右边界</summary>
		public float XMax => X + Width;

		/// <summary>下边界</summary>
		public float YMax => Y + Height;

		/// <summary>
		/// 从 Unity Rect（左下角原点）转换为 TableViewRect（左上角原点）
		/// </summary>
		/// <param name="unityRect">Unity 坐标系下的矩形</param>
		/// <param name="containerHeight">容器总高度（用于 Y 轴翻转）</param>
		public static XTableViewRect FromUnityRect(Rect unityRect, float containerHeight)
		{
			return new XTableViewRect
			{
				X = unityRect.xMin,
				Y = containerHeight - unityRect.yMax,
				Width = unityRect.width,
				Height = unityRect.height
			};
		}

		/// <summary>
		/// 转换为 Unity Rect（左下角原点）
		/// </summary>
		public Rect ToUnityRect(float containerHeight)
		{
			return new Rect(X, containerHeight - Y - Height, Width, Height);
		}

		/// <summary>
		/// 通过行列尺寸计算可见单元格范围。
		/// 累加行高/列宽直到超出视图范围。
		/// </summary>
		/// <param name="rowHeights">每行高度数组</param>
		/// <param name="colWidths">每列宽度数组</param>
		/// <param name="startRow">可见起始行（包含）</param>
		/// <param name="endRow">可见结束行（不包含）</param>
		/// <param name="startCol">可见起始列（包含）</param>
		/// <param name="endCol">可见结束列（不包含）</param>
		public void GetVisibleCellRange(
			float[] rowHeights, float[] colWidths,
			out int startRow, out int endRow,
			out int startCol, out int endCol)
		{
			startRow = 0;
			endRow = 0;
			startCol = 0;
			endCol = 0;

			if (rowHeights == null || colWidths == null) return;

			// 计算行范围（Y 方向，从上到下）
			float accumulatedY = 0f;
			for (int r = 0; r < rowHeights.Length; r++)
			{
				float rowBottom = accumulatedY + rowHeights[r];
				if (rowBottom > Y && accumulatedY < YMax)
				{
					if (startRow == 0 && accumulatedY < YMax)
						startRow = r;
					endRow = r + 1;
				}
				accumulatedY = rowBottom;
				if (accumulatedY > YMax) break;
			}

			// 计算列范围（X 方向，从左到右）
			float accumulatedX = 0f;
			for (int c = 0; c < colWidths.Length; c++)
			{
				float colRight = accumulatedX + colWidths[c];
				if (colRight > X && accumulatedX < XMax)
				{
					if (startCol == 0 && accumulatedX < XMax)
						startCol = c;
					endCol = c + 1;
				}
				accumulatedX = colRight;
				if (accumulatedX > XMax) break;
			}
		}
	}
}
