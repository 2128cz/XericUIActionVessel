using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using XericLibrary.Runtime;
using XericLibrary.Runtime.Type;

namespace XericUI.InfiniteVessel
{
    /// <summary>
    /// 无限UI列表容器基类
    /// </summary>
    public abstract class XericInfiniteVesselBase : WeaklyMonoBase
    {
        #region 字段属性

        public GameObject mainItemPrefab;
        
        private ObjectPool<GameObject> _itemPool = null;

        #endregion

        #region 生命周期

        protected virtual void Awake()
        {
            AwakeInitItemPool();
        }

        protected virtual void AwakeInitItemPool()
        {
            _itemPool = new ObjectPool<GameObject>(
                CreatItem, GetItemManager, ReleaseItem, DestroyItem);
        }

        #endregion

        #region 对象池操作

        /// <summary>
        /// 创建对象
        /// </summary>
        /// <returns></returns>
        public virtual GameObject CreatItem()
        {
            return Object.Instantiate(mainItemPrefab);
        }

        /// <summary>
        /// 获取对象，并获取管理器
        /// </summary>
        /// <param name="target"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public virtual void GetItemManager(GameObject target)
        {
            target.SetActive(true);
        }

        /// <summary>
        /// 释放对象
        /// </summary>
        /// <param name="target"></param>
        public virtual void ReleaseItem(GameObject target)
        {
            target.SetActive(false);
        }

        /// <summary>
        /// 销毁对象
        /// </summary>
        /// <param name="target"></param>
        public virtual void DestroyItem(GameObject target)
        {
            Object.Destroy(target);
        }

        #endregion
    }
}