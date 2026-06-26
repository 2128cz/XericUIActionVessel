namespace XericUI.Core.StyleSheetUI
{
	/// <summary>
	/// UI 交互状态枚举——仿 Selectable.SelectionState（protected 无法外部引用）。
	/// 使用时通过 (StyleSheetSelectionState)(int)selectableState 转换。
	/// </summary>
	public enum StyleSheetSelectionState
	{
		/// <summary>正常状态</summary>
		Normal,

		/// <summary>高亮（鼠标悬停）状态</summary>
		Highlighted,

		/// <summary>按下状态</summary>
		Pressed,

		/// <summary>选中状态</summary>
		Selected,

		/// <summary>禁用状态</summary>
		Disabled,
	}
}
