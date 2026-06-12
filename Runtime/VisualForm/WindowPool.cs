using System.Collections.Generic;
using UnityEngine;

namespace XericUI.VisualForm
{
    /// <summary>
    /// 窗口实例对象池 - 管理预制体实例的复用，减少频繁的创建和销毁开销
    /// </summary>
    public static class WindowPool
    {
        /// <summary>
        /// 待命字典：键为预制体原型引用，值为隐藏的待复用实例队列
        /// </summary>
        private static readonly Dictionary<GameObject, Queue<GameObject>> s_StandbyDict = new Dictionary<GameObject, Queue<GameObject>>();

        /// <summary>
        /// 从对象池获取一个实例。如果池中有则复用，否则实例化新的
        /// </summary>
        /// <param name="prefab">预制体原型</param>
        /// <param name="parent">父级Transform</param>
        /// <returns>获取到的实例</returns>
        public static GameObject Get(GameObject prefab, Transform parent)
        {
            if (prefab == null)
            {
                Debug.LogError("[WindowPool] 预制体为空，无法获取实例");
                return null;
            }

            if (s_StandbyDict.TryGetValue(prefab, out var queue) && queue.Count > 0)
            {
                var instance = queue.Dequeue();
                if (instance != null)
                {
                    instance.transform.SetParent(parent, false);
                    instance.SetActive(true);
                    return instance;
                }
            }

            // 池中无可用实例，创建新的
            var newInstance = Object.Instantiate(prefab, parent, false);
            newInstance.name = prefab.name;
            return newInstance;
        }

        /// <summary>
        /// 将实例回收到对象池中
        /// </summary>
        /// <param name="prefab">预制体原型</param>
        /// <param name="instance">要回收的实例</param>
        public static void Release(GameObject prefab, GameObject instance)
        {
            if (prefab == null || instance == null)
                return;

            instance.SetActive(false);

            if (!s_StandbyDict.TryGetValue(prefab, out var queue))
            {
                queue = new Queue<GameObject>();
                s_StandbyDict[prefab] = queue;
            }

            queue.Enqueue(instance);
        }

        /// <summary>
        /// 清空指定预制体的对象池
        /// </summary>
        /// <param name="prefab">预制体原型</param>
        public static void Clear(GameObject prefab)
        {
            if (prefab == null) return;

            if (s_StandbyDict.TryGetValue(prefab, out var queue))
            {
                while (queue.Count > 0)
                {
                    var instance = queue.Dequeue();
                    if (instance != null)
                        Object.Destroy(instance);
                }
                s_StandbyDict.Remove(prefab);
            }
        }

        /// <summary>
        /// 清空所有对象池
        /// </summary>
        public static void ClearAll()
        {
            foreach (var kvp in s_StandbyDict)
            {
                while (kvp.Value.Count > 0)
                {
                    var instance = kvp.Value.Dequeue();
                    if (instance != null)
                        Object.Destroy(instance);
                }
            }
            s_StandbyDict.Clear();
        }

        /// <summary>
        /// 检查指定预制体在池中是否有可用实例
        /// </summary>
        /// <param name="prefab">预制体原型</param>
        /// <returns>是否有可用实例</returns>
        public static bool HasAvailable(GameObject prefab)
        {
            return prefab != null && s_StandbyDict.TryGetValue(prefab, out var queue) && queue.Count > 0;
        }
    }
}
