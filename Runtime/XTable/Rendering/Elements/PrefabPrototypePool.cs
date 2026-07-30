using System;
using System.Collections.Generic;
using UnityEngine;

namespace XericUI.XTable.Rendering.Elements
{
    /// <summary>按原型管理的预制体池。占用槽位绑定 cellId，归还后隐藏并留在同原型待命池中复用。</summary>
    public class PrefabPrototypePool
    {
        private class PrefabPool
        {
            public readonly Dictionary<int, GameObject> ActiveSlots = new();
            public readonly Stack<GameObject> Standby = new();
        }

        private readonly Dictionary<GameObject, PrefabPool> m_Pools = new();
        private readonly Transform m_Parent;
        private const int MAX_PREFAB_ITEMS = 256;
        private int m_TotalCreated;
        private const HideFlags POOL_FLAGS = HideFlags.DontSave | HideFlags.HideInHierarchy;
        private static readonly Vector2 AnchorTopLeft = new(0, 1);
        private static readonly Vector2 PivotTopLeft = new(0, 1);

        public PrefabPrototypePool(Transform parent) => m_Parent = parent;

        public GameObject Acquire(GameObject prefab, int cellId, Transform parent = null)
        {
            if (prefab == null) return null;
            PrefabPool pool = GetOrCreatePool(prefab);
            Transform targetParent = parent ?? m_Parent;
            if (pool.ActiveSlots.TryGetValue(cellId, out GameObject existing))
            {
                UpdateLayout(existing, targetParent, cellId);
                return existing;
            }

            GameObject instance = pool.Standby.Count > 0 ? pool.Standby.Pop() : Create(prefab, targetParent);
            if (instance == null) return null;
            pool.ActiveSlots[cellId] = instance;
            PrepareNewBinding(instance, targetParent, cellId);
            return instance;
        }

        public void ReturnAll(Action<GameObject> beforeRelease = null)
        {
            foreach (PrefabPool pool in m_Pools.Values)
            {
                foreach (GameObject instance in pool.ActiveSlots.Values)
                {
                    beforeRelease?.Invoke(instance);
                    instance.SetActive(false);
                    pool.Standby.Push(instance);
                }
                pool.ActiveSlots.Clear();
            }
        }

        public bool Release(GameObject instance, Action<GameObject> beforeRelease = null)
        {
            if (instance == null) return false;
            foreach (PrefabPool pool in m_Pools.Values)
            {
                int foundCellId = -1;
                foreach (var pair in pool.ActiveSlots)
                {
                    if (pair.Value != instance) continue;
                    foundCellId = pair.Key;
                    break;
                }
                if (foundCellId < 0) continue;
                beforeRelease?.Invoke(instance);
                pool.ActiveSlots.Remove(foundCellId);
                instance.SetActive(false);
                pool.Standby.Push(instance);
                return true;
            }
            return false;
        }

        public void DestroyAll()
        {
            foreach (PrefabPool pool in m_Pools.Values)
            {
                foreach (GameObject instance in pool.ActiveSlots.Values) KillObject(instance);
                foreach (GameObject instance in pool.Standby) KillObject(instance);
            }
            m_Pools.Clear();
            m_TotalCreated = 0;
        }

        private PrefabPool GetOrCreatePool(GameObject prefab)
        {
            if (!m_Pools.TryGetValue(prefab, out PrefabPool pool))
            {
                pool = new PrefabPool();
                m_Pools.Add(prefab, pool);
            }
            return pool;
        }

        private GameObject Create(GameObject prefab, Transform parent)
        {
            if (m_TotalCreated >= MAX_PREFAB_ITEMS)
            {
                Debug.LogWarning($"[XericUIActionTable] Prefab 池已达上限 {MAX_PREFAB_ITEMS}，无法分配新实例");
                return null;
            }
            GameObject instance = UnityEngine.Object.Instantiate(prefab, parent);
            instance.hideFlags = POOL_FLAGS;
            m_TotalCreated++;
            return instance;
        }

        private static void PrepareNewBinding(GameObject instance, Transform parent, int cellId)
        {
            UpdateLayout(instance, parent, cellId);
            instance.SetActive(true);
        }

        private static void UpdateLayout(GameObject instance, Transform parent, int cellId)
        {
            if (parent != null && instance.transform.parent != parent)
                instance.transform.SetParent(parent, false);
            instance.name = $"_prefab_{cellId}";
            RectTransform rt = instance.GetComponent<RectTransform>() ?? instance.AddComponent<RectTransform>();
            rt.anchorMin = AnchorTopLeft;
            rt.anchorMax = AnchorTopLeft;
            rt.pivot = PivotTopLeft;
        }

        private static void KillObject(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(go);
            else UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
