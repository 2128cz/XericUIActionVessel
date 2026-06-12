using System;
using UnityEngine;

namespace XericUI.BubbleLayout
{
	/// <summary>
	/// 布局框定义 —— 描述一个在父级坐标空间中的矩形区域，
	/// 用作横向/竖向布局中特定轨道的目标区域。
	/// </summary>
	[Serializable]
	public class LayoutBox
	{
		/// <summary>
		/// 布局框名称（仅用于 Inspector 标识）
		/// </summary>
		[Tooltip("布局框名称")]
		public string Name = "Box";

		/// <summary>
		/// 此框在父级 RectTransform localPosition 空间中的矩形区域
		/// </summary>
		[Tooltip("布局框的矩形区域（localPosition 空间）")]
		public Rect Region = new Rect(-100, -100, 200, 200);

		/// <summary>
		/// 矩形区域内使用的枢轴点（0~1），用于计算子项在区域内的排列起点
		/// </summary>
		[Tooltip("区域内枢轴")]
		public Vector2 Pivot = new Vector2(0.5f, 0.5f);

		/// <summary>
		/// 每行/列的最大元素数量（0=不限制），仅对穿插模式有意义
		/// </summary>
		[Tooltip("每轨最大元素数，0=不限制")]
		public int MaxItemsPerTrack;

		public LayoutBox() { }

		public LayoutBox(string name)
		{
			Name = name;
		}

		public LayoutBox(string name, Rect region, Vector2 pivot = default)
		{
			Name = name;
			Region = region;
			Pivot = pivot == default ? new Vector2(0.5f, 0.5f) : pivot;
		}

		/// <summary>
		/// 获取 Region 在 localPosition 空间中的中心点
		/// </summary>
		public Vector2 GetCenter()
		{
			return Region.center;
		}

		/// <summary>
		/// 获取排列的起始锚点位置（根据 Pivot 在 Region 内的映射）
		/// </summary>
		public Vector2 GetAnchorPoint()
		{
			return new Vector2(
				Region.x + Region.width  * Pivot.x,
				Region.y + Region.height * Pivot.y);
		}
	}

	/// <summary>
	/// 元素到布局框的序列化映射项（仅用于 Editor 持久化）
	/// </summary>
	[Serializable]
	public struct LayoutBoxAssignment
	{
		/// <summary>目标子项的 Key（name_instanceID）</summary>
		public string ItemKey;

		/// <summary>分配的布局框索引</summary>
		public int BoxIndex;
	}
}
