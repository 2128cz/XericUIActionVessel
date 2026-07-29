using System;

using UnityEngine;

namespace XericUI.XTable.Core
{
	/// <summary>
	/// 表格选择句柄——统一管理单选/多选状态。
	/// 外部可创建此句柄并通过表格的 SetSelectionHandle 或直接操作句柄来控制选区。
	/// 选区变更时触发 OnSelectionChanged 事件，表格监听此事件进行重绘。
	/// </summary>
	public class XTableSelectionHandle
	{
		#region 属性

		/// <summary>起始行（多选锚点行，单选当前行）</summary>
		public int StartRow { get; private set; } = -1;

		/// <summary>起始列（多选锚点列，单选当前列）</summary>
		public int StartCol { get; private set; } = -1;

		/// <summary>结束行（仅多选有效，等于 StartRow 时为单选）</summary>
		public int EndRow { get; private set; } = -1;

		/// <summary>结束列（仅多选有效，等于 StartCol 时为单选）</summary>
		public int EndCol { get; private set; } = -1;

		/// <summary>是否有有效选择</summary>
		public bool HasSelection => StartRow >= 0 && StartCol >= 0;

		/// <summary>是否为多选（起止不同）</summary>
		public bool HasMultiSelection =>
			HasSelection &&
			(StartRow != EndRow || StartCol != EndCol);

		/// <summary>选中区域最小行</summary>
		public int MinRow => HasSelection ? Mathf.Min(StartRow, EndRow) : -1;

		/// <summary>选中区域最大行</summary>
		public int MaxRow => HasSelection ? Mathf.Max(StartRow, EndRow) : -1;

		/// <summary>选中区域最小列</summary>
		public int MinCol => HasSelection ? Mathf.Min(StartCol, EndCol) : -1;

		/// <summary>选中区域最大列</summary>
		public int MaxCol => HasSelection ? Mathf.Max(StartCol, EndCol) : -1;

		#endregion

		#region 事件

		/// <summary>选区变化时触发</summary>
		public event Action<XTableSelectionHandle> OnSelectionChanged;

		#endregion

		#region 公开方法

		/// <summary>
		/// 单选一个单元格（同时清除多选范围）
		/// </summary>
		public void Select(int row, int col)
		{
			StartRow = row;
			StartCol = col;
			EndRow = row;
			EndCol = col;
			NotifyChanged();
		}

		/// <summary>
		/// 多选一个范围
		/// </summary>
		public void Select(int startRow, int startCol, int endRow, int endCol)
		{
			StartRow = startRow;
			StartCol = startCol;
			EndRow = endRow;
			EndCol = endCol;
			NotifyChanged();
		}

		/// <summary>一次性设置选区端点；仅在值变化时通知。</summary>
		public void SetEndpoints(int startRow, int startCol, int endRow, int endCol)
		{
			if (StartRow == startRow && StartCol == startCol && EndRow == endRow && EndCol == endCol) return;
			StartRow = startRow;
			StartCol = startCol;
			EndRow = endRow;
			EndCol = endCol;
			NotifyChanged();
		}

		/// <summary>
		/// 扩展多选范围到目标格（保持原有锚点）
		/// </summary>
		public void ExtendTo(int row, int col)
		{
			if (!HasSelection)
			{
				Select(row, col);
				return;
			}
			EndRow = row;
			EndCol = col;
			NotifyChanged();
		}

		/// <summary>
		/// 清除选择
		/// <summary>清除选择</summary>
		public void Clear()
		{
			if (!HasSelection) return;
			StartRow = -1;
			StartCol = -1;
			EndRow = -1;
			EndCol = -1;
			NotifyChanged();
		}

		/// <summary>将选区裁剪到 [0, maxRow] × [0, maxCol] 范围</summary>
		public void ClampTo(int maxRow, int maxCol)
		{
			if (!HasSelection) return;
			int oldStartRow = StartRow, oldStartCol = StartCol;
			int oldEndRow = EndRow, oldEndCol = EndCol;

			StartRow = Mathf.Clamp(StartRow, -1, maxRow);
			StartCol = Mathf.Clamp(StartCol, -1, maxCol);
			EndRow = Mathf.Clamp(EndRow, -1, maxRow);
			EndCol = Mathf.Clamp(EndCol, -1, maxCol);

			if (StartRow < 0 || StartCol < 0)
				Clear();
			else if (oldStartRow != StartRow || oldStartCol != StartCol ||
				 oldEndRow != EndRow || oldEndCol != EndCol)
				NotifyChanged();
		}

		/// <summary>
		/// 判断指定坐标是否在选中范围内
		/// </summary>
		public bool IsInRange(int row, int col)
		{
			if (!HasSelection) return false;
			int minR = Mathf.Min(StartRow, EndRow);
			int maxR = Mathf.Max(StartRow, EndRow);
			int minC = Mathf.Min(StartCol, EndCol);
			int maxC = Mathf.Max(StartCol, EndCol);
			return row >= minR && row <= maxR && col >= minC && col <= maxC;
		}

		/// <summary>左移一格</summary>
		public int MoveLeft(int maxCol = 0)
		{
			if (!HasSelection) return -1;
			int newCol = Mathf.Max(maxCol, StartCol - 1);
			if (newCol != StartCol)
				Select(StartRow, newCol);
			return newCol;
		}

		/// <summary>右移一格</summary>
		public int MoveRight(int maxCol)
		{
			if (!HasSelection) return -1;
			int newCol = Mathf.Min(maxCol, StartCol + 1);
			if (newCol != StartCol)
				Select(StartRow, newCol);
			return newCol;
		}

		/// <summary>上移一格</summary>
		public int MoveUp(int maxRow = 0)
		{
			if (!HasSelection) return -1;
			int newRow = Mathf.Max(maxRow, StartRow - 1);
			if (newRow != StartRow)
				Select(newRow, StartCol);
			return newRow;
		}

		/// <summary>下移一格</summary>
		public int MoveDown(int maxRow)
		{
			if (!HasSelection) return -1;
			int newRow = Mathf.Min(maxRow, StartRow + 1);
			if (newRow != StartRow)
				Select(newRow, StartCol);
			return newRow;
		}

		#endregion

		#region 内部

		private void NotifyChanged()
		{
			OnSelectionChanged?.Invoke(this);
		}

		#endregion
	}
}
