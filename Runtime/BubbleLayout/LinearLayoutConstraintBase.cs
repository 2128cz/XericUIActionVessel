using System;
using System.Collections.Generic;
using UnityEngine;

namespace XericUI.BubbleLayout
{
	/// <summary>
	/// 横向 / 竖向线性布局约束插件的共用基类。
	/// 提供布局框管理、元素分组排序、穿插迭代、弹簧吸附/瞬移、Editor Gizmo 框架等复用逻辑。
	/// </summary>
	public abstract class LinearLayoutConstraintBase : BubbleLayoutPluginBase, IBubbleConstraintPlugin
	{
		#region 字段

		[Header("吸附力")]
		[Tooltip("弹簧吸附力倍率，值越大吸附越快")]
		[SerializeField] protected float m_AttractionForce = 50f;

		[Header("排列")]
		[Tooltip("排序模式")]
		[SerializeField] protected LayoutSortMode m_SortMode = LayoutSortMode.HierarchyOrder;

		[Tooltip("元素间距")]
		[SerializeField] protected float m_Spacing = 10f;

		[Header("布局框")]
		[Tooltip("布局框列表（默认拥有一个全区域框）。每个框代表一个独立排列轨道，子项按框穿插排列。")]
		[SerializeField] protected List<LayoutBox> m_LayoutBoxes = new List<LayoutBox>
		{
			new LayoutBox("Default", new Rect(-200, -200, 400, 400)),
		};

		/// <summary>
		/// 元素到布局框的分配表（运行时字典的序列化后备）
		/// </summary>
		[SerializeField] private List<LayoutBoxAssignment> m_SerializedAssignments = new List<LayoutBoxAssignment>();

		[NonSerialized]
		protected Dictionary<string, int> m_BoxAssignments;

		#endregion

		#region 属性

		/// <summary>主轴名称（"X" / "Y"），用于子类在 Gizmo / 日志中标识</summary>
		protected abstract string PrimaryAxisName { get; }

		/// <summary>获取元素沿主轴方向的尺寸</summary>
		protected abstract float GetPrimarySize(BubbleItemData item);

		/// <summary>
		/// 获取元素沿主轴方向的尺寸（用于 Gizmo 预览的 RectTransform 版本）
		/// </summary>
		protected abstract float GetPrimarySizeFromRT(RectTransform rt);

		/// <summary>
		/// 根据主轴/辅轴坐标合成最终 localPosition
		/// </summary>
		protected abstract Vector2 MakeTargetPosition(float primary, float secondary);

		#endregion

		#region 布局框管理

		/// <summary>
		/// 惰性初始化运行时字典
		/// </summary>
		protected void EnsureAssignmentsInitialized()
		{
			if (m_BoxAssignments != null)
				return;

			m_BoxAssignments = new Dictionary<string, int>();
			for (int i = 0; i < m_SerializedAssignments.Count; i++)
			{
				var entry = m_SerializedAssignments[i];
				if (!string.IsNullOrEmpty(entry.ItemKey))
					m_BoxAssignments[entry.ItemKey] = entry.BoxIndex;
			}
		}

		/// <summary>
		/// 确保布局框列表至少有一项
		/// </summary>
		protected void EnsureAtLeastOneBox()
		{
			if (m_LayoutBoxes == null)
				m_LayoutBoxes = new List<LayoutBox>();
			if (m_LayoutBoxes.Count == 0)
				m_LayoutBoxes.Add(new LayoutBox("Default", new Rect(-200, -200, 400, 400)));
		}

		/// <summary>
		/// 将指定 GameObject 分配到指定编号的布局框。
		/// 默认所有子项属于第 0 号框。
		/// </summary>
		/// <param name="boxIndex">布局框索引（从 0 开始），超出范围会被钳制</param>
		/// <param name="target">要分配的子项 GameObject</param>
		public void MarkObject2LayoutBox(int boxIndex, GameObject target)
		{
			if (target == null)
				return;

			EnsureAtLeastOneBox();
			boxIndex = Mathf.Clamp(boxIndex, 0, m_LayoutBoxes.Count - 1);
			EnsureAssignmentsInitialized();

			string key = $"{target.name}_{target.GetInstanceID()}";
			m_BoxAssignments[key] = boxIndex;

			// 同步回序列化列表
			SyncDictionaryToSerialized();
		}

		/// <summary>
		/// 清除指定 GameObject 的布局框分配（恢复默认框 0）
		/// </summary>
		public void ClearBoxAssignment(GameObject target)
		{
			if (target == null)
				return;

			EnsureAssignmentsInitialized();
			string key = $"{target.name}_{target.GetInstanceID()}";
			m_BoxAssignments.Remove(key);
			SyncDictionaryToSerialized();
		}

		/// <summary>
		/// 根据屏幕空间坐标和指定 Pivot，将布局框的 Region 映射到屏幕对应位置。
		/// 适用于 Canvas ScreenSpace-Overlay / Camera 模式。
		/// </summary>
		/// <param name="boxIndex">目标布局框索引</param>
		/// <param name="screenRect">屏幕空间矩形（像素坐标）</param>
		/// <param name="camera">渲染 Canvas 的相机，ScreenSpace-Overlay 时传 null</param>
		public void SetBoxRegionFromScreenSpace(int boxIndex, Rect screenRect, Camera camera = null)
		{
			EnsureAtLeastOneBox();
			if (boxIndex < 0 || boxIndex >= m_LayoutBoxes.Count)
				return;

			var parentRT = transform as RectTransform;
			if (parentRT == null)
				return;

			Vector2 localCenter;
			RectTransformUtility.ScreenPointToLocalPointInRectangle(
				parentRT, screenRect.center, camera, out localCenter);

			Vector2 localSize = screenRect.size / parentRT.lossyScale;

			m_LayoutBoxes[boxIndex].Region = new Rect(
				localCenter.x - localSize.x * 0.5f,
				localCenter.y - localSize.y * 0.5f,
				localSize.x,
				localSize.y);
		}

		/// <summary>
		/// 获取指定 GameObject 当前分配的布局框索引
		/// </summary>
		public int GetBoxIndex(GameObject target)
		{
			if (target == null)
				return 0;

			EnsureAssignmentsInitialized();
			string key = $"{target.name}_{target.GetInstanceID()}";
			return m_BoxAssignments.TryGetValue(key, out int idx) ? idx : 0;
		}

		/// <summary>
		/// 获取所有布局框（只读）
		/// </summary>
		public IReadOnlyList<LayoutBox> GetLayoutBoxes()
		{
			EnsureAtLeastOneBox();
			return m_LayoutBoxes;
		}

		private void SyncDictionaryToSerialized()
		{
			m_SerializedAssignments.Clear();
			if (m_BoxAssignments == null)
				return;

			foreach (var kv in m_BoxAssignments)
			{
				m_SerializedAssignments.Add(new LayoutBoxAssignment
				{
					ItemKey = kv.Key,
					BoxIndex = kv.Value,
				});
			}
		}

		#endregion

		#region IBubbleConstraintPlugin 实现

		public virtual void ProcessConstraint(List<BubbleItemData> items, Vector2 layoutCenter)
		{
			if (items == null || items.Count == 0)
				return;

			EnsureAtLeastOneBox();
			EnsureAssignmentsInitialized();

			// 1. 将元素按布局框分组
			var boxGroups = new List<BubbleItemData>[m_LayoutBoxes.Count];
			for (int b = 0; b < m_LayoutBoxes.Count; b++)
				boxGroups[b] = new List<BubbleItemData>();

			for (int i = 0; i < items.Count; i++)
			{
				var item = items[i];
				int boxIdx = 0;
				if (item.rectTransform != null)
				{
					string key = $"{item.rectTransform.name}_{item.rectTransform.GetInstanceID()}";
					if (m_BoxAssignments.TryGetValue(key, out int assigned))
						boxIdx = Mathf.Clamp(assigned, 0, m_LayoutBoxes.Count - 1);
				}
				boxGroups[boxIdx].Add(item);
			}

			// 2. 各组内部排序
			for (int b = 0; b < boxGroups.Length; b++)
				SortItems(boxGroups[b]);

			// 3. 计算各组总主轴尺寸
			float[] boxTotalPrimarySize = new float[boxGroups.Length];
			float[] boxAdvance = new float[boxGroups.Length];
			for (int b = 0; b < boxGroups.Length; b++)
			{
				float total = 0f;
				for (int i = 0; i < boxGroups[b].Count; i++)
					total += GetPrimarySize(boxGroups[b][i]) + m_Spacing;
				if (boxGroups[b].Count > 0)
					total -= m_Spacing;
				boxTotalPrimarySize[b] = total;
			}

			// 4. 计算各框的起始位置（主轴方向），位于 Region 中心偏前
			for (int b = 0; b < boxGroups.Length; b++)
			{
				var region = m_LayoutBoxes[b].Region;
				float primaryCenter = IsPrimaryHorizontal
					? region.center.x
					: region.center.y;

				boxAdvance[b] = primaryCenter - boxTotalPrimarySize[b] * 0.5f;
			}

			// 5. 穿插迭代：按轨轮询各框
			int[] boxIndices = new int[boxGroups.Length];
			bool anyRemaining = true;
			var targetPositions = new List<(BubbleItemData item, int boxIndex, Vector2 targetPos)>();

			while (anyRemaining)
			{
				anyRemaining = false;
				for (int b = 0; b < boxGroups.Length; b++)
				{
					if (boxIndices[b] >= boxGroups[b].Count)
						continue;

					anyRemaining = true;
					var item = boxGroups[b][boxIndices[b]];
					var box = m_LayoutBoxes[b];

					// 计算辅轴位置（box 区域中心 ± 对齐偏移）
					float secondary = IsPrimaryHorizontal
						? box.Region.center.y + GetSecondaryAlignOffset(item, box)
						: box.Region.center.x + GetSecondaryAlignOffset(item, box);

					Vector2 targetPos = MakeTargetPosition(boxAdvance[b], secondary);

					// 确保目标在 box 区域内
					targetPos = ClampToBox(targetPos, item, box);

					targetPositions.Add((item, b, targetPos));

					// 推进主轴位置
					boxAdvance[b] += GetPrimarySize(item) + m_Spacing;
					boxIndices[b]++;
				}
			}

			// 6. 按计算顺序依次施加力（确保穿插顺序被物理系统感知）
			for (int i = 0; i < targetPositions.Count; i++)
			{
				var (item, boxIndex, targetPos) = targetPositions[i];
				var box = m_LayoutBoxes[boxIndex];

				// 检查是否超出 box 允许范围，超出则瞬移
				if (IsOutOfBox(item.position, item, box))
				{
					item.position = targetPos;
					item.velocity = Vector2.zero;
				}
				else
				{
					Vector2 springForce = (targetPos - item.position) * m_AttractionForce;
					item.force += springForce;
				}
			}
		}

		/// <summary>
		/// 判断主轴是水平还是垂直
		/// </summary>
		protected abstract bool IsPrimaryHorizontal { get; }

		/// <summary>
		/// 子类提供辅轴对齐偏移量（如横排的垂直对齐、竖排的拉链偏移）
		/// </summary>
		/// <returns>偏移量（正值 = 远离区域左上角方向）</returns>
		protected virtual float GetSecondaryAlignOffset(BubbleItemData item, LayoutBox box)
		{
			return 0f;
		}

		/// <summary>
		/// 将目标位置钳制到布局框区域内（考虑元素自身尺寸）
		/// </summary>
		protected Vector2 ClampToBox(Vector2 target, BubbleItemData item, LayoutBox box)
		{
			Rect r = box.Region;
			float halfW = item.size.x * 0.5f;
			float halfH = item.size.y * 0.5f;

			target.x = Mathf.Clamp(target.x, r.xMin + halfW, r.xMax - halfW);
			target.y = Mathf.Clamp(target.y, r.yMin + halfH, r.yMax - halfH);
			return target;
		}

		/// <summary>
		/// 判断位置是否超出布局框（用于瞬移判断）
		/// </summary>
		protected bool IsOutOfBox(Vector2 pos, BubbleItemData item, LayoutBox box)
		{
			Rect r = box.Region;
			float halfW = item.size.x * 0.5f;
			float halfH = item.size.y * 0.5f;

			return pos.x - halfW < r.xMin || pos.x + halfW > r.xMax
				|| pos.y - halfH < r.yMin || pos.y + halfH > r.yMax;
		}

		/// <summary>
		/// 排序元素列表
		/// </summary>
		protected void SortItems(List<BubbleItemData> items)
		{
			if (items.Count <= 1)
				return;

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

		#endregion

		#region Editor Gizmos

#if UNITY_EDITOR
		/// <summary>
		/// Gizmo 预览数据结构
		/// </summary>
		protected struct GizmoSlot
		{
			public RectTransform rectTransform;
			public Vector2 size;
			public int index;
			public int boxIndex;
		}

		protected virtual void OnDrawGizmosSelected()
		{
			if (!isActiveAndEnabled || transform.childCount == 0)
				return;

			// 收集子元素
			var allSlots = new List<GizmoSlot>();
			for (int i = 0; i < transform.childCount; i++)
			{
				var rt = transform.GetChild(i) as RectTransform;
				if (rt == null || !rt.gameObject.activeInHierarchy)
					continue;
				allSlots.Add(new GizmoSlot { rectTransform = rt, size = rt.rect.size, index = i, boxIndex = 0 });
			}
			if (allSlots.Count == 0) return;

			// 读回分配
			EnsureAssignmentsInitialized();
			for (int i = 0; i < allSlots.Count; i++)
			{
				var slot = allSlots[i];
				string key = $"{slot.rectTransform.name}_{slot.rectTransform.GetInstanceID()}";
				if (m_BoxAssignments.TryGetValue(key, out int bIdx))
				{
					slot.boxIndex = Mathf.Clamp(bIdx, 0, m_LayoutBoxes.Count - 1);
					allSlots[i] = slot;
				}
			}

			// 按当前排序模式排序
			switch (m_SortMode)
			{
				case LayoutSortMode.PositionX:
					allSlots.Sort((a, b) => a.rectTransform.localPosition.x.CompareTo(b.rectTransform.localPosition.x));
					break;
				case LayoutSortMode.PositionY:
					allSlots.Sort((a, b) => b.rectTransform.localPosition.y.CompareTo(a.rectTransform.localPosition.y));
					break;
				case LayoutSortMode.Size:
					allSlots.Sort((a, b) => (b.size.x * b.size.y).CompareTo(a.size.x * a.size.y));
					break;
			}

			var parentRT = (RectTransform)transform;
			Vector2 layoutCenter = new Vector2(
				parentRT.rect.width  * (0.5f - parentRT.pivot.x),
				parentRT.rect.height * (0.5f - parentRT.pivot.y));

			// 1. 绘制所有布局框区域
			DrawBoxRegions();

			// 2. 委托子类绘制元素目标位置
			DrawItemTargets(allSlots, layoutCenter);
		}

		/// <summary>
		/// 绘制所有布局框的矩形区域（白色虚线框）
		/// </summary>
		protected virtual void DrawBoxRegions()
		{
			Color[] boxColors =
			{
				new Color(0.5f, 0.5f, 1f, 0.35f),   // 蓝色调
				new Color(1f, 0.5f, 0.5f, 0.35f),   // 红色调
				new Color(0.5f, 1f, 0.5f, 0.35f),   // 绿色调
				new Color(1f, 1f, 0.5f, 0.35f),     // 黄色调
				new Color(0.5f, 1f, 1f, 0.35f),     // 青色调
				new Color(1f, 0.5f, 1f, 0.35f),     // 紫色调
			};

			for (int b = 0; b < m_LayoutBoxes.Count; b++)
			{
				var box = m_LayoutBoxes[b];
				Color c = boxColors[b % boxColors.Length];

				Rect r = box.Region;
				Vector3 centerWorld = transform.TransformPoint(new Vector3(r.center.x, r.center.y, 0));
				Vector3 sizeWorld = transform.TransformVector(new Vector3(r.width, r.height, 0));

				// 半透明填充
				Gizmos.color = new Color(c.r, c.g, c.b, 0.08f);
				Gizmos.DrawCube(centerWorld, sizeWorld);

				// 虚线边框
				Gizmos.color = c;
				DrawWireRect(centerWorld, sizeWorld);

				// 中心十字
				float crossLen = Mathf.Min(r.width, r.height) * 0.08f;
				Vector3 cxWorld = transform.TransformVector(new Vector3(crossLen, 0, 0));
				Vector3 cyWorld = transform.TransformVector(new Vector3(0, crossLen, 0));
				Gizmos.DrawLine(centerWorld - cxWorld, centerWorld + cxWorld);
				Gizmos.DrawLine(centerWorld - cyWorld, centerWorld + cyWorld);
			}
		}

		/// <summary>
		/// 子类重写：根据已排序的 slots 和 layoutCenter 绘制各元素的目标位置预览
		/// </summary>
		protected abstract void DrawItemTargets(List<GizmoSlot> allSlots, Vector2 layoutCenter);

		/// <summary>
		/// 绘制一个线框矩形
		/// </summary>
		protected static void DrawWireRect(Vector3 center, Vector3 size)
		{
			Vector3 half = size * 0.5f;
			Vector3[] corners = new Vector3[5]
			{
				center + new Vector3(-half.x, -half.y, 0),
				center + new Vector3( half.x, -half.y, 0),
				center + new Vector3( half.x,  half.y, 0),
				center + new Vector3(-half.x,  half.y, 0),
				center + new Vector3(-half.x, -half.y, 0),
			};
			for (int i = 0; i < 4; i++)
				Gizmos.DrawLine(corners[i], corners[i + 1]);
		}

		/// <summary>
		/// 将 localPosition 空间的 Rect 绘制为世界空间线框
		/// </summary>
		protected void DrawLocalRectAsWireCube(Rect localRect, Color wireColor, Color? fillColor = null)
		{
			Vector3 worldCenter = transform.TransformPoint(new Vector3(localRect.center.x, localRect.center.y, 0));
			Vector3 worldSize = transform.TransformVector(new Vector3(localRect.width, localRect.height, 0));

			if (fillColor.HasValue)
			{
				Gizmos.color = fillColor.Value;
				Gizmos.DrawCube(worldCenter, worldSize);
			}

			Gizmos.color = wireColor;
			Gizmos.DrawWireCube(worldCenter, worldSize);
		}
#endif

		#endregion
	}
}
