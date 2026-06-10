using System.Collections.Generic;
using UnityEngine;

namespace XericUI.BubbleLayout
{
	/// <summary>
	/// 横排布局约束插件 —— 将气泡元素沿 X 轴排列，并通过弹簧力吸附到目标位置。
	/// </summary>
	[AddComponentMenu("Xeric UI Vessel/Layout/Horizontal Layout Constraint", 54)]
	public class HorizontalLayoutConstraint : BubbleLayoutPluginBase, IBubbleConstraintPlugin
	{
		[Header("吸附力")]
		[Tooltip("弹簧吸附力倍率，值越大吸附越快")]
		[SerializeField] private float m_AttractionForce = 50f;

		[Header("排列")]
		[Tooltip("排序模式")]
		[SerializeField] private LayoutSortMode m_SortMode = LayoutSortMode.HierarchyOrder;

		[Tooltip("元素间距")]
		[SerializeField] private float m_Spacing = 10f;

		[Tooltip("起始偏移（相对于布局中心左侧）")]
		[SerializeField] private float m_StartOffsetX = 0f;

		[Tooltip("垂直对齐方式：0=顶部, 1=居中, 2=底部")]
		[Range(0f, 2f)]
		[SerializeField] private float m_VerticalAlignment = 1f;

		public void ProcessConstraint(List<BubbleItemData> items, Vector2 layoutCenter)
		{
			if (items == null || items.Count == 0)
				return;

			var sorted = new List<BubbleItemData>(items);
			SortItems(sorted);

			// 计算总宽度
			float totalWidth = 0f;
			for (int i = 0; i < sorted.Count; i++)
				totalWidth += sorted[i].size.x + m_Spacing;
			totalWidth -= m_Spacing;

			float startX = layoutCenter.x + m_StartOffsetX - totalWidth * 0.5f;

			float currentX = startX;
			for (int i = 0; i < sorted.Count; i++)
			{
				var item = sorted[i];

				// 垂直对齐
				float alignOffset = (m_VerticalAlignment - 1f) * item.size.y * 0.5f;
				float targetY = layoutCenter.y - alignOffset;

				Vector2 targetPos = new Vector2(currentX, targetY);

				Vector2 springForce = (targetPos - item.position) * m_AttractionForce;
				item.force += springForce;

				currentX += item.size.x + m_Spacing;
			}
		}

		private void SortItems(List<BubbleItemData> items)
		{
			switch (m_SortMode)
			{
				case LayoutSortMode.HierarchyOrder:
					break;
				case LayoutSortMode.PositionX:
					items.Sort((a, b) => a.position.x.CompareTo(b.position.x));
					break;
				case LayoutSortMode.PositionY:
					items.Sort((a, b) => b.position.y.CompareTo(a.position.y));
					break;
				case LayoutSortMode.Size:
					items.Sort((a, b) => (b.size.x * b.size.y).CompareTo(a.size.x * a.size.y));
					break;
			}
		}
	}
}
