using System.Collections.Generic;
using UnityEngine;

namespace XericUI.BubbleLayout
{
	/// <summary>
	/// 横排布局约束插件 —— 将气泡元素沿 X 轴排列，并通过弹簧力吸附到目标位置。
	/// 支持多布局框穿插排列。
	/// </summary>
	[AddComponentMenu("Xeric UI Vessel/Layout/Horizontal Layout Constraint", 54)]
	public class HorizontalLayoutConstraint : LinearLayoutConstraintBase
	{
		[Header("横排特有")]
		[Tooltip("垂直对齐方式：0=顶部, 1=居中, 2=底部")]
		[Range(0f, 2f)]
		[SerializeField] private float m_VerticalAlignment = 1f;

		[Tooltip("起始水平偏移（相对布局框区域中心）")]
		[SerializeField] private float m_StartOffsetX = 0f;

		#region LinearLayoutConstraintBase 实现

		/// <inheritdoc/>
		protected override string PrimaryAxisName => "X";

		/// <inheritdoc/>
		protected override bool IsPrimaryHorizontal => true;

		/// <inheritdoc/>
		protected override float GetPrimarySize(BubbleItemData item) => item.size.x;

		/// <inheritdoc/>
		protected override float GetPrimarySizeFromRT(RectTransform rt) => rt.rect.width;

		/// <inheritdoc/>
		protected override Vector2 MakeTargetPosition(float primary, float secondary)
		{
			return new Vector2(primary, secondary);
		}

		/// <inheritdoc/>
		protected override float GetSecondaryAlignOffset(BubbleItemData item, LayoutBox box)
		{
			// 垂直对齐偏移：0=顶部(top), 1=居中, 2=底部(bottom)
			// 在 localPosition 空间，Y 轴向上，所以 top = 正方向偏移
			float offset = (1f - m_VerticalAlignment) * (box.Region.height - item.size.y) * 0.5f;
			return offset;
		}

		#endregion

		#region Editor Gizmos

#if UNITY_EDITOR
		/// <inheritdoc/>
		protected override void DrawItemTargets(List<GizmoSlot> allSlots, Vector2 layoutCenter)
		{
			// 将 slots 按 boxIndex 分组
			var boxSlots = new List<GizmoSlot>[m_LayoutBoxes.Count];
			for (int b = 0; b < m_LayoutBoxes.Count; b++)
				boxSlots[b] = new List<GizmoSlot>();

			for (int i = 0; i < allSlots.Count; i++)
			{
				var slot = allSlots[i];
				int bIdx = Mathf.Clamp(slot.boxIndex, 0, m_LayoutBoxes.Count - 1);
				boxSlots[bIdx].Add(slot);
			}

			// 穿插迭代，计算每个 slot 的目标位置
			int[] indices = new int[boxSlots.Length];
			float[] advances = new float[boxSlots.Length];
			for (int b = 0; b < boxSlots.Length; b++)
			{
				float total = 0f;
				for (int i = 0; i < boxSlots[b].Count; i++)
					total += boxSlots[b][i].size.x + m_Spacing;
				if (boxSlots[b].Count > 0) total -= m_Spacing;
				advances[b] = m_LayoutBoxes[b].Region.center.x + m_StartOffsetX - total * 0.5f;
			}

			Color fillColor = new Color(1f, 0.6f, 0.1f, 0.15f);
			Color wireColor = new Color(1f, 0.55f, 0f, 0.7f);

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

					float alignOffset = (1f - m_VerticalAlignment) * (box.Region.height - slot.size.y) * 0.5f;
					float targetY = box.Region.center.y + alignOffset;
					float targetX = advances[b];

					Rect localRect = new Rect(
						targetX - slot.size.x * 0.5f,
						targetY - slot.size.y * 0.5f,
						slot.size.x,
						slot.size.y);

					DrawLocalRectAsWireCube(localRect, wireColor, fillColor);

					advances[b] += slot.size.x + m_Spacing;
					indices[b]++;
				}
			}
		}
#endif

		#endregion
	}
}
