using System;
using System.Collections.Generic;

using UnityEngine;

namespace XericUI.XTable.Rendering.Elements
{
	/// <summary>
	/// 基于计数器的槽位对象池（泛型）。
	/// cellId 在绑定期间稳定地映射到同一槽位；计数器仅在全部槽位解除绑定后整体待命复用。
	/// </summary>
	/// <typeparam name="T">池对象类型</typeparam>
	public class SlotCounterPool<T> where T : class
	{
		#region 内部类型

		private class Slot
		{
			public int CellId = -1;
			public T PooledObject;
		}

		private class SlotCounter
		{
			public readonly List<Slot> Slots = new List<Slot>(capacity: 4);
			public int BoundSlotCount;
		}

		#endregion

		#region 字段

		private const int DEFAULT_ROUTE_BUCKET_COUNT = 256;

		private readonly SlotCounter[] m_Counters;
		private readonly int m_MaxCreatedCount;
		private readonly Stack<SlotCounter> m_StandbyCounters = new Stack<SlotCounter>();
		private readonly Func<T> m_Factory;
		private readonly Action<IReadOnlyList<T>> m_OnCounterBorrowed;
		private readonly Action<IReadOnlyList<T>> m_OnCounterReturned;
		private int m_CreatedCount;

		/// <summary>已创建对象总数（只读）</summary>
		public int CreatedCount => m_CreatedCount;
		public int MaxCreatedCount => m_MaxCreatedCount;
		public int RouteBucketCount => m_Counters.Length;
		public int ActiveCounterCount
		{
			get
			{
				int count = 0;
				for (int i = 0; i < m_Counters.Length; i++)
					if (m_Counters[i] != null) count++;
				return count;
			}
		}
		public bool IsPoolFull => m_CreatedCount >= m_MaxCreatedCount;

		#endregion

		#region 构造

		public SlotCounterPool(Func<T> factory, int maxCreatedCount = 256,
			Action<IReadOnlyList<T>> onCounterBorrowed = null, Action<IReadOnlyList<T>> onCounterReturned = null,
			int routeBucketCount = DEFAULT_ROUTE_BUCKET_COUNT)
		{
			m_Factory = factory ?? throw new ArgumentNullException(nameof(factory));
			m_MaxCreatedCount = Mathf.Max(1, maxCreatedCount);
			m_Counters = new SlotCounter[Mathf.Max(1, routeBucketCount)];
			m_OnCounterBorrowed = onCounterBorrowed;
			m_OnCounterReturned = onCounterReturned;
		}

		#endregion

		#region 池操作

		/// <summary>为指定 cellId 获取池对象，并指示是否建立了新槽位绑定。</summary>
		public T Acquire(int cellId, out bool isNewBinding)
		{
			int index = GetRouteIndex(cellId);
			var counter = m_Counters[index];
			bool borrowedCounter = false;
			if (counter == null)
			{
				counter = m_StandbyCounters.Count > 0 ? m_StandbyCounters.Pop() : new SlotCounter();
				m_Counters[index] = counter;
				borrowedCounter = true;
			}

			for (int i = 0; i < counter.Slots.Count; i++)
			{
				var slot = counter.Slots[i];
				if (slot.CellId == cellId)
				{
					isNewBinding = false;
					return slot.PooledObject;
				}
			}

			Slot availableSlot = null;
			for (int i = 0; i < counter.Slots.Count; i++)
			{
				if (counter.Slots[i].CellId == -1)
				{
					availableSlot = counter.Slots[i];
					break;
				}
			}

			if (availableSlot == null)
			{
				if (m_CreatedCount >= m_MaxCreatedCount)
				{
					if (borrowedCounter)
					{
						m_Counters[index] = null;
						m_StandbyCounters.Push(counter);
					}
					Debug.LogWarning($"[SlotCounterPool<{typeof(T).Name}>] 已达上限 {m_MaxCreatedCount}，无法分配新对象");
					isNewBinding = false;
					return null;
				}

				availableSlot = new Slot { PooledObject = m_Factory() };
				counter.Slots.Add(availableSlot);
				m_CreatedCount++;
			}

			availableSlot.CellId = cellId;
			counter.BoundSlotCount++;
			isNewBinding = true;
			if (borrowedCounter)
				m_OnCounterBorrowed?.Invoke(GetObjects(counter));
			return availableSlot.PooledObject;
		}

		/// <summary>为指定 cellId 获取一个池对象。</summary>
		public T Acquire(int cellId)
		{
			return Acquire(cellId, out _);
		}

		/// <summary>解除 cellId 的槽位绑定；仅计数器完全空闲时才将其整体归还待命栈。</summary>
		public bool ReleaseCell(int cellId)
		{
			int index = GetRouteIndex(cellId);
			var counter = m_Counters[index];
			if (counter == null) return false;

			for (int i = 0; i < counter.Slots.Count; i++)
			{
				var slot = counter.Slots[i];
				if (slot.CellId != cellId) continue;

				slot.CellId = -1;
				counter.BoundSlotCount--;
				if (counter.BoundSlotCount == 0)
					ReturnCounter(index, counter);
				return true;
			}
			return false;
		}

		/// <summary>兼容旧 API：解除指定 cellId 的绑定。</summary>
		public void Release(int cellId)
		{
			ReleaseCell(cellId);
		}

		/// <summary>通过池对象引用解除对应 cellId 的槽位绑定。</summary>
		public bool ReleaseByObject(T pooledObject)
		{
			if (pooledObject == null) return false;

			for (int ci = 0; ci < m_Counters.Length; ci++)
			{
				var counter = m_Counters[ci];
				if (counter == null) continue;
				for (int i = 0; i < counter.Slots.Count; i++)
				{
					var slot = counter.Slots[i];
					if (slot.CellId != -1 && ReferenceEquals(slot.PooledObject, pooledObject))
						return ReleaseCell(slot.CellId);
				}
			}
			return false;
		}

		/// <summary>解除全部槽位绑定，并将所有计数器整体归还待命栈。</summary>
		public void ReleaseAllBindings()
		{
			for (int ci = 0; ci < m_Counters.Length; ci++)
			{
				var counter = m_Counters[ci];
				if (counter == null) continue;
				for (int i = 0; i < counter.Slots.Count; i++)
					counter.Slots[i].CellId = -1;
				counter.BoundSlotCount = 0;
				ReturnCounter(ci, counter);
			}
		}

		/// <summary>兼容旧 API：解除所有活跃槽位绑定。</summary>
		public void ReturnAll(Action<T> beforeRelease = null)
		{
			if (beforeRelease != null)
			{
				for (int ci = 0; ci < m_Counters.Length; ci++)
				{
					var counter = m_Counters[ci];
					if (counter == null) continue;
					for (int i = 0; i < counter.Slots.Count; i++)
						if (counter.Slots[i].CellId != -1) beforeRelease(counter.Slots[i].PooledObject);
				}
			}
			ReleaseAllBindings();
		}

		/// <summary>彻底销毁所有池对象。</summary>
		public void DestroyAll(Action<T> destroyFunc)
		{
			if (destroyFunc == null) return;
			var destroyedCounters = new HashSet<SlotCounter>();
			for (int ci = 0; ci < m_Counters.Length; ci++)
			{
				var counter = m_Counters[ci];
				if (counter != null) destroyedCounters.Add(counter);
				m_Counters[ci] = null;
			}
			foreach (var counter in m_StandbyCounters) destroyedCounters.Add(counter);
			m_StandbyCounters.Clear();
			foreach (var counter in destroyedCounters)
				foreach (var slot in counter.Slots) destroyFunc(slot.PooledObject);
			m_CreatedCount = 0;
		}

		private int GetRouteIndex(int cellId)
		{
			int index = cellId % m_Counters.Length;
			return index < 0 ? index + m_Counters.Length : index;
		}

		private void ReturnCounter(int index, SlotCounter counter)
		{
			m_OnCounterReturned?.Invoke(GetObjects(counter));
			m_Counters[index] = null;
			m_StandbyCounters.Push(counter);
		}

		private static IReadOnlyList<T> GetObjects(SlotCounter counter)
		{
			var objects = new List<T>(counter.Slots.Count);
			for (int i = 0; i < counter.Slots.Count; i++) objects.Add(counter.Slots[i].PooledObject);
			return objects;
		}

		#endregion
	}
}
