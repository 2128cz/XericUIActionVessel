using System.Collections.Generic;

using UnityEngine;

namespace XericUI.XTable.Rendering.Component
{
	/// <summary>
	/// 单元格对象池——复用表格单元格 GameObject，避免频繁 Instantiate/Destroy。
	/// </summary>
	public class XTableObjectPool
	{
		/// <summary>回收池</summary>
		private readonly Stack<GameObject> m_Pool = new Stack<GameObject>();

		/// <summary>当前活跃对象（已借出）</summary>
		private readonly HashSet<GameObject> m_Active = new HashSet<GameObject>();

		/// <summary>预制体模板</summary>
		private readonly GameObject m_Prefab;

		/// <summary>父级 Transform</summary>
		private readonly Transform m_Parent;

		/// <summary>初始池容量</summary>
		private readonly int m_InitialCapacity;

		public XTableObjectPool(GameObject prefab, Transform parent, int initialCapacity = 64)
		{
			m_Prefab = prefab;
			m_Parent = parent;
			m_InitialCapacity = initialCapacity;

			// 预创建对象
			for (int i = 0; i < initialCapacity; i++)
			{
				GameObject obj = CreateNew();
				obj.SetActive(false);
				m_Pool.Push(obj);
			}
		}

		/// <summary>
		/// 从池中获取一个对象
		/// </summary>
		public GameObject Get()
		{
			GameObject obj;
			if (m_Pool.Count > 0)
			{
				obj = m_Pool.Pop();
			}
			else
			{
				obj = CreateNew();
			}

			obj.SetActive(true);
			m_Active.Add(obj);
			return obj;
		}

		/// <summary>
		/// 将对象归还到池中
		/// </summary>
		public void Return(GameObject obj)
		{
			if (obj == null) return;

			obj.SetActive(false);
			m_Active.Remove(obj);
			m_Pool.Push(obj);
		}

		/// <summary>
		/// 归还所有活跃对象
		/// </summary>
		public void ReturnAll()
		{
			foreach (var obj in m_Active)
			{
				if (obj != null)
				{
					obj.SetActive(false);
					m_Pool.Push(obj);
				}
			}
			m_Active.Clear();
		}

		/// <summary>
		/// 清理所有对象
		/// </summary>
		public void Clear()
		{
			ReturnAll();
			while (m_Pool.Count > 0)
			{
				var obj = m_Pool.Pop();
				if (obj != null)
					Object.Destroy(obj);
			}
		}

		/// <summary>当前活跃对象数</summary>
		public int ActiveCount => m_Active.Count;

		/// <summary>池中空闲对象数</summary>
		public int PoolCount => m_Pool.Count;

		private GameObject CreateNew()
		{
			GameObject obj = m_Prefab != null
				? Object.Instantiate(m_Prefab, m_Parent)
				: new GameObject("TableCell", typeof(RectTransform));

			obj.transform.SetParent(m_Parent, false);
			return obj;
		}
	}
}
