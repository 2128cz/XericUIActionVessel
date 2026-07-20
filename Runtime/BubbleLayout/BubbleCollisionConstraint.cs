using System.Collections.Generic;
using UnityEngine;
using XericLibrary.Runtime;

namespace XericUI.BubbleLayout
{
	/// <summary>
	/// 气泡碰撞约束插件 —— 使用四叉树检测重叠并推挤分离。
	/// 默认 Order = 1000，在约束阶段最后执行，确保碰撞排挤在所有其他约束之后进行。
	/// </summary>
	[AddComponentMenu("Xeric UI Vessel/Layout/Bubble Collision Constraint", 52)]
	public class BubbleCollisionConstraint : BubbleLayoutPluginBase, IBubbleConstraintPlugin
	{
#if UNITY_EDITOR
		protected override int EditorDefaultOrder => 1000;
#endif

		[Tooltip("最大迭代次数，防止无限循环")]
		[SerializeField] private int m_MaxIterations = 10;

		[Tooltip("排斥力刚度系数")]
		[SerializeField] private float m_Stiffness = 1f;

		[Tooltip("最小分离距离（像素）")]
		[SerializeField] private float m_MinSeparation = 2f;

		[Tooltip("阻尼系数，每次迭代递减")]
		[SerializeField] private float m_IterationDamping = 0.85f;

		/// <summary>
		/// 最大迭代次数
		/// </summary>
		public int MaxIterations
		{
			get => m_MaxIterations;
			set => m_MaxIterations = Mathf.Max(1, value);
		}

		public void ProcessConstraint(List<BubbleItemData> items, Vector2 layoutCenter)
		{
			if (items == null || items.Count < 2)
				return;

			// 计算布局边界（包含所有元素的最小包围矩形 + 边距）
			Rect layoutBounds = CalculateLayoutBounds(items);

			// 为碰撞检测创建委托
			System.Func<BubbleItemData, Rect> getRectFunc = item => item.GetRect();

			float currentStiffness = m_Stiffness;

			for (int iteration = 0; iteration < m_MaxIterations; iteration++)
			{
				// 每轮迭代重建四叉树
				var quadTree = new QuadTree<BubbleItemData>(layoutBounds, getRectFunc, 
					maxObjectsPerNode: 5, maxDepth: 5);

				// 插入所有元素
				for (int i = 0; i < items.Count; i++)
				{
					quadTree.Insert(items[i]);
				}

				bool anyOverlap = false;
				var overlapSet = new HashSet<BubbleItemData>();

				// 检测所有重叠对
				for (int i = 0; i < items.Count; i++)
				{
					var itemA = items[i];
					Rect rectA = itemA.GetRect();

					// 使用四叉树查找与itemA重叠的元素
					overlapSet.Clear();
					quadTree.Retrieve(rectA, overlapSet);

					foreach (var itemB in overlapSet)
					{
						// 跳过自身
						if (ReferenceEquals(itemA, itemB))
							continue;

						// 确保每对只处理一次（利用HashSet的确定性避免重复处理）
						if (itemA.GetHashCode() > itemB.GetHashCode())
							continue;

						Rect rectB = itemB.GetRect();

						// 再次确认重叠
						if (!RectsOverlap(rectA, rectB))
							continue;

						anyOverlap = true;

						// 计算排斥力
						Vector2 force = BubblePhysicsSolver.CalculateRepulsionForce(
							itemA.position, itemA.size,
							itemB.position, itemB.size,
							currentStiffness);

						if (force != Vector2.zero)
						{
							// 两个元素各退一半
							itemA.position += force * 0.5f;
							itemB.position -= force * 0.5f;
						}
					}
				}

				if (!anyOverlap)
					break;

				// 阻尼递减
				currentStiffness *= m_IterationDamping;
			}
		}

		/// <summary>
		/// 计算所有元素的包围矩形
		/// </summary>
		private Rect CalculateLayoutBounds(List<BubbleItemData> items)
		{
			if (items.Count == 0)
				return new Rect(0, 0, 100, 100);

			float minX = float.MaxValue, minY = float.MaxValue;
			float maxX = float.MinValue, maxY = float.MinValue;

			for (int i = 0; i < items.Count; i++)
			{
				Rect r = items[i].GetRect();
				minX = Mathf.Min(minX, r.xMin);
				minY = Mathf.Min(minY, r.yMin);
				maxX = Mathf.Max(maxX, r.xMax);
				maxY = Mathf.Max(maxY, r.yMax);
			}

			// 添加边距
			float margin = 100f;
			return new Rect(minX - margin, minY - margin, 
				(maxX - minX) + margin * 2f, (maxY - minY) + margin * 2f);
		}

		private static bool RectsOverlap(Rect a, Rect b)
		{
			return a.xMin < b.xMax && a.xMax > b.xMin &&
				   a.yMin < b.yMax && a.yMax > b.yMin;
		}
	}
}
