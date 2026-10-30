using System.Collections.Generic;
using UnityEngine;
using XericUI.Core;

namespace XericUI.BubbleLayout
{
	/// <summary>
	/// 边界限制模式
	/// </summary>
	public enum BoundaryShapeMode
	{
		/// <summary>矩形范围：尺寸的 x/y 代表矩形的宽度和高度</summary>
		Rectangle,

		/// <summary>椭圆范围：尺寸的 x/y 代表椭圆的横轴半径和纵轴半径</summary>
		Ellipse,
	}

	/// <summary>
	/// 边界限制约束插件 —— 将气泡元素限制在指定范围内。
	/// 支持矩形/椭圆两种形状，并记录超出范围的元素。
	/// </summary>
	[AddComponentMenu(XericRegistrationPaths.Layout + "/Boundary Constraint", 56)]
	public class BoundaryConstraint : BubbleLayoutPluginBase, IBubbleConstraintPlugin
	{
		[Header("限制范围")]
		[Tooltip("限制形状模式：矩形或椭圆")]
		[SerializeField] private BoundaryShapeMode m_ShapeMode = BoundaryShapeMode.Rectangle;

		[Tooltip("范围尺寸。矩形模式 = 宽x高；椭圆模式 = 横轴半径x纵轴半径")]
		[SerializeField] private Vector2 m_BoundarySize = new Vector2(400f, 300f);

		[Header("行为")]
		[Tooltip("是否在元素超出边界时将其瞬移到边界上（关闭则仅记录不修正）")]
		[SerializeField] private bool m_ClampToBoundary = true;

		#region 越界追踪

		[System.NonSerialized]
		private HashSet<int> m_OutOfBoundsObjects = new HashSet<int>();

		/// <summary>
		/// 记录越界元素：Key=GameObject实例ID，Value=true表示当前越界
		/// </summary>
		[System.NonSerialized]
		private Dictionary<int, bool> m_OutOfBoundsCache = new Dictionary<int, bool>();

		/// <summary>
		/// 检查指定 GameObject 是否超出了边界范围
		/// </summary>
		public bool IsOutOfBounds(GameObject target)
		{
			if (target == null)
				return false;
			return m_OutOfBoundsCache.TryGetValue(target.GetInstanceID(), out bool value) && value;
		}

		/// <summary>
		/// 获取当前所有越界 GameObject 的实例 ID 集合（只读）
		/// </summary>
		public IReadOnlyCollection<int> GetOutOfBoundsInstanceIDs()
		{
			return m_OutOfBoundsObjects;
		}

		/// <summary>
		/// 获取当前越界的元素数量
		/// </summary>
		public int GetOutOfBoundsCount() => m_OutOfBoundsObjects.Count;

		/// <summary>
		/// 清除越界记录
		/// </summary>
		public void ClearOutOfBoundsHistory()
		{
			m_OutOfBoundsObjects.Clear();
			m_OutOfBoundsCache.Clear();
		}

		#endregion

		#region IBubbleConstraintPlugin

#if UNITY_EDITOR
		protected override int EditorDefaultOrder => 500;
#endif

		public void ProcessConstraint(List<BubbleItemData> items, Vector2 layoutCenter)
		{
			if (items == null || items.Count == 0)
				return;

			// 本轮重置越界缓存
			m_OutOfBoundsObjects.Clear();
			m_OutOfBoundsCache.Clear();

			for (int i = 0; i < items.Count; i++)
			{
				var item = items[i];
				if (item.rectTransform == null)
					continue;

				int instanceId = item.rectTransform.GetInstanceID();
				bool outOfBounds = false;

				// 根据形状模式检测 item 矩形是否完全或部分越界
				switch (m_ShapeMode)
				{
					case BoundaryShapeMode.Rectangle:
						outOfBounds = !IsFullyInsideRect(
							item.position, item.size, layoutCenter, m_BoundarySize);
						break;

					case BoundaryShapeMode.Ellipse:
						outOfBounds = !IsFullyInsideEllipse(
							item.position, item.size, layoutCenter, m_BoundarySize);
						break;
				}

				m_OutOfBoundsCache[instanceId] = outOfBounds;

				if (outOfBounds)
				{
					m_OutOfBoundsObjects.Add(instanceId);

					if (m_ClampToBoundary)
					{
						// 将元素中心钳制到边界内
						item.position = ClampToBoundary(item.position, item.size, layoutCenter, m_BoundarySize, m_ShapeMode);
					}
				}
			}
		}

		#endregion

		#region 碰撞检测

		/// <summary>
		/// 检测矩形元素是否完全在矩形边界内
		/// </summary>
		private static bool IsFullyInsideRect(Vector2 center, Vector2 size, Vector2 boundaryCenter, Vector2 boundarySize)
		{
			float halfBW = boundarySize.x * 0.5f;
			float halfBH = boundarySize.y * 0.5f;
			float halfW  = size.x * 0.5f;
			float halfH  = size.y * 0.5f;

			return center.x - halfW >= boundaryCenter.x - halfBW
				&& center.x + halfW <= boundaryCenter.x + halfBW
				&& center.y - halfH >= boundaryCenter.y - halfBH
				&& center.y + halfH <= boundaryCenter.y + halfBH;
		}

		/// <summary>
		/// 检测矩形元素是否完全在椭圆边界内
		/// 椭圆方程：dx²/rx² + dy²/ry² ≤ 1 时需要元素所有角点均在椭圆内。
		/// 近似检测：元素中心 + 对角线半长构成的「外接圆」半径 vs 椭圆最大轴向半径。
		/// 精确检测：依次检查四个角点。
		/// </summary>
		private static bool IsFullyInsideEllipse(Vector2 center, Vector2 size, Vector2 boundaryCenter, Vector2 boundaryRadius)
		{
			float rx = boundaryRadius.x;
			float ry = boundaryRadius.y;

			if (rx <= 0f || ry <= 0f)
				return false;

			// 取矩形的四个角偏移量
			float halfW = size.x * 0.5f;
			float halfH = size.y * 0.5f;

			// 检查四个角点是否都在椭圆内
			return IsPointInsideEllipse(center.x - halfW, center.y - halfH, boundaryCenter, rx, ry)
				&& IsPointInsideEllipse(center.x + halfW, center.y - halfH, boundaryCenter, rx, ry)
				&& IsPointInsideEllipse(center.x - halfW, center.y + halfH, boundaryCenter, rx, ry)
				&& IsPointInsideEllipse(center.x + halfW, center.y + halfH, boundaryCenter, rx, ry);
		}

		/// <summary>
		/// 判断一个点是否在椭圆内：dx²/rx² + dy²/ry² ≤ 1
		/// </summary>
		private static bool IsPointInsideEllipse(float px, float py, Vector2 center, float rx, float ry)
		{
			float dx = (px - center.x) / rx;
			float dy = (py - center.y) / ry;
			return dx * dx + dy * dy <= 1f;
		}

		/// <summary>
		/// 将元素中心钳制到边界内（保留最大可能的尺寸）
		/// </summary>
		private static Vector2 ClampToBoundary(
			Vector2 center, Vector2 size, Vector2 boundaryCenter, Vector2 boundarySize, BoundaryShapeMode mode)
		{
			float halfW = size.x * 0.5f;
			float halfH = size.y * 0.5f;

			switch (mode)
			{
				case BoundaryShapeMode.Rectangle:
				{
					float halfBW = boundarySize.x * 0.5f;
					float halfBH = boundarySize.y * 0.5f;

					// 确保有足够空间容纳元素
					float maxW = Mathf.Max(halfBW - halfW, 0f);
					float maxH = Mathf.Max(halfBH - halfH, 0f);

					center.x = Mathf.Clamp(center.x, boundaryCenter.x - maxW, boundaryCenter.x + maxW);
					center.y = Mathf.Clamp(center.y, boundaryCenter.y - maxH, boundaryCenter.y + maxH);
					return center;
				}

				case BoundaryShapeMode.Ellipse:
				{
					float rx = boundarySize.x;
					float ry = boundarySize.y;

					if (rx <= halfW || ry <= halfH)
					{
						// 椭圆太小无法容纳元素，强制居中
						return boundaryCenter;
					}

					// 计算元素中心在椭圆内的有效范围
					float effectiveRx = rx - halfW;
					float effectiveRy = ry - halfH;

					// 将 center 投影到有效椭圆内
					float dx = center.x - boundaryCenter.x;
					float dy = center.y - boundaryCenter.y;

					// 椭圆约束：dx²/effectiveRx² + dy²/effectiveRy² ≤ 1
					if (effectiveRx > 0f && effectiveRy > 0f)
					{
						float nx = dx / effectiveRx;
						float ny = dy / effectiveRy;
						float dist = nx * nx + ny * ny;

						if (dist > 1f)
						{
							float scale = 1f / Mathf.Sqrt(dist);
							dx *= scale;
							dy *= scale;
						}
					}

					return boundaryCenter + new Vector2(dx, dy);
				}

				default:
					return center;
			}
		}

		#endregion

		#region Editor Gizmos

#if UNITY_EDITOR
		protected virtual void OnDrawGizmosSelected()
		{
			if (!isActiveAndEnabled)
				return;

			var parentRT = transform as RectTransform;
			Vector2 layoutCenter;
			if (parentRT != null)
			{
				layoutCenter = new Vector2(
					parentRT.rect.width  * (0.5f - parentRT.pivot.x),
					parentRT.rect.height * (0.5f - parentRT.pivot.y));
			}
			else
			{
				layoutCenter = Vector2.zero;
			}

			Vector3 worldCenter = transform.TransformPoint(new Vector3(layoutCenter.x, layoutCenter.y, 0));

			switch (m_ShapeMode)
			{
				case BoundaryShapeMode.Rectangle:
				{
					Vector3 worldSize = transform.TransformVector(new Vector3(m_BoundarySize.x, m_BoundarySize.y, 0));
					Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.25f);
					Gizmos.DrawCube(worldCenter, worldSize);
					Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.7f);
					Gizmos.DrawWireCube(worldCenter, worldSize);
					break;
				}

				case BoundaryShapeMode.Ellipse:
				{
					float rx = m_BoundarySize.x;
					float ry = m_BoundarySize.y;
					int segments = 64;
					float step = 360f / segments * Mathf.Deg2Rad;

					Vector3 prevPoint = worldCenter + new Vector3(Mathf.Cos(0) * rx, Mathf.Sin(0) * ry, 0);

					// 半透明椭圆盘
					Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.12f);
					for (int i = 1; i <= segments; i++)
					{
						float angle = step * i;
						Vector3 nextPoint = worldCenter + new Vector3(Mathf.Cos(angle) * rx, Mathf.Sin(angle) * ry, 0);
						Gizmos.DrawLine(worldCenter, nextPoint); // 辐线填充
						prevPoint = nextPoint;
					}

					// 椭圆边框
					prevPoint = worldCenter + new Vector3(Mathf.Cos(0) * rx, Mathf.Sin(0) * ry, 0);
					Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.7f);
					for (int i = 1; i <= segments; i++)
					{
						float angle = step * i;
						Vector3 nextPoint = worldCenter + new Vector3(Mathf.Cos(angle) * rx, Mathf.Sin(angle) * ry, 0);
						Gizmos.DrawLine(prevPoint, nextPoint);
						prevPoint = nextPoint;
					}
					break;
				}
			}
		}
#endif

		#endregion
	}
}
