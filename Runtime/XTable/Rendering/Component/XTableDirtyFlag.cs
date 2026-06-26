using System;

namespace XericUI.XTable.Rendering.Component
{
	/// <summary>
	/// 脏标记类型——标识表格需要刷新的变更类型。
	/// </summary>
	[Flags]
	public enum TableDirtyType
	{
		None = 0,
		/// <summary>数据内容变更</summary>
		DataChanged = 1 << 0,
		/// <summary>视图范围变更（滚动/缩放）</summary>
		ViewChanged = 1 << 1,
		/// <summary>样式表变更</summary>
		StyleChanged = 1 << 2,
		/// <summary>选中状态变更</summary>
		SelectionChanged = 1 << 3,
		/// <summary>行列尺寸变更</summary>
		LayoutChanged = 1 << 4,
		/// <summary>全部脏（强制完整刷新）</summary>
		All = DataChanged | ViewChanged | StyleChanged | SelectionChanged | LayoutChanged
	}

	/// <summary>
	/// 脏标记结构——追踪表格的刷新状态。
	/// </summary>
	public struct XTableDirtyFlag
	{
		/// <summary>当前脏标记集合</summary>
		public TableDirtyType Flags;

		/// <summary>是否有任何脏标记</summary>
		public bool IsDirty => Flags != TableDirtyType.None;

		/// <summary>标记一种脏类型</summary>
		public void Mark(TableDirtyType type)
		{
			Flags |= type;
		}

		/// <summary>清除所有脏标记</summary>
		public void Clear()
		{
			Flags = TableDirtyType.None;
		}

		/// <summary>检查是否包含特定脏类型</summary>
		public bool Has(TableDirtyType type)
		{
			return (Flags & type) == type;
		}
	}
}
