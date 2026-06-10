using System;
using System.Collections.Generic;
using UnityEngine;

namespace XericUI.BubbleLayout
{
	/// <summary>
	/// 环绕布局约束插件 —— 将气泡元素沿圆形排列，并通过弹簧力吸附到目标位置。
	/// </summary>
	[AddComponentMenu("Xeric UI Vessel/Layout/Circular Layout Constraint", 55)]
	public class CircularLayoutConstraint : BubbleLayoutPluginBase, IBubbleConstraintPlugin
	{
		[Header("吸附力")]
		[Tooltip("弹簧吸附力倍率，值越大吸附越快")]
		[SerializeField] private float m_AttractionForce = 50f;

		[Header("环绕参数")]
		[Tooltip("环绕半径")]
		[SerializeField] private float m_Radius = 200f;

		[Tooltip("起始角度（度），0=右侧，90=上方")]
		[SerializeField] private float m_StartAngle = 0f;

		[Tooltip("角度模式")]
		[SerializeField] private AngleMode m_AngleMode = AngleMode.Auto;

		[Tooltip("自动角度模式下的角度间距（度）")]
		[SerializeField] private float m_AngleStep = 30f;

		[Tooltip("最小角度限制（度），防止元素因尺寸过大而重叠")]
		[SerializeField] private float m_MinAngle = 5f;

		[Tooltip("排列方向：顺时针还是逆时针")]
		[SerializeField] private bool m_Clockwise = true;

		[Header("排列")]
		[Tooltip("排序模式")]
		[SerializeField] private LayoutSortMode m_SortMode = LayoutSortMode.HierarchyOrder;

		public enum AngleMode
		{
			/// <summary>根据元素数量自动均分360度</summary>
			Auto,

			/// <summary>使用固定角度步长</summary>
			Fixed,
		}

		public void ProcessConstraint(List<BubbleItemData> items, Vector2 layoutCenter)
		{
			if (items == null || items.Count == 0)
				return;

			var sorted = new List<BubbleItemData>(items);
			SortItems(sorted);

			// 计算角度步长
			float angleStep;
			if (m_AngleMode == AngleMode.Auto)
			{
				angleStep = sorted.Count > 1 ? 360f / sorted.Count : 0f;

				// 检查最小角度限制：确保步长不小于最宽元素所需的角度
				if (sorted.Count > 1)
				{
					float minRequiredAngle = m_MinAngle;
					for (int i = 0; i < sorted.Count; i++)
					{
						// 元素在圆周上所占的近似角度
						float itemArcAngle = Mathf.Atan2(sorted[i].size.x * 0.5f, m_Radius) * 2f * Mathf.Rad2Deg;
						minRequiredAngle = Mathf.Max(minRequiredAngle, itemArcAngle);
					}
					angleStep = Mathf.Max(angleStep, minRequiredAngle);
				}
			}
			else
			{
				angleStep = m_AngleStep;
			}

			// 逐元素计算目标位置并施加弹簧力
			for (int i = 0; i < sorted.Count; i++)
			{
				var item = sorted[i];

				float angle = m_StartAngle + (m_Clockwise ? -1f : 1f) * angleStep * i;
				float rad = angle * Mathf.Deg2Rad;

				Vector2 targetPos = layoutCenter + new Vector2(
					Mathf.Cos(rad) * m_Radius,
					Mathf.Sin(rad) * m_Radius
				);

				Vector2 springForce = (targetPos - item.position) * m_AttractionForce;
				item.force += springForce;
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
