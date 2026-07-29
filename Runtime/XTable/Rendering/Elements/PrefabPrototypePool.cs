using System.Collections.Generic;

using UnityEngine;

namespace XericUI.XTable.Rendering.Elements
{
	/// <summary>
	/// 预制体对象池——按原型分组管理，每个原型使用计数器槽位绑定 cellId 到预制体实例。
	/// 预制体不参与 FrozenSlots 流转：始终绑定在 cellId 槽位上，仅在表格销毁时释放。
	/// </summary>
	public class PrefabPrototypePool
	{
		#region 内部类型

		private class PrefabPool
		{
			public GameObject Prototype;
			/// <summary>cellId → 预制体实例 的计数器槽位绑定</summary>
			public Dictionary<int, GameObject> Slots = new Dictionary<int, GameObject>();
		}

		#endregion

		#region 字段

		private Dictionary<GameObject, PrefabPool> m_Pools = new Dictionary<GameObject, PrefabPool>();
		private Transform m_Parent;

		/// <summary>渲染项目总数上限（全局，跨所有原型）</summary>
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

		/// <summary>彻底销毁所有池对象（所有原型的活跃槽位）</summary>
		public void DestroyAll()
		{
			foreach (var kv in m_Pools)
			{
				var pool = kv.Value;
				foreach (var instance in pool.Slots.Values)
					KillObject(instance);
				pool.Slots.Clear();
			}
			m_Pools.Clear();
			m_TotalCreated = 0;
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

		/// <summary>
		/// 为指定 cellId 获取预制体实例。
		/// 若该 cellId 已有绑定的实例，直接返回；否则创建新实例（需未达上限）。
		/// </summary>
		public GameObject Acquire(GameObject prefab, int cellId, Transform parent = null)
		{
			if (prefab == null) return null;

			var pool = GetOrCreatePool(prefab);
			Transform targetParent = parent ?? m_Parent;

			// 已绑定 → 直接复用并迁移到当前渲染区域。
			if (pool.Slots.TryGetValue(cellId, out var existing))
			{
				if (targetParent != null && existing.transform.parent != targetParent)
					existing.transform.SetParent(targetParent, false);
				return existing;
			}

			// 创建新实例
			if (m_TotalCreated >= MAX_PREFAB_ITEMS)
			{
				Debug.LogWarning($"[XericUIActionTable] Prefab 池已达上限 {MAX_PREFAB_ITEMS}，无法分配新实例");
				return null;
			}

			var instance = Object.Instantiate(prefab, targetParent);
			instance.hideFlags = POOL_FLAGS;
			instance.name = $"_prefab_{cellId}";
			m_TotalCreated++;

			// 强制 RectTransform 左上角坐标系
			RectTransform rt = instance.GetComponent<RectTransform>();
			if (rt == null) rt = instance.AddComponent<RectTransform>();
			rt.anchorMin = AnchorTopLeft;
			rt.anchorMax = AnchorTopLeft;
			rt.pivot = PivotTopLeft;

			pool.Slots[cellId] = instance;
			return instance;
		}

		/// <summary>
		/// 归还指定实例至槽位（仅从槽位移除 + 销毁对象，不在滚动时调用）。
		/// 预制体的回收仅发生在表格销毁时。
		/// </summary>
		public void Release(GameObject instance)
		{
			if (instance == null) return;

			foreach (var kv in m_Pools)
			{
				var pool = kv.Value;
				// 查找并移除
				int? foundKey = null;
				foreach (var slot in pool.Slots)
				{
					if (slot.Value == instance)
					{
						foundKey = slot.Key;
						break;
					}
				}
				if (foundKey.HasValue)
				{
					pool.Slots.Remove(foundKey.Value);
					m_TotalCreated--;
					KillObject(instance);
					return;
				}
			}

			// 不属于任何池（异常情况），直接销毁
			KillObject(instance);
		}

		/// <summary>销毁所有按 cellId 绑定的实例，并在销毁前通知调用方。</summary>
		public void ReleaseAll(System.Action<GameObject> beforeRelease = null)
		{
			foreach (var pool in m_Pools.Values)
			{
				foreach (var instance in pool.Slots.Values)
				{
					beforeRelease?.Invoke(instance);
					KillObject(instance);
				}
				pool.Slots.Clear();
			}
			m_Pools.Clear();
			m_TotalCreated = 0;
		}

		/// <summary>结束当前渲染周期。实例保留在全局槽位中，以便跨区域复用。</summary>
		public void ReturnAll()
		{
		}

		/// <summary>获取当前所有活跃实例（用于外部遍历回调）</summary>
		public IReadOnlyCollection<GameObject> GetActive(GameObject prefab)
		{
			if (prefab == null || !m_Pools.TryGetValue(prefab, out var pool))
				return System.Array.Empty<GameObject>();
			return pool.Slots.Values;
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
