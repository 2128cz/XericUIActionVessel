using System.Collections.Generic;
using UnityEngine;
using XericUI.Core;

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
	/// 支持多布局框穿插排列和拉链模式（一左一右交替）。
	/// </summary>
	[AddComponentMenu(XericRegistrationPaths.Layout + "/Vertical Layout Constraint", 53)]
	public class VerticalLayoutConstraint : LinearLayoutConstraintBase
	{
		[Header("竖排特有")]
		[Tooltip("起始竖直偏移（相对布局框区域中心）")]
		[SerializeField] private float m_StartOffsetY = 0f;

		[Header("拉链模式")]
		[Tooltip("启用拉链排列（偶数索引偏左，奇数索引偏右，仅对默认单框布局有效）")]
		[SerializeField] private bool m_ZipperMode = false;

		[Tooltip("拉链模式下左右间距")]
		[SerializeField] private float m_ZipperGap = 20f;

		#region LinearLayoutConstraintBase 实现

		/// <inheritdoc/>
		protected override string PrimaryAxisName => "Y";

		/// <inheritdoc/>
		protected override bool IsPrimaryHorizontal => false;

		/// <inheritdoc/>
		protected override float GetPrimarySize(BubbleItemData item) => item.size.y;

		/// <inheritdoc/>
		protected override float GetPrimarySizeFromRT(RectTransform rt) => rt.rect.height;

		/// <inheritdoc/>
		protected override Vector2 MakeTargetPosition(float primary, float secondary)
		{
			return new Vector2(secondary, primary);
		}

		/// <inheritdoc/>
		protected override float GetSecondaryAlignOffset(BubbleItemData item, LayoutBox box)
		{
			// 拉链偏移由外部交叉索引决定，这里返回 0
			return 0f;
		}

		#endregion

		#region 重写 ProcessConstraint 以支持拉链模式

		/// <inheritdoc/>
		public override void ProcessConstraint(List<BubbleItemData> items, Vector2 layoutCenter)
		{
			if (items == null || items.Count == 0)
				return;

			EnsureAtLeastOneBox();
			EnsureAssignmentsInitialized();

			// 分组
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

			// 排序各组
			for (int b = 0; b < boxGroups.Length; b++)
				SortItems(boxGroups[b]);

			// 计算各组总主轴尺寸及起始位置
			float[] boxTotalPrimary = new float[boxGroups.Length];
			float[] boxAdvance = new float[boxGroups.Length];
			for (int b = 0; b < boxGroups.Length; b++)
			{
				float total = 0f;
				for (int i = 0; i < boxGroups[b].Count; i++)
					total += boxGroups[b][i].size.y + m_Spacing;
				if (boxGroups[b].Count > 0) total -= m_Spacing;
				boxTotalPrimary[b] = total;
				boxAdvance[b] = m_LayoutBoxes[b].Region.center.y + m_StartOffsetY + total * 0.5f;
			}

			// 穿插迭代
			int[] boxIndices = new int[boxGroups.Length];
			int globalIndex = 0;
			bool anyRemaining = true;
			var targetList = new List<(BubbleItemData item, int boxIndex, Vector2 targetPos)>();

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

					// 辅轴位置：拉链模式下的 X 偏移
					float secondaryX = box.Region.center.x;
					if (m_ZipperMode)
					{
						secondaryX += (globalIndex % 2 == 0 ? -m_ZipperGap : m_ZipperGap) * 0.5f;
					}

					// 主轴位置推进：从上往下（Y 减小）
					float targetY = boxAdvance[b];
					Vector2 targetPos = new Vector2(secondaryX, targetY);
					targetPos = ClampToBox(targetPos, item, box);

					targetList.Add((item, b, targetPos));

					boxAdvance[b] -= item.size.y + m_Spacing;
					boxIndices[b]++;
					globalIndex++;
				}
			}

			// 施加力 / 瞬移
			for (int i = 0; i < targetList.Count; i++)
			{
				var (item, boxIndex, targetPos) = targetList[i];
				var box = m_LayoutBoxes[boxIndex];

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

		#endregion

		#region Editor Gizmos

#if UNITY_EDITOR
		/// <inheritdoc/>
		protected override void DrawItemTargets(List<GizmoSlot> allSlots, Vector2 layoutCenter)
		{
			var boxSlots = new List<GizmoSlot>[m_LayoutBoxes.Count];
			for (int b = 0; b < m_LayoutBoxes.Count; b++)
				boxSlots[b] = new List<GizmoSlot>();

			for (int i = 0; i < allSlots.Count; i++)
			{
				var slot = allSlots[i];
				int bIdx = Mathf.Clamp(slot.boxIndex, 0, m_LayoutBoxes.Count - 1);
				boxSlots[bIdx].Add(slot);
			}

			int[] indices = new int[boxSlots.Length];
			float[] advances = new float[boxSlots.Length];
			for (int b = 0; b < boxSlots.Length; b++)
			{
				float total = 0f;
				for (int i = 0; i < boxSlots[b].Count; i++)
					total += boxSlots[b][i].size.y + m_Spacing;
				if (boxSlots[b].Count > 0) total -= m_Spacing;
				advances[b] = m_LayoutBoxes[b].Region.center.y + m_StartOffsetY + total * 0.5f;
			}

			Color fillColor = new Color(0.1f, 0.7f, 1f, 0.15f);
			Color wireColor = new Color(0f, 0.6f, 1f, 0.7f);

			int globalIndex = 0;
			bool anyRemaining = true;
			while (anyRemaining)
			{
				anyRemaining = false;
				for (int b = 0; b < boxSlots.Length; b++)
				{
					if (indices[b] >= boxSlots[b].Count)
						continue;

					anyRemaining = true;
					var slot = boxSlots[b][indices[b]];
					var box = m_LayoutBoxes[b];

					float targetX = box.Region.center.x;
					if (m_ZipperMode)
						targetX += (globalIndex % 2 == 0 ? -m_ZipperGap : m_ZipperGap) * 0.5f;

					float targetY = advances[b];

					Rect localRect = new Rect(
						targetX - slot.size.x * 0.5f,
						targetY - slot.size.y * 0.5f,
						slot.size.x,
						slot.size.y);

					DrawLocalRectAsWireCube(localRect, wireColor, fillColor);

					advances[b] -= slot.size.y + m_Spacing;
					indices[b]++;
					globalIndex++;
				}
			}
		}
#endif

		#endregion
	}
}
