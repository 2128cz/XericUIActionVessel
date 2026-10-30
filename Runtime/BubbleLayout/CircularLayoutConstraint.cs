using System;
using System.Collections.Generic;
using UnityEngine;
using XericUI.Core;

namespace XericUI.BubbleLayout
{
	/// <summary>
	/// 环绕布局约束插件 —— 将气泡元素沿圆形排列，并通过弹簧力吸附到目标位置。
	/// </summary>
	[AddComponentMenu(XericRegistrationPaths.Layout + "/Circular Layout Constraint", 55)]
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

#if UNITY_EDITOR
		private struct GizmoSlot
		{
			public RectTransform rectTransform;
			public Vector2 size;
			public int index;
		}

		private void OnDrawGizmosSelected()
		{
			if (!isActiveAndEnabled || transform.childCount == 0)
				return;

			var slots = new List<GizmoSlot>();
			for (int i = 0; i < transform.childCount; i++)
			{
				var rt = transform.GetChild(i) as RectTransform;
				if (rt == null || !rt.gameObject.activeInHierarchy)
					continue;
				slots.Add(new GizmoSlot { rectTransform = rt, size = rt.rect.size, index = i });
			}
			if (slots.Count == 0) return;

			switch (m_SortMode)
			{
				case LayoutSortMode.PositionX:
					slots.Sort((a, b) => a.rectTransform.localPosition.x.CompareTo(b.rectTransform.localPosition.x));
					break;
				case LayoutSortMode.PositionY:
					slots.Sort((a, b) => b.rectTransform.localPosition.y.CompareTo(a.rectTransform.localPosition.y));
					break;
				case LayoutSortMode.Size:
					slots.Sort((a, b) => (b.size.x * b.size.y).CompareTo(a.size.x * a.size.y));
					break;
			}

			var parentRT = (RectTransform)transform;
			Vector2 center = new Vector2(
				parentRT.rect.width  * (0.5f - parentRT.pivot.x),
				parentRT.rect.height * (0.5f - parentRT.pivot.y));

			// 计算角度步长
			float angleStep;
			if (m_AngleMode == AngleMode.Auto)
			{
				angleStep = slots.Count > 1 ? 360f / slots.Count : 0f;
				if (slots.Count > 1)
				{
					float minRequired = m_MinAngle;
					for (int i = 0; i < slots.Count; i++)
					{
						float arc = Mathf.Atan2(slots[i].size.x * 0.5f, m_Radius) * 2f * Mathf.Rad2Deg;
						minRequired = Mathf.Max(minRequired, arc);
					}
					angleStep = Mathf.Max(angleStep, minRequired);
				}
			}
			else
			{
				angleStep = m_AngleStep;
			}

			Vector3 worldCenter = transform.TransformPoint(new Vector3(center.x, center.y, 0));

			// 绘制环绕参考圆
			Color circleColor = new Color(0.2f, 1f, 0.5f, 0.4f);
			Gizmos.color = circleColor;
			DrawWireCircle(worldCenter, m_Radius, 64);

			// 绘制元素目标位置标记
			Color slotColor = new Color(0.2f, 1f, 0.5f, 0.7f);

			for (int i = 0; i < slots.Count; i++)
			{
				var slot = slots[i];
				float angle = m_StartAngle + (m_Clockwise ? -1f : 1f) * angleStep * i;
				float rad = angle * Mathf.Deg2Rad;

				Vector2 targetLocal = center + new Vector2(Mathf.Cos(rad) * m_Radius, Mathf.Sin(rad) * m_Radius);
				Vector3 targetWorld = transform.TransformPoint(new Vector3(targetLocal.x, targetLocal.y, 0));

				// 小方框标记
				Vector3 slotScale = transform.TransformVector(new Vector3(slot.size.x * 0.3f, slot.size.y * 0.3f, 0));
				Gizmos.color = slotColor;
				Gizmos.DrawWireCube(targetWorld, slotScale);

				// 从圆心到目标的连线
				Gizmos.color = circleColor;
				Gizmos.DrawLine(worldCenter, targetWorld);
			}
		}

		private static void DrawWireCircle(Vector3 center, float radius, int segments)
		{
			float anglePerSegment = 360f / segments * Mathf.Deg2Rad;
			Vector3 prevPoint = center + new Vector3(Mathf.Cos(0) * radius, Mathf.Sin(0) * radius, 0);
			for (int i = 1; i <= segments; i++)
			{
				float a = anglePerSegment * i;
				Vector3 nextPoint = center + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0);
				Gizmos.DrawLine(prevPoint, nextPoint);
				prevPoint = nextPoint;
			}
		}
#endif
	}
}
