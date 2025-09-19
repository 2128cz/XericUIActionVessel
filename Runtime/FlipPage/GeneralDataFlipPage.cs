using System;
using UnityEngine;
using UnityEngine.Pool;

namespace XericUI.FlipPage
{
    /// <summary>
    /// 翻页组件管理器(通用数据层)
    /// </summary>
    [AddComponentMenu("Xeric UI Vessel/FlipPage/General FilpPage Manager")]
    public class GeneralDataFlipPage : FlipPageComponentManager
    {
        #region 生命周期

        protected override void Awake()
        {
            base.Awake();
        }

        #endregion

        #region 容器控制
            
        /// <summary>
        /// 添加一个数据项目
        /// </summary>
        /// <param name="item">数据项目</param>
        /// <typeparam name="T"></typeparam>
        public void AddItem<T>(T item)
            where T : ItemComponentInfo
        {
            base.AddItem(item, () => AddPageLast(new PageComponentInfo()));
        }

        /// <summary>
        /// 添加一个数据项目
        /// </summary>
        /// <param name="item">数据项目</param>
        public override void AddItem(ItemInfo item)
        {
            base.AddItem(item, () => AddPageLast(new PageComponentInfo()));
        }

        #endregion
    }
}