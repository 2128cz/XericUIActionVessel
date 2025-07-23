using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using XericLibrary.Runtime.MacroLibrary;
using XericUI.Core.Base;
using XericUI.Core.Vessel;

namespace XericUI.FlipPage
{
    public delegate void PageIndexAction(int pageIndex, int pageOffset, int totalPageCount);
    
    /// <summary>
    /// 翻页管理器
    /// </summary>
    public class XericFlipPageIndexVesselBase : XericUIBehaciour
    {
        #region 委托事件

        public event PageIndexAction OnPageFlip;

        #endregion
        
        #region 设定参数

        /// <summary>
        /// 候选页数量
        /// </summary>
#if ODIN_INSPECTOR
        [LabelText("候选页数量")]
        [BoxGroup("设定", centerLabel: true)]
#endif
        [SerializeField]
        private int candidatesNumber = 1;

        /// <summary>
        /// 翻页可以跨越索引边接（循环索引）
        /// </summary>
#if ODIN_INSPECTOR
        [LabelText("翻页时允许跨越索引循环")]
        [BoxGroup("设定", centerLabel: true)]
#endif
        [SerializeField]
        private bool canPassIndexBoundary;

        #endregion

        #region 类内类型

        [Serializable]
        public class PageInfo
        {
#if ODIN_INSPECTOR
            [LabelText("页面名称")]
#endif
            public string Name = DateTime.Now.Ticks.ToString();
        }

        #endregion

        #region 字段属性

        /// <summary>
        /// 当前页面
        /// </summary>
        private int _currentPageIndex = 0;

        /// <summary>
        /// 所有页面
        /// </summary>
        private LinkedList<PageInfo> _pageInfos = new LinkedList<PageInfo>();

        
        
        public LinkedList<PageInfo> PageInfos => _pageInfos;
        
        #endregion

        #region 生命周期

        protected override void Awake()
        {
            base.Awake();
        }

        protected override void Start()
        {
            base.Start();
            FlipPage(_currentPageIndex);
        }

        #endregion

        #region 翻页管理

        /// <summary>
        /// 翻到指定页
        /// </summary>
        /// <param name="pageIndex"></param>
        /// <param name="forceRefresh"></param>
        public virtual void FlipPage(int pageIndex, bool forceRefresh = false)
        {
            // 检查翻页索引有效
            if (!canPassIndexBoundary &&
                (pageIndex < 0 || pageIndex >= _pageInfos.Count))
                pageIndex = Mathf.Clamp(pageIndex, 0, _pageInfos.Count - 1);
            else
                pageIndex = MacroMath.PMod(pageIndex, _pageInfos.Count - 1);
            // 检查翻页数量有效
            if (!forceRefresh && _currentPageIndex == pageIndex)
                return;
            // 翻页计算
            var pageIndexOffset = _currentPageIndex - pageIndex;
            var lastPageIndex = _currentPageIndex;
            _currentPageIndex = pageIndex;
            try
            {
                // 翻页事件委托
                OnPageFlip?.Invoke(pageIndex, pageIndexOffset, _pageInfos.Count);
            }
            catch (Exception e)
            {
                Debug.LogError($"翻页时委托错误已捕获\n{e.Message}\n{e.Data}");
            }
        }

        /// <summary>
        /// 翻到开头
        /// </summary>
#if ODIN_INSPECTOR
        [HorizontalGroup("翻页"), Button("翻到开头")]
#endif
        public void FlipPageToStart()
            => FlipPage(0);

        /// <summary>
        /// 翻到上一页
        /// </summary>
#if ODIN_INSPECTOR
        [HorizontalGroup("翻页"), Button("翻到上一页")]
#endif
        public void FlipPageToPrevious()
            => FlipPage(_currentPageIndex - 1);

        /// <summary>
        /// 翻到下一页
        /// </summary>
#if ODIN_INSPECTOR
        [HorizontalGroup("翻页"), Button("翻到下一页")]
#endif
        public void FlipPageToNext()
            => FlipPage(_currentPageIndex + 1);

        /// <summary>
        /// 翻到结尾
        /// </summary>
#if ODIN_INSPECTOR
        [HorizontalGroup("翻页"), Button("翻到结尾")]
#endif
        public void FlipPageToEnd()
            => FlipPage(_pageInfos.Count - 1);

        
        
        /// <summary>
        /// 添加页面
        /// </summary>
        public virtual void AddPage(PageInfo pageInfo)
        {
            _pageInfos.AddLast(pageInfo);
            // 刷新页面
            FlipPage(_currentPageIndex, true);
        }

        /// <summary>
        /// 移除页面
        /// </summary>
        public void RemovePage(int pageIndex)
        {
            RemovePage(IndexOfPage(pageIndex));
        }

        /// <summary>
        /// 移除页面
        /// </summary>
        /// <param name="pageRef"></param>
        public void RemovePage(PageInfo pageRef)
        {
            _pageInfos.Remove(pageRef);
            // 刷新页面
            FlipPage(_currentPageIndex, true);
        }
        
        /// <summary>
        /// 交换AB页面
        /// </summary>
        public void SwapPages(int pageIndexA, int pageIndexB)
        {
            var a = _pageInfos.GetOrderNode().LandedIndex(pageIndexA);
            var b = _pageInfos.GetOrderNode().LandedIndex(pageIndexB);
            
            _pageInfos.SwapNodes(a, b);
            // 刷新页面
            FlipPage(_currentPageIndex, true);
        }

        /// <summary>
        /// 选择索引下的页面
        /// </summary>
        /// <param name="pageIndex"></param>
        /// <returns></returns>
        public PageInfo IndexOfPage(int pageIndex)
        {
            return _pageInfos.LandedIndex(pageIndex);
        }
        
        #endregion
    }
}