using System;
using System.Collections.Generic;
using UnityEngine;

namespace XericUI.BubbleLayout
{
	/// <summary>
	/// 布局排列排序模式
	/// </summary>
	public enum LayoutSortMode
	{
		/// <summary>按 Hierarchy 中的顺序</summary>
		HierarchyOrder,

		/// <summary>按当前 X 坐标排序</summary>
		PositionX,

		/// <summary>按当前 Y 坐标排序</summary>
		PositionY,

		/// <summary>按面积 (宽x高) 排序</summary>
		Size,
	}

	/// <summary>
	/// 竖排布局约束插件 —— 将气泡元素沿 Y 轴排列，并通过弹簧力吸附到目标位置。
	/// </summary>
	[AddComponentMenu("Xeric UI Vessel/Layout/Vertical Layout Constraint", 53)]
	public class VerticalLayoutConstraint : BubbleLayoutPluginBase, IBubbleConstraintPlugin
	{
		[Header("吸附力")]
		[Tooltip("弹簧吸附力倍率，值越大吸附越快")]
		[SerializeField] private float m_AttractionForce = 50f;

		[Header("排列")]
		[Tooltip("排序模式")]
		[SerializeField] private LayoutSortMode m_SortMode = LayoutSortMode.HierarchyOrder;

		[Tooltip("元素间距")]
		[SerializeField] private float m_Spacing = 10f;

		[Tooltip("起始偏移（相对于布局中心上方）")]
		[SerializeField] private float m_StartOffsetY = 0f;

		[Header("拉链模式")]
		[Tooltip("启用拉链排列（偶数索引偏左，奇数索引偏右）")]
		[SerializeField] private bool m_ZipperMode = false;

		[Tooltip("拉链模式下左右间距")]
		[SerializeField] private float m_ZipperGap = 20f;

		public void ProcessConstraint(List<BubbleItemData> items, Vector2 layoutCenter)
		{
			if (items == null || items.Count == 0)
				return;

			// 排序
			var sorted = new List<BubbleItemData>(items);
			SortItems(sorted);

			// 计算起始 Y
			float totalHeight = 0f;
			for (int i = 0; i < sorted.Count; i++)
				totalHeight += sorted[i].size.y + m_Spacing;
			totalHeight -= m_Spacing; // 去掉最后一个间距

			float startY = layoutCenter.y + m_StartOffsetY + totalHeight * 0.5f;

			// 逐元素设置目标位置并施加弹簧力
			float currentY = startY;
			for (int i = 0; i < sorted.Count; i++)
			{
				var item = sorted[i];
				float targetX = layoutCenter.x;
				float targetY = currentY;

				// 拉链模式：交替左右偏移
				if (m_ZipperMode)
				{
					targetX = layoutCenter.x + (i % 2 == 0 ? -m_ZipperGap : m_ZipperGap) * 0.5f;
				}

				Vector2 targetPos = new Vector2(targetX, targetY);

				// 弹簧力：F = (target - current) * forceMultiplier
				Vector2 springForce = (targetPos - item.position) * m_AttractionForce;
				item.force += springForce;

				currentY -= item.size.y + m_Spacing;
			}
		}

		private void SortItems(List<BubbleItemData> items)
		{
			switch (m_SortMode)
			{
				case LayoutSortMode.HierarchyOrder:
					// 保持原始顺序（即 rectChildren 的顺序）
					break;
				case LayoutSortMode.PositionX:
					items.Sort((a, b) => a.position.x.CompareTo(b.position.x));
					break;
				case LayoutSortMode.PositionY:
					items.Sort((a, b) => b.position.y.CompareTo(a.position.y)); // Y 从大到小（上到下）
					break;
				case LayoutSortMode.Size:
					items.Sort((a, b) => (b.size.x * b.size.y).CompareTo(a.size.x * a.size.y));
					break;
			}
		}
	}
}
