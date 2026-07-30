using System;
using System.Collections.Generic;

using UnityEngine;

namespace XericUI.XTable.Rendering.Elements
{
	/// <summary>
	/// 基于计数器的槽位对象池（泛型）。
	/// 每个 cellId 映射到固定索引的 SlotCounter，释放槽位保留在原计数器内复用。
	/// 避免跨计数器拆分碰撞组导致的对象位置冲突。
	/// </summary>
	/// <typeparam name="T">池对象类型</typeparam>
	public class SlotCounterPool<T> where T : class
	{
		#region 内部类型

		private class Slot
		{
			/// <summary>当前绑定的单元格 ID（-1 = 空闲，可被偷取）</summary>
			public int CellId = -1;
			/// <summary>池对象引用</summary>
			public T PooledObject;
		}

		private class SlotCounter
		{
			public List<Slot> Slots = new List<Slot>(capacity: 4);
		}

		#endregion

		#region 字段

		/// <summary>计数器数组大小（= 池上限，也是 cellId 取模的模数）</summary>
		public const int POOL_SIZE = 256;

		private SlotCounter[] m_Counters = new SlotCounter[POOL_SIZE];

		/// <summary>已创建对象总数（不含预制体 Instantiate）</summary>
		private int m_CreatedCount;

		/// <summary>对象工厂（构造函数传入）</summary>
		private Func<T> m_Factory;

		/// <summary>已创建对象总数（只读）</summary>
		public int CreatedCount => m_CreatedCount;

		#endregion

		#region 构造

		public SlotCounterPool(Func<T> factory)
		{
			m_Factory = factory ?? throw new ArgumentNullException(nameof(factory));
		}

		#endregion

		#region 池操作

		/// <summary>
		/// 为指定 cellId 获取一个池对象。
		/// 优先复用本计数器内的空闲槽位，最后创建新对象。
		/// </summary>
		public T Acquire(int cellId)
		{
			int index = cellId % POOL_SIZE;
			if (index < 0) index += POOL_SIZE; // 处理负 cellId

			var counter = m_Counters[index];
			if (counter == null)
			{
				counter = new SlotCounter();
				m_Counters[index] = counter;
			}

			// 1. 在计数器内查找空闲槽位
			for (int i = 0; i < counter.Slots.Count; i++)
			{
				if (counter.Slots[i].CellId == -1)
				{
					counter.Slots[i].CellId = cellId;
					return counter.Slots[i].PooledObject;
				}
			}

			// 2. 创建新对象。槽位不能跨计数器流转，避免破坏碰撞单元格的稳定关系。
			if (m_CreatedCount < POOL_SIZE)
			{
				var obj = m_Factory();
				var slot = new Slot { CellId = cellId, PooledObject = obj };
				counter.Slots.Add(slot);
				m_CreatedCount++;
				return obj;
			}

			// 4. 已达上限
			Debug.LogWarning($"[SlotCounterPool<{typeof(T).Name}>] 已达上限 {POOL_SIZE}，无法分配新对象");
			return null;
		}

		/// <summary>
		/// 释放指定 cellId 占用的槽位，并保留在原计数器中供相同碰撞组复用。
		/// </summary>
		public void Release(int cellId)
		{
			int index = cellId % POOL_SIZE;
			if (index < 0) index += POOL_SIZE;

			var counter = m_Counters[index];
			if (counter == null) return;

			for (int i = 0; i < counter.Slots.Count; i++)
			{
				if (counter.Slots[i].CellId == cellId)
				{
					counter.Slots[i].CellId = -1;
					return;
				}
			}
		}

		/// <summary>
		/// 尝试通过池对象引用查找并释放对应槽位。
		/// 用于 Release(PooledObject) 场景，需要遍历查找。
		/// </summary>
		public bool ReleaseByObject(T pooledObject)
		{
			if (pooledObject == null) return false;

			// 先遍历所有计数器的活跃槽位
			for (int ci = 0; ci < POOL_SIZE; ci++)
			{
				var counter = m_Counters[ci];
				if (counter == null) continue;

				for (int i = 0; i < counter.Slots.Count; i++)
				{
					if (ReferenceEquals(counter.Slots[i].PooledObject, pooledObject))
					{
						counter.Slots[i].CellId = -1;
						return true;
					}
				}
			}
			return false;
		}

		/// <summary>释放所有活跃槽位；回调在槽位变为空闲前执行。</summary>
		public void ReturnAll(Action<T> beforeRelease = null)
		{
			for (int ci = 0; ci < POOL_SIZE; ci++)
			{
				var counter = m_Counters[ci];
				if (counter == null) continue;
				for (int i = 0; i < counter.Slots.Count; i++)
				{
					var slot = counter.Slots[i];
					if (slot.CellId == -1) continue;
					beforeRelease?.Invoke(slot.PooledObject);
					slot.CellId = -1;
				}
			}
		}

		/// <summary>
		/// 彻底销毁所有池对象。
		/// </summary>
		public void DestroyAll(Action<T> destroyFunc)
		{
			if (destroyFunc == null) return;

			for (int ci = 0; ci < POOL_SIZE; ci++)
			{
				var counter = m_Counters[ci];
				if (counter == null) continue;

				foreach (var slot in counter.Slots)
					destroyFunc(slot.PooledObject);
				counter.Slots.Clear();
			}

			m_CreatedCount = 0;
		}

		#endregion
	}
}
