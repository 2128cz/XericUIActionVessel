using System.Collections.Generic;

using UnityEngine;

namespace XericUI.XTable.Rendering.Elements
{
	/// <summary>
	/// 预制体对象池——按原型分组管理，每个预制体对应一个独立的对象池。
	/// 使用 Queue + HashSet 模式，FIFO 保证对象按回收顺序均匀复用。
	/// </summary>
	public class PrefabPrototypePool
	{
		#region 内部类型

		private class PrefabPool
		{
			public GameObject Prototype;
			/// <summary>休眠实例队列（FIFO，先归还的优先复用）</summary>
			public Queue<GameObject> Available = new Queue<GameObject>();
			/// <summary>活跃实例集合（O(1) 查找 + 防止重复归还）</summary>
			public HashSet<GameObject> Active = new HashSet<GameObject>();
		}

		#endregion

		#region 字段

		private Dictionary<GameObject, PrefabPool> m_Pools = new Dictionary<GameObject, PrefabPool>();
		private Transform m_Parent;

		/// <summary>渲染项目总数上限</summary>
		private const int MAX_PREFAB_ITEMS = 256;
		private int m_TotalCreated;

		private const HideFlags POOL_FLAGS = HideFlags.DontSave | HideFlags.HideInHierarchy;

		private static readonly Vector2 AnchorTopLeft = new Vector2(0, 1);
		private static readonly Vector2 PivotTopLeft = new Vector2(0, 1);

		#endregion

		#region 构造 / 销毁

		public PrefabPrototypePool(Transform parent)
		{
			m_Parent = parent;
		}

		/// <summary>彻底销毁所有池对象（活跃 + 休眠）</summary>
		public void DestroyAll()
		{
			foreach (var kv in m_Pools)
			{
				var pool = kv.Value;
				// 销毁活跃实例
				foreach (var go in pool.Active)
					KillObject(go);
				pool.Active.Clear();
				// 销毁休眠实例
				while (pool.Available.Count > 0)
					KillObject(pool.Available.Dequeue());
			}
			m_Pools.Clear();
		}

		private static void KillObject(GameObject go)
		{
			if (go == null) return;
			if (Application.isPlaying)
				Object.Destroy(go);
			else
				Object.DestroyImmediate(go);
		}

		#endregion

		#region 池操作

		/// <summary>从池中获取实例（FIFO 复用；池空且未达上限则 Instantiate；已达上限返回 null）</summary>
		public GameObject Acquire(GameObject prefab, int row, int col)
		{
			if (prefab == null) return null;

			var pool = GetOrCreatePool(prefab);

			GameObject instance;
			if (pool.Available.Count > 0)
			{
				instance = pool.Available.Dequeue();
			}
			else if (m_TotalCreated < MAX_PREFAB_ITEMS)
			{
				instance = Object.Instantiate(prefab, m_Parent);
				instance.hideFlags = POOL_FLAGS;
				m_TotalCreated++;
			}
			else
			{
				Debug.LogWarning($"[XericUIActionTable] Prefab 池已达上限 {MAX_PREFAB_ITEMS}，无法分配新实例");
				return null;
			}

			instance.name = $"_prefab_{row}_{col}";

			// 强制 RectTransform 左上角坐标系
			RectTransform rt = instance.GetComponent<RectTransform>();
			if (rt == null) rt = instance.AddComponent<RectTransform>();
			rt.anchorMin = AnchorTopLeft;
			rt.anchorMax = AnchorTopLeft;
			rt.pivot = PivotTopLeft;

			pool.Active.Add(instance);
			return instance;
		}

		/// <summary>将指定实例归还对象池（不入池根，原地保留避免 OnEnable/OnDisable）</summary>
		public void Release(GameObject instance)
		{
			if (instance == null) return;

			// 查找所属池
			foreach (var kv in m_Pools)
			{
				if (kv.Value.Active.Remove(instance))
				{
					kv.Value.Available.Enqueue(instance);
					return;
				}
			}

			// 不属于任何池（异常情况），直接销毁
			KillObject(instance);
		}

		/// <summary>将所有活跃实例回收至池（原地保留，仅从活跃集移除并入队）</summary>
		public void ReturnAll()
		{
			foreach (var kv in m_Pools)
			{
				var pool = kv.Value;
				foreach (var go in pool.Active)
				{
					pool.Available.Enqueue(go);
				}
				pool.Active.Clear();
			}
		}

		/// <summary>获取当前所有活跃实例（用于外部遍历回调）</summary>
		public IReadOnlyCollection<GameObject> GetActive(GameObject prefab)
		{
			if (prefab == null || !m_Pools.TryGetValue(prefab, out var pool))
				return System.Array.Empty<GameObject>();
			return pool.Active;
		}

		#endregion

		#region 内部方法

		private PrefabPool GetOrCreatePool(GameObject prefab)
		{
			if (!m_Pools.TryGetValue(prefab, out var pool))
			{
				pool = new PrefabPool { Prototype = prefab };
				m_Pools[prefab] = pool;
			}
			return pool;
		}

		#endregion
	}
}
