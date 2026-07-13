using System.Collections.Generic;

using UnityEngine;

namespace XericUI.XTable.Core
{
	/// <summary>
	/// 表格四叉树构建用的单元格元数据。
	/// </summary>
	public struct XTableQuadtreeCell
	{
		public int Row;
		public int Col;
		public string Namespace;
	}

	/// <summary>
	/// 四叉树查询结果——一个均匀象限的可见部分。
	/// </summary>
	public struct QuadrantResult
	{
		/// <summary>该象限在表格像素空间中的区域（左上角坐标系，y 向下为正）</summary>
		public Rect Region;

		/// <summary>统一命名空间（空字符串 = 空区域）</summary>
		public string Namespace;

		/// <summary>该象限包含的单元格坐标列表</summary>
		public List<(int row, int col)> Cells;
	}

	/// <summary>
	/// 表格四叉树——基于单元格 <see cref="XTableQuadtreeCell.Namespace"/> 进行递归区域分割。
	/// 若一个象限内存在不同命名空间的单元格，则继续分裂，直到所有差异分开。
	/// 渲染阶段通过 <see cref="QueryVisible"/> 获取可见范围内的所有均匀象限。
	/// </summary>
	public class XTableQuadtree
	{
		#region 内部节点

		private class Node
		{
			/// <summary>当前象限在表格像素空间中的区域</summary>
			public Rect Region;

			/// <summary>
			/// null = 非均匀节点（有子节点），非 null = 均匀叶子节点。
			/// 空字符串 "" 表示该区域无任何单元格。
			/// </summary>
			public string UniformNamespace;

			/// <summary>子象限（null 表示叶子节点）</summary>
			public Node[] Children;

			/// <summary>该均匀象限包含的单元格坐标（仅叶子均匀节点有效）</summary>
			public List<(int, int)> Cells;

			public bool IsUniform => UniformNamespace != null;
		}

		#endregion

		#region 字段

		private Node m_Root;
		private float[] m_RowHeights;
		private float[] m_ColWidths;
		private int m_MaxCellsPerNode;

		/// <summary>四叉树是否已构建完成</summary>
		public bool IsBuilt { get; private set; }

		#endregion

		#region 常量

		#endregion

		#region 构建

		/// <summary>
		/// 全量构建四叉树。
		/// </summary>
		/// <param name="totalWidth">表格总宽度（像素）</param>
		/// <param name="totalHeight">表格总高度（像素）</param>
		/// <param name="rowHeights">行高数组（索引 = 行号）</param>
		/// <param name="colWidths">列宽数组（索引 = 列号）</param>
		/// <param name="cells">所有有数据的单元格元数据列表</param>
		public void Build(float totalWidth, float totalHeight,
			float[] rowHeights, float[] colWidths,
			List<XTableQuadtreeCell> cells, int maxCellsPerNode = 64)
		{
			if (rowHeights == null || colWidths == null || cells == null)
			{
				IsBuilt = false;
				return;
			}

			m_RowHeights = rowHeights;
			m_ColWidths = colWidths;
			m_MaxCellsPerNode = maxCellsPerNode;
			m_Root = new Node { Region = new Rect(0, 0, totalWidth, totalHeight) };

			int maxRow = rowHeights.Length - 1;
			int maxCol = colWidths.Length - 1;

			if (maxRow >= 0 && maxCol >= 0)
				BuildNode(m_Root, cells, 0, maxRow, 0, maxCol);
			else
				m_Root.UniformNamespace = "";

			IsBuilt = true;
		}

		/// <summary>强制重建（外部入口，通常由表格组件在数据/布局变更后调用）</summary>
		public void ForceRebuild(float totalWidth, float totalHeight,
			float[] rowHeights, float[] colWidths,
			List<XTableQuadtreeCell> cells, int maxCellsPerNode = 64)
		{
			Clear();
			Build(totalWidth, totalHeight, rowHeights, colWidths, cells, maxCellsPerNode);
		}

		private void BuildNode(Node node, List<XTableQuadtreeCell> allCells,
			int minRow, int maxRow, int minCol, int maxCol)
		{
			// 收集当前行列范围内的单元格
			var cellsInRange = new List<XTableQuadtreeCell>();
			for (int i = 0; i < allCells.Count; i++)
			{
				var cell = allCells[i];
				if (cell.Row >= minRow && cell.Row <= maxRow &&
					cell.Col >= minCol && cell.Col <= maxCol)
					cellsInRange.Add(cell);
			}

			// 空区域
			if (cellsInRange.Count == 0)
			{
				node.UniformNamespace = "";
				return;
			}

			// 检查是否所有单元格命名空间一致
			string firstNs = NormalizeNs(cellsInRange[0].Namespace);
			bool allSame = true;
			for (int i = 1; i < cellsInRange.Count; i++)
			{
				if (NormalizeNs(cellsInRange[i].Namespace) != firstNs)
				{
					allSame = false;
					break;
				}
			}

			if (allSame)
			{
				// 命名空间统一，但若空间跨度过大则继续分裂以便按块跳过不可见区域
				if (cellsInRange.Count > m_MaxCellsPerNode &&
				    (minRow < maxRow || minCol < maxCol))
				{
					// 继续执行分裂逻辑（不要 return）
				}
				else
				{
					node.UniformNamespace = firstNs;
					node.Cells = new List<(int, int)>(cellsInRange.Count);
					for (int i = 0; i < cellsInRange.Count; i++)
						node.Cells.Add((cellsInRange[i].Row, cellsInRange[i].Col));
					return;
				}
			}

			// 无法继续分裂（单行单列），强制标记为均匀
			if (minRow >= maxRow && minCol >= maxCol)
			{
				node.UniformNamespace = firstNs;
				node.Cells = new List<(int, int)>(cellsInRange.Count);
				for (int i = 0; i < cellsInRange.Count; i++)
					node.Cells.Add((cellsInRange[i].Row, cellsInRange[i].Col));
				return;
			}

			// 分裂：在行列索引空间取中点
			int midRow = (minRow + maxRow) / 2;
			int midCol = (minCol + maxCol) / 2;

			node.Children = new Node[4];

			// 左上 (minRow..midRow, minCol..midCol)
			node.Children[0] = MakeChild(minRow, midRow, minCol, midCol);
			BuildNode(node.Children[0], allCells, minRow, midRow, minCol, midCol);

			// 右上 (minRow..midRow, midCol+1..maxCol)
			node.Children[1] = MakeChild(minRow, midRow, midCol + 1, maxCol);
			BuildNode(node.Children[1], allCells, minRow, midRow, midCol + 1, maxCol);

			// 左下 (midRow+1..maxRow, minCol..midCol)
			node.Children[2] = MakeChild(midRow + 1, maxRow, minCol, midCol);
			BuildNode(node.Children[2], allCells, midRow + 1, maxRow, minCol, midCol);

			// 右下 (midRow+1..maxRow, midCol+1..maxCol)
			node.Children[3] = MakeChild(midRow + 1, maxRow, midCol + 1, maxCol);
			BuildNode(node.Children[3], allCells, midRow + 1, maxRow, midCol + 1, maxCol);
		}

		private Node MakeChild(int minRow, int maxRow, int minCol, int maxCol)
		{
			float x = GetColLeftX(minCol);
			float y = GetRowTopY(minRow);
			float w = GetColRightX(maxCol) - x;
			float h = GetRowBottomY(maxRow) - y;
			return new Node { Region = new Rect(x, y, w, h) };
		}

		#endregion

		#region 查询

		/// <summary>
		/// 查询可见矩形内的所有均匀象限。
		/// </summary>
		/// <param name="visibleRect">可见矩形（左上角坐标系）</param>
		/// <param name="results">结果列表（复用外部容器）</param>
		public void QueryVisible(Rect visibleRect, List<QuadrantResult> results)
		{
			if (results == null)
				results = new List<QuadrantResult>();
			if (m_Root == null)
				return;

			QueryNode(m_Root, visibleRect, results);
		}

		private void QueryNode(Node node, Rect visibleRect, List<QuadrantResult> results)
		{
			// 不相交则跳过
			if (!RectIntersects(node.Region, visibleRect))
				return;

			if (node.IsUniform)
			{
				// 裁剪到可见范围
				Rect clipped = RectIntersection(node.Region, visibleRect);
				if (clipped.width > 0 && clipped.height > 0)
				{
					results.Add(new QuadrantResult
					{
						Region = clipped,
						Namespace = node.UniformNamespace,
						Cells = node.Cells
					});
				}
				return;
			}

			// 非均匀节点：递归查询子节点
			if (node.Children != null)
			{
				for (int i = 0; i < node.Children.Length; i++)
				{
					if (node.Children[i] != null)
						QueryNode(node.Children[i], visibleRect, results);
				}
			}
		}

		#endregion

		#region 清理

		/// <summary>清空四叉树</summary>
		public void Clear()
		{
			m_Root = null;
			m_RowHeights = null;
			m_ColWidths = null;
			IsBuilt = false;
		}

		#endregion

		#region 坐标计算

		private float GetRowTopY(int row)
		{
			float y = 0;
			for (int i = 0; i < row && i < m_RowHeights.Length; i++)
				y += m_RowHeights[i];
			return y;
		}

		private float GetRowBottomY(int row)
		{
			float y = 0;
			for (int i = 0; i <= row && i < m_RowHeights.Length; i++)
				y += m_RowHeights[i];
			return y;
		}

		private float GetColLeftX(int col)
		{
			float x = 0;
			for (int i = 0; i < col && i < m_ColWidths.Length; i++)
				x += m_ColWidths[i];
			return x;
		}

		private float GetColRightX(int col)
		{
			float x = 0;
			for (int i = 0; i <= col && i < m_ColWidths.Length; i++)
				x += m_ColWidths[i];
			return x;
		}

		#endregion

		#region 工具

		/// <summary>命名空间规范化：null、空串、默认命名空间统一为 "xeric_table_default"</summary>
		private static string NormalizeNs(string ns)
		{
			if (string.IsNullOrEmpty(ns) || ns == "xeric_table_default")
				return "xeric_table_default";
			return ns;
		}

		private static bool RectIntersects(Rect a, Rect b)
		{
			return a.x < b.xMax && a.xMax > b.x &&
				   a.y < b.yMax && a.yMax > b.y;
		}

		private static Rect RectIntersection(Rect a, Rect b)
		{
			float x = Mathf.Max(a.x, b.x);
			float y = Mathf.Max(a.y, b.y);
			float xMax = Mathf.Min(a.xMax, b.xMax);
			float yMax = Mathf.Min(a.yMax, b.yMax);
			return new Rect(x, y, Mathf.Max(0, xMax - x), Mathf.Max(0, yMax - y));
		}

		#endregion
	}
}
