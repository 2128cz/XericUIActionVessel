using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;
using XericLibrary.Runtime.Type.SpatialAlgorithm;

#if UNITY_EDITOR
using System.Diagnostics;
#endif

namespace XericUI.BubbleLayout
{
#if UNITY_EDITOR
	/// <summary>
	/// 气泡布局各阶段性能耗时数据（仅 Editor 可用）
	/// </summary>
	public struct BubbleLayoutProfiler
	{
		/// <summary>同步子元素耗时(ms)</summary>
		public double SyncTimeMs;

		/// <summary>动画阶段耗时(ms)</summary>
		public double AnimationTimeMs;

		/// <summary>物理积分耗时(ms)</summary>
		public double PhysicsTimeMs;

		/// <summary>约束阶段耗时(ms)</summary>
		public double ConstraintTimeMs;

		/// <summary>碰撞排挤耗时(ms)</summary>
		public double CollisionTimeMs;

		/// <summary>位置应用耗时(ms)</summary>
		public double ApplyTimeMs;

		/// <summary>总耗时(ms)</summary>
		public double TotalTimeMs;

		/// <summary>当前元素数量</summary>
		public int ItemCount;

		/// <summary>碰撞迭代次数</summary>
		public int CollisionIterations;

		/// <summary>动画插件数量</summary>
		public int AnimationPluginCount;

		/// <summary>约束插件数量</summary>
		public int ConstraintPluginCount;
	}
#endif
	/// <summary>
	/// 气泡布局组件 —— 基于物理模拟的2D UI布局。
	/// 子元素按顺序添加，通过四叉树检测重叠并以迭代推挤方式排布。
	/// 支持动画插件和约束插件，插件按 Order 优先级分阶段顺序执行。
	/// </summary>
	[ExecuteAlways]
	[AddComponentMenu("Xeric UI Vessel/Layout/Bubble Layout Group", 51)]
	public class BubbleLayoutGroup : LayoutGroup
	{
		#region 序列化字段

		[Header("插件")]
		[Tooltip("是否启用插件系统")]
		[SerializeField] private bool m_EnablePlugins = true;

		[Tooltip("速度阻尼系数 (0=无阻尼, 1=完全停止)")]
		[SerializeField] private float m_PhysicsDamping = 0.9f;

		[Tooltip("默认质量")]
		[SerializeField] private float m_PhysicsMass = 1f;

		[Tooltip("质量随 UI 面积等比例变化（面积越大，质量越大，移动越慢）")]
		[SerializeField] private bool m_MassScaleWithArea = false;

		[Tooltip("物理时间步长（0表示使用Time.deltaTime）")]
		[SerializeField] private float m_PhysicsDeltaTime = 0f;

		[Tooltip("最大迭代次数")]
		[SerializeField] private int m_MaxIterations = 10;

		[Tooltip("排斥力刚度系数")]
		[SerializeField] private float m_Stiffness = 1f;

		[Tooltip("最小分离距离")]
		[SerializeField] private float m_MinSeparation = 2f;

		[Tooltip("每次迭代的阻尼递减系数")]
		[SerializeField] private float m_IterationDamping = 0.85f;

		[Header("布局")]
		[Tooltip("布局时忽略不可见的成员")]
		[SerializeField] private bool m_LayoutIgnoreInactive = true;

		#endregion

		#region 属性

		/// <summary>最大迭代次数</summary>
		public int MaxIterations
		{
			get => m_MaxIterations;
			set => m_MaxIterations = Mathf.Max(1, value);
		}

		/// <summary>排斥力刚度</summary>
		public float Stiffness
		{
			get => m_Stiffness;
			set => m_Stiffness = Mathf.Max(0, value);
		}

		/// <summary>最小分离距离</summary>
		public float MinSeparation
		{
			get => m_MinSeparation;
			set => m_MinSeparation = Mathf.Max(0, value);
		}

		/// <summary>物理速度阻尼</summary>
		public float PhysicsDamping
		{
			get => m_PhysicsDamping;
			set => m_PhysicsDamping = Mathf.Clamp01(value);
		}

		#endregion

		#region 私有字段

		// 气泡元素数据
		private List<BubbleItemData> m_Items = new List<BubbleItemData>();
		private Dictionary<string, int> m_ItemIndexMap = new Dictionary<string, int>();

		// 插件缓存
		private List<IBubbleAnimationPlugin> m_AnimationPlugins = new List<IBubbleAnimationPlugin>();
		private List<IBubbleConstraintPlugin> m_ConstraintPlugins = new List<IBubbleConstraintPlugin>();
		private bool m_PluginsDirty = true;

		// 四叉树（复用）
		private QuadTree<BubbleItemData> m_QuadTree;
		private Func<BubbleItemData, Rect> m_GetRectFunc;

		// 布局状态
		private bool m_LayoutInProgress;

#if UNITY_EDITOR
		/// <summary>上一帧性能数据（仅 Editor）</summary>
		public BubbleLayoutProfiler LastProfilerData { get; private set; }

		private Stopwatch m_ProfilerStopwatch;
#endif

		#endregion

		#region Unity 生命周期

		protected BubbleLayoutGroup()
		{ }

		protected override void Awake()
		{
			base.Awake();
			m_GetRectFunc = item => item.GetRect();
		}

		protected override void OnEnable()
		{
			base.OnEnable();
			m_PluginsDirty = true;
			LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
		}

		protected override void OnDisable()
		{
			m_Tracker.Clear();
			base.OnDisable();
		}

		protected override void OnTransformChildrenChanged()
		{
			base.OnTransformChildrenChanged();
			m_PluginsDirty = true; // 子对象变化可能引入新插件
		}

#if UNITY_EDITOR
		protected override void Reset()
		{
			m_EnablePlugins = true;
			m_PhysicsDamping = 0.9f;
			m_PhysicsMass = 1f;
			m_MassScaleWithArea = false;
			m_PhysicsDeltaTime = 0f;
			m_MaxIterations = 10;
			m_Stiffness = 1f;
			m_MinSeparation = 2f;
			m_IterationDamping = 0.85f;
			m_LayoutIgnoreInactive = true;
		}

		protected virtual void Update()
		{
			if (!Application.isPlaying)
			{
				// 编辑时实时预览：标记布局重建
				LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
			}
		}
#endif

		#endregion

		#region LayoutGroup 重写

		/// <summary>
		/// 计算水平布局输入
		/// </summary>
		public override void CalculateLayoutInputHorizontal()
		{
			base.CalculateLayoutInputHorizontal();
			RefreshChildList();
			CalcTotalMinAndPreferredSize(0);
		}

		/// <summary>
		/// 计算垂直布局输入
		/// </summary>
		public override void CalculateLayoutInputVertical()
		{
			CalcTotalMinAndPreferredSize(1);
		}

		/// <summary>
		/// 设置水平布局 —— 在此执行完整的2D布局管线
		/// </summary>
		public override void SetLayoutHorizontal()
		{
			if (m_LayoutInProgress)
				return;

			m_LayoutInProgress = true;

			try
			{
				RunLayoutPipeline();
			}
			finally
			{
				m_LayoutInProgress = false;
			}
		}

		/// <summary>
		/// 设置垂直布局 —— 已在 SetLayoutHorizontal 中完成，此处为空
		/// </summary>
		public override void SetLayoutVertical()
		{
			// 2D 布局已在 SetLayoutHorizontal 中完成
		}

		#endregion

		#region 布局管线

		/// <summary>
		/// 执行完整的布局管线：
		/// 收集子项 → 动画阶段 → 物理积分 → 约束阶段 → 碰撞排挤 → 应用位置
		/// </summary>
		private void RunLayoutPipeline()
		{
#if UNITY_EDITOR
			if (m_ProfilerStopwatch == null)
				m_ProfilerStopwatch = new Stopwatch();
			var profiler = new BubbleLayoutProfiler();
			var totalSw = Stopwatch.StartNew();
#endif

			// 1. 同步子元素列表
#if UNITY_EDITOR
			m_ProfilerStopwatch.Restart();
#endif
			SyncItemList();
#if UNITY_EDITOR
			m_ProfilerStopwatch.Stop();
			profiler.SyncTimeMs = m_ProfilerStopwatch.Elapsed.TotalMilliseconds;
#endif

			if (m_Items.Count == 0)
			{
#if UNITY_EDITOR
				profiler.ItemCount = 0;
				totalSw.Stop();
				profiler.TotalTimeMs = totalSw.Elapsed.TotalMilliseconds;
				LastProfilerData = profiler;
#endif
				return;
			}

			// 2. 刷新插件列表
			if (m_PluginsDirty)
				RefreshPlugins();

			// 3. 动画阶段 —— 插件施加力/速度
			if (m_EnablePlugins)
			{
				float dt = GetDeltaTime();
#if UNITY_EDITOR
				m_ProfilerStopwatch.Restart();
#endif
				RunAnimationPhase(dt);
#if UNITY_EDITOR
				m_ProfilerStopwatch.Stop();
				profiler.AnimationTimeMs = m_ProfilerStopwatch.Elapsed.TotalMilliseconds;
#endif
			}

			// 4. 物理积分阶段
			{
				// 更新每元素质量
				if (m_MassScaleWithArea)
				{
					for (int i = 0; i < m_Items.Count; i++)
					{
						var item = m_Items[i];
						item.mass = m_PhysicsMass * (item.size.x * item.size.y);
					}
				}
				else
				{
					for (int i = 0; i < m_Items.Count; i++)
					{
						m_Items[i].mass = m_PhysicsMass;
					}
				}

#if UNITY_EDITOR
				m_ProfilerStopwatch.Restart();
#endif
				float dt = GetDeltaTime();
				BubblePhysicsSolver.Integrate(m_Items, dt, m_PhysicsDamping);
#if UNITY_EDITOR
				m_ProfilerStopwatch.Stop();
				profiler.PhysicsTimeMs = m_ProfilerStopwatch.Elapsed.TotalMilliseconds;
#endif
			}

			// 5. 约束阶段 —— 插件限制位置
			if (m_EnablePlugins)
			{
				var center = GetLayoutCenter();
#if UNITY_EDITOR
				m_ProfilerStopwatch.Restart();
#endif
				RunConstraintPhase(center);
#if UNITY_EDITOR
				m_ProfilerStopwatch.Stop();
				profiler.ConstraintTimeMs = m_ProfilerStopwatch.Elapsed.TotalMilliseconds;
#endif
			}

			// 6. 气泡碰撞排挤阶段
#if UNITY_EDITOR
			m_ProfilerStopwatch.Restart();
#endif
			int collisionIterations = RunBubbleCollision();
#if UNITY_EDITOR
			m_ProfilerStopwatch.Stop();
			profiler.CollisionTimeMs = m_ProfilerStopwatch.Elapsed.TotalMilliseconds;
			profiler.CollisionIterations = collisionIterations;
#endif

			// 7. 将最终位置应用回 RectTransform
#if UNITY_EDITOR
			m_ProfilerStopwatch.Restart();
#endif
			ApplyPositions();
#if UNITY_EDITOR
			m_ProfilerStopwatch.Stop();
			profiler.ApplyTimeMs = m_ProfilerStopwatch.Elapsed.TotalMilliseconds;
#endif

#if UNITY_EDITOR
			totalSw.Stop();
			profiler.TotalTimeMs = totalSw.Elapsed.TotalMilliseconds;
			profiler.ItemCount = m_Items.Count;
			profiler.AnimationPluginCount = m_EnablePlugins ? m_AnimationPlugins.Count : 0;
			profiler.ConstraintPluginCount = m_EnablePlugins ? m_ConstraintPlugins.Count : 0;
			LastProfilerData = profiler;
#endif
		}

		/// <summary>
		/// 获取布局中心点（localPosition 空间）
		/// localPosition (0,0) = pivot 位置，此方法返回 rect 中心相对 pivot 的偏移
		/// </summary>
		private Vector2 GetLayoutCenter()
		{
			var rt = rectTransform;
			return new Vector2(
				rt.rect.width  * (0.5f - rt.pivot.x),
				rt.rect.height * (0.5f - rt.pivot.y));
		}

		/// <summary>
		/// 获取时间步长
		/// </summary>
		private float GetDeltaTime()
		{
			if (m_PhysicsDeltaTime > 0f)
				return m_PhysicsDeltaTime;
			return Application.isPlaying ? Time.deltaTime : 0.016f;
		}

		#endregion

		#region 子元素管理

		/// <summary>
		/// 刷新子元素列表（筛选有效子项）
		/// </summary>
		private void RefreshChildList()
		{
			rectChildren.Clear();
			var toIgnoreList = ListPool<Component>.Get();

			for (int i = 0; i < rectTransform.childCount; i++)
			{
				var rect = rectTransform.GetChild(i) as RectTransform;
				if (rect == null)
					continue;

				if (m_LayoutIgnoreInactive && !rect.gameObject.activeInHierarchy)
					continue;

				rect.GetComponents(typeof(ILayoutIgnorer), toIgnoreList);
				if (toIgnoreList.Count > 0)
				{
					bool ignored = false;
					for (int j = 0; j < toIgnoreList.Count; j++)
					{
						if (((ILayoutIgnorer)toIgnoreList[j]).ignoreLayout)
						{
							ignored = true;
							break;
						}
					}
					if (ignored)
						continue;
				}

				rectChildren.Add(rect);
			}

			ListPool<Component>.Release(toIgnoreList);
			m_Tracker.Clear();
		}

		/// <summary>
		/// 同步 BubbleItemData 列表：
		/// 新增、更新现有、移除已不存在的子项
		/// </summary>
		private void SyncItemList()
		{
			// 标记所有现有项为 "未匹配"
			var unmatchedKeys = new HashSet<string>(m_ItemIndexMap.Keys);

			// 遍历当前有效子项
			for (int i = 0; i < rectChildren.Count; i++)
			{
				var rt = rectChildren[i];
				string key = $"{rt.name}_{rt.GetInstanceID()}";

				if (m_ItemIndexMap.TryGetValue(key, out int idx))
				{
					// 已存在，更新尺寸和旋转
					var item = m_Items[idx];
					item.size = rt.rect.size;
					item.rotation = rt.localEulerAngles.z;
					item.rectTransform = rt;
					unmatchedKeys.Remove(key);
				}
				else
				{
					// 新增
					var newItem = BubbleItemData.FromRectTransform(rt);
					m_Items.Add(newItem);
					m_ItemIndexMap[key] = m_Items.Count - 1;
				}
			}

			// 移除已不存在的子项
			foreach (var key in unmatchedKeys)
			{
				if (m_ItemIndexMap.TryGetValue(key, out int idx))
				{
					// 交换删除法
					int lastIdx = m_Items.Count - 1;
					if (idx != lastIdx)
					{
						m_Items[idx] = m_Items[lastIdx];
						m_ItemIndexMap[m_Items[idx].Key] = idx;
					}
					m_Items.RemoveAt(lastIdx);
					m_ItemIndexMap.Remove(key);
				}
			}
		}

		/// <summary>
		/// 计算总体最小和首选尺寸
		/// </summary>
		private void CalcTotalMinAndPreferredSize(int axis)
		{
			float min = 0f;
			float preferred = 0f;

			for (int i = 0; i < rectChildren.Count; i++)
			{
				float childMin = LayoutUtility.GetMinSize(rectChildren[i], axis);
				float childPreferred = LayoutUtility.GetPreferredSize(rectChildren[i], axis);

				if (axis == 0)
				{
					// 水平：所有子项宽度之和
					min += childMin;
					preferred += childPreferred;
				}
				else
				{
					// 垂直：取最大高度
					min = Mathf.Max(min, childMin);
					preferred = Mathf.Max(preferred, childPreferred);
				}
			}

			// 加上内边距
			if (axis == 0)
			{
				min += padding.horizontal;
				preferred += padding.horizontal;
			}
			else
			{
				min += padding.vertical;
				preferred += padding.vertical;
			}

			SetLayoutInputForAxis(min, preferred, -1, axis);
		}

		#endregion

		#region 插件管理

		/// <summary>
		/// 扫描 GameObject 上的插件组件
		/// </summary>
		private void RefreshPlugins()
		{
			m_AnimationPlugins.Clear();
			m_ConstraintPlugins.Clear();

			var components = GetComponents<MonoBehaviour>();
			for (int i = 0; i < components.Length; i++)
			{
				var comp = components[i];
				if (comp == null || comp == this)
					continue;

				if (comp is IBubbleAnimationPlugin animPlugin)
					m_AnimationPlugins.Add(animPlugin);

				if (comp is IBubbleConstraintPlugin constraintPlugin)
					m_ConstraintPlugins.Add(constraintPlugin);
			}

			// 按 Order 排序（升序，值小先执行）
			m_AnimationPlugins.Sort((a, b) => a.Order.CompareTo(b.Order));
			m_ConstraintPlugins.Sort((a, b) => a.Order.CompareTo(b.Order));

			m_PluginsDirty = false;
		}

		/// <summary>
		/// 手动标记插件列表需要刷新
		/// </summary>
		public void MarkPluginsDirty()
		{
			m_PluginsDirty = true;
		}

		/// <summary>
		/// 运行动画阶段
		/// </summary>
		private void RunAnimationPhase(float deltaTime)
		{
			for (int i = 0; i < m_AnimationPlugins.Count; i++)
			{
				if (!m_AnimationPlugins[i].Enabled)
					continue;
				m_AnimationPlugins[i].ProcessAnimation(m_Items, deltaTime);
			}
		}

		/// <summary>
		/// 运行约束阶段
		/// </summary>
		private void RunConstraintPhase(Vector2 layoutCenter)
		{
			for (int i = 0; i < m_ConstraintPlugins.Count; i++)
			{
				if (!m_ConstraintPlugins[i].Enabled)
					continue;
				m_ConstraintPlugins[i].ProcessConstraint(m_Items, layoutCenter);
			}
		}

		#endregion

		#region 气泡碰撞排挤

		/// <summary>
		/// 使用四叉树检测重叠并通过迭代推挤消除所有重叠
		/// </summary>
		private int RunBubbleCollision()
		{
			if (m_Items.Count < 2)
				return 0;

			// 计算布局边界并创建/复用四叉树
			Rect layoutBounds = CalculateLayoutBounds();
			if (m_QuadTree == null || m_QuadTree.RootRect != layoutBounds)
			{
				m_QuadTree = new QuadTree<BubbleItemData>(layoutBounds, m_GetRectFunc,
					maxObjectsPerNode: 5, maxDepth: 5);
			}
			else
			{
				m_QuadTree.Clear();
			}

			float currentStiffness = m_Stiffness;
			var overlapSet = new HashSet<BubbleItemData>();

			for (int iteration = 0; iteration < m_MaxIterations; iteration++)
			{
				// 重建四叉树
				m_QuadTree.Rebuild(m_Items);

				bool anyOverlap = false;

				for (int i = 0; i < m_Items.Count; i++)
				{
					var itemA = m_Items[i];
					Rect rectA = itemA.GetRect();

					// 查询与 itemA 重叠的元素
					overlapSet.Clear();
					m_QuadTree.Retrieve(rectA, overlapSet);

					foreach (var itemB in overlapSet)
					{
						if (ReferenceEquals(itemA, itemB))
							continue;

						// 确保每对只处理一次
						if (itemA.GetHashCode() > itemB.GetHashCode())
							continue;

						Rect rectB = itemB.GetRect();

						// 计算实际重叠量
						if (!RectsOverlap(rectA, rectB))
							continue;

						anyOverlap = true;

						// 计算排斥位移
						Vector2 displacement = CalculateRepulsionDisplacement(
							itemA.position, itemA.size,
							itemB.position, itemB.size,
							currentStiffness);

						// 两元素各退一半
						itemA.position += displacement * 0.5f;
						itemB.position -= displacement * 0.5f;
					}
				}

				if (!anyOverlap)
				{
					return iteration + 1;
				}

				currentStiffness *= m_IterationDamping;
			}

			return m_MaxIterations;
		}

		/// <summary>
		/// 计算所有元素的包围矩形（带边距）
		/// </summary>
		private Rect CalculateLayoutBounds()
		{
			if (m_Items.Count == 0)
				return new Rect(0, 0, 100, 100);

			float minX = float.MaxValue, minY = float.MaxValue;
			float maxX = float.MinValue, maxY = float.MinValue;

			for (int i = 0; i < m_Items.Count; i++)
			{
				Rect r = m_Items[i].GetRect();
				minX = Mathf.Min(minX, r.xMin);
				minY = Mathf.Min(minY, r.yMin);
				maxX = Mathf.Max(maxX, r.xMax);
				maxY = Mathf.Max(maxY, r.yMax);
			}

			float margin = Mathf.Max(200f, Mathf.Max(maxX - minX, maxY - minY) * 0.5f);
			return new Rect(minX - margin, minY - margin,
				(maxX - minX) + margin * 2f, (maxY - minY) + margin * 2f);
		}

		/// <summary>
		/// 计算排斥位移量
		/// </summary>
		private Vector2 CalculateRepulsionDisplacement(
			Vector2 posA, Vector2 sizeA,
			Vector2 posB, Vector2 sizeB,
			float stiffness)
		{
			Vector2 delta = posA - posB;
			Vector2 halfSizes = (sizeA + sizeB) * 0.5f;

			float overlapX = halfSizes.x - Mathf.Abs(delta.x);
			float overlapY = halfSizes.y - Mathf.Abs(delta.y);

			if (overlapX <= 0f || overlapY <= 0f)
				return Vector2.zero;

			// 沿重叠最小的方向排斥，减少移动量
			if (overlapX < overlapY)
			{
				float sign = delta.x > 0f ? 1f : -1f;
				return new Vector2(sign * overlapX * stiffness, 0f);
			}
			else if (overlapY < overlapX)
			{
				float sign = delta.y > 0f ? 1f : -1f;
				return new Vector2(0f, sign * overlapY * stiffness);
			}
			else
			{
				float signX = delta.x > 0f ? 1f : -1f;
				float signY = delta.y > 0f ? 1f : -1f;
				return new Vector2(signX * overlapX * stiffness * 0.5f, signY * overlapY * stiffness * 0.5f);
			}
		}

		private static bool RectsOverlap(Rect a, Rect b)
		{
			return a.xMin < b.xMax && a.xMax > b.xMin &&
				   a.yMin < b.yMax && a.yMax > b.yMin;
		}

		#endregion

		#region 位置应用

		/// <summary>
		/// 将所有 BubbleItemData 的位置写回 RectTransform
		/// </summary>
		private void ApplyPositions()
		{
			for (int i = 0; i < m_Items.Count; i++)
			{
				var item = m_Items[i];
				if (item.rectTransform == null)
					continue;

				m_Tracker.Add(this, item.rectTransform,
					DrivenTransformProperties.AnchoredPosition | DrivenTransformProperties.AnchoredPositionZ);

				item.ApplyToTransform();
			}
		}

		#endregion

		#region 公共 API

		/// <summary>
		/// 获取指定Key对应的气泡数据
		/// </summary>
		public BubbleItemData GetItemData(string key)
		{
			if (m_ItemIndexMap.TryGetValue(key, out int idx))
				return m_Items[idx];
			return null;
		}

		/// <summary>
		/// 获取指定RectTransform对应的气泡数据
		/// </summary>
		public BubbleItemData GetItemData(RectTransform rt)
		{
			if (rt == null)
				return null;
			return GetItemData($"{rt.name}_{rt.GetInstanceID()}");
		}

		/// <summary>
		/// 获取所有气泡元素数据（只读）
		/// </summary>
		public IReadOnlyList<BubbleItemData> GetAllItems()
		{
			return m_Items;
		}

		/// <summary>
		/// 手动触发布局重建
		/// </summary>
		public void RequestLayoutRebuild()
		{
			LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
		}

		#endregion

		#region 调试

#if UNITY_EDITOR
		private void OnDrawGizmos()
		{
			if (!enabled)
				return;

			// 绘制每个子元素的包围盒
			for (int i = 0; i < m_Items.Count; i++)
			{
				var item = m_Items[i];
				Rect r = item.GetRect();
				Gizmos.color = new Color(0, 1, 1, 0.3f);
				Vector3 center = new Vector3(r.center.x, r.center.y, 0);
				Vector3 size = new Vector3(r.width, r.height, 0);
				Gizmos.DrawWireCube(center, size);
			}
		}
#endif

		#endregion
	}
}
