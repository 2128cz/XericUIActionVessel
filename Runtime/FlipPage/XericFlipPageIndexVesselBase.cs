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
    /// <summary>
    /// 翻页管理器
    /// </summary>
    /// <code>
    /// 翻页允许按页，按项目进行管理。
    /// </code>
    public class XericFlipPageIndexVesselBase : XericUIBehaciour
    {
        #region 设定参数

        /// <summary>
        /// 候选页数量
        /// </summary>
#if ODIN_INSPECTOR
        [LabelText("候选页数量")]
        [BoxGroup("设定", centerLabel: true)]
#endif
        [Tooltip("最大同时呈现的页面选择器数量")]
        [SerializeField]
        protected float candidatesNumber = 1;

        /// <summary>
        /// 翻页可以跨越索引边接（循环索引）
        /// </summary>
#if ODIN_INSPECTOR
        [LabelText("翻页时允许跨越索引循环")]
        [BoxGroup("设定")]
#endif
        [SerializeField]
        protected bool canPassIndexBoundary;

        /// <summary>
        /// 页面默认最大项目数量
        /// </summary>
#if ODIN_INSPECTOR
        [LabelText("单个页面内默认最多项目数量")]
        [BoxGroup("设定")]
#endif
        [SerializeField]
        protected int defaultItemCountPerPage = -1;

        #endregion

        #region 类内类型

        /// <summary>
        /// 页面信息
        /// </summary>
        [Serializable]
        public abstract class PageInfo
        {
            #region 字段属性

            /// <summary>
            /// 页面名称
            /// </summary>
#if ODIN_INSPECTOR
            [LabelText("页面名称")]
#endif
            public string name = DateTime.Now.Ticks.ToString();

            /// <summary>
            /// 页面内项目列表
            /// </summary>
#if ODIN_INSPECTOR
            [LabelText("页面项目信息")]
#endif
            public List<ItemInfo> itemInfos = new List<ItemInfo>();

            /// <summary>
            /// 从零开始计数的页面下标
            /// </summary>
#if ODIN_INSPECTOR
            [LabelText("页面索引[0, n]")]
#endif
            public int pageNumber { get; internal set; }

            #endregion

            #region 生命周期

            /// <summary>
            /// 页面显示时
            /// </summary>
            /// <param name="quickVisit">快速浏览(用于区分动画和逻辑，为真时，不应当在动画事件后继续加载数据)</param>
            protected internal abstract void OnPageVisible(bool quickVisit);
            
            /// <summary>
            /// 页面隐藏时
            /// </summary>
            /// <param name="quickVisit">快速浏览(用于区分动画和逻辑，为真时，不应当在动画事件后继续加载数据)</param>
            protected internal abstract void OnPageInvisible(bool quickVisit);
            
            #endregion
            
            public override string ToString()
            {
                return $"[{nameof(PageInfo)}] ({name})";
            }
        }

        /// <summary>
        /// 项目信息
        /// </summary>
        [Serializable]
        public abstract class ItemInfo
        {
            #region 字段属性

#if ODIN_INSPECTOR
            [LabelText("项目名称")]
#endif
            public string name = DateTime.Now.Ticks.ToString();

            #endregion

            #region 生命周期

            /// <summary>
            /// 当项目显示时
            /// </summary>
            protected internal abstract void OnItemVisible();

            /// <summary>
            /// 当项目隐藏时
            /// </summary>
            protected internal abstract void OnItemInvisible();

            #endregion

            public override string ToString()
            {
                return $"[{nameof(ItemInfo)}] ({name})";
            }
        }

        #endregion

        #region 字段属性

        /// <summary>
        /// 当前选中的页面索引
        /// </summary>
#if ODIN_INSPECTOR
        [LabelText("当前选中页面索引")]
#endif
        [SerializeField]
        protected int currentPageIndex = 0;

        /// <summary>
        /// 所有页面
        /// </summary>
        private LinkedList<PageInfo> _pageInfos = new LinkedList<PageInfo>();

        /// <summary>
        /// 所有列表项目
        /// </summary>
        private HashSet<ItemInfo> _itemInfos = new HashSet<ItemInfo>();

        /// <summary>
        /// 当前选中页面节点
        /// </summary>
        public LinkedListNode<PageInfo> currentPageNode = null;


        /// <summary>
        /// 所有页面
        /// </summary>
        public LinkedList<PageInfo> PageInfos => _pageInfos;

        /// <summary>
        /// 当前选中页面内容
        /// </summary>
        public PageInfo currentPage => currentPageNode.Value;

        #endregion

        #region 生命周期

        protected override void Awake()
        {
            base.Awake();
        }

        protected override void Start()
        {
            base.Start();

            FlipPage(currentPageIndex);
        }

        #endregion

        #region 翻页管理

        /// <summary>
        /// 当发生翻页时(这会在整个翻页动作流程的最后执行)
        /// </summary>
        protected virtual void OnFlipPage()
        {
            
        }
        
        /// <summary>
        /// 通过指令筛选页面
        /// </summary>
        /// <param name="pageNumber"></param>
        /// <param name="forceRefresh"></param>
        public virtual void FlipPage(string pageNumber, bool forceRefresh = false)
        {
            if (float.TryParse(pageNumber, out var index))
            {
                FlipPage((int)index, forceRefresh);
            }
        }

        /// <summary>
        /// 翻到指定页
        /// </summary>
        /// <param name="pageIndex"></param>
        /// <param name="quickSwitchPage">快速翻页，</param>
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
            if (!forceRefresh && currentPageIndex == pageIndex)
                return;
            // 翻页计算
            var pageIndexOffset = pageIndex - currentPageIndex;
            var lastPageIndex = currentPageIndex;
            // 如果节点不存在，通过索引寻找
            if (currentPageNode == null)
                currentPageNode = _pageInfos.GetOrderNode().LandedIndex(currentPageIndex);
            // 如果索引偏移，逐个移动
            else if (pageIndexOffset > 0)
            {
                for (var i = 0; i < pageIndexOffset; i++)
                {
                    var next = currentPageNode!.Next;
                    SwitchPage(next, _pageInfos.Last, i + 1 == pageIndexOffset);
                }
            }
            else if (pageIndexOffset < 0)
            {
                for (var i = 0; i > pageIndexOffset; i--)
                {
                    var previa = currentPageNode!.Previous;
                    SwitchPage(previa, _pageInfos.First, i - 1 == pageIndexOffset);
                }
            }
            // 没有翻动有效页，退出
            else
                return;
            
            currentPageIndex = pageIndex;
            OnFlipPage();
            
            // 翻页
            void SwitchPage(LinkedListNode<PageInfo> newPageNode, LinkedListNode<PageInfo> defualtPageNode,  bool quickPage)
            {
                if (newPageNode != null)
                {
                    currentPage.OnPageInvisible(quickPage);
                    currentPageNode = newPageNode;
                    currentPage.OnPageVisible(quickPage);
                }

                if (canPassIndexBoundary)
                {
                    currentPage.OnPageInvisible(quickPage);
                    currentPageNode = defualtPageNode;
                    currentPage.OnPageVisible(quickPage);
                }
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
            => FlipPage(currentPageIndex - 1);

        /// <summary>
        /// 翻到下一页
        /// </summary>
#if ODIN_INSPECTOR
        [HorizontalGroup("翻页"), Button("翻到下一页")]
#endif
        public void FlipPageToNext()
            => FlipPage(currentPageIndex + 1);

        /// <summary>
        /// 翻到结尾
        /// </summary>
#if ODIN_INSPECTOR
        [HorizontalGroup("翻页"), Button("翻到结尾")]
#endif
        public void FlipPageToEnd()
            => FlipPage(_pageInfos.Count - 1);

        /// <summary>
        /// 添加页面在开头
        /// </summary>
        public virtual void AddPageFirst(PageInfo pageInfo)
        {
            if (pageInfo == null)
            {
                Debug.LogError($"不能添加一个空的页面内容到管理器中！", this);
                return;
            }

            // 添加页面
            _pageInfos.AddFirst(pageInfo);
            // 添加页面下标
            pageInfo.pageNumber = 0;
            // 更新后面页面的索引
            RefreshAllPageNumber();
            // 刷新页面
            FlipPage(currentPageIndex, true);
        }

        /// <summary>
        /// 添加页面在结尾
        /// </summary>
        public virtual void AddPageLast(PageInfo pageInfo)
        {
            if (pageInfo == null)
            {
                Debug.LogError($"不能添加一个空的页面内容到管理器中！", this);
                return;
            }
            // 添加页面下标
            pageInfo.pageNumber = _pageInfos.Count;
            // 添加页面
            _pageInfos.AddLast(pageInfo);
            // 刷新页面
            FlipPage(currentPageIndex, true);
        }
        
        /// <summary>
        /// 移除页面
        /// </summary>
        public virtual void RemovePage(int pageIndex)
        {
            if (0 > pageIndex || pageIndex > _pageInfos.Count)
            {
                Debug.LogError($"不能移除不在范围内的索引{pageIndex}！", this);
                return;
            }
            // 获取页面节点
            var page = _pageInfos.GetOrderNode().LandedIndex(pageIndex);
            // 更新后面页面的索引
            var tempPage = page;
            var tempPageNumber = page.Value.pageNumber; 
            while (tempPage.Next != null)
            {
                tempPage = tempPage.Next;
                tempPage.Value.pageNumber = tempPageNumber++;
            }
            // 如果移除的是当前页面，执行关闭操作
            if (currentPageNode == page)
            {
                page.Value.OnPageInvisible(true);
                currentPageNode = null;
            }
            _pageInfos.Remove(page); 
            // 刷新页面
            FlipPage(currentPageIndex, true);
        }

        /// <summary>
        /// 清除所有页面
        /// </summary>
        public virtual void CleanPage()
        {
            _pageInfos.Clear();
            currentPageNode = null;
            currentPageIndex = 0;
        }
        
        /// <summary>
        /// 交换AB页面
        /// </summary>
        public virtual void SwapPages(int pageIndexA, int pageIndexB)
        {
            var a = _pageInfos.GetOrderNode().LandedIndex(pageIndexA);
            var b = _pageInfos.GetOrderNode().LandedIndex(pageIndexB);
            // 交换节点
            _pageInfos.SwapNodes(a, b);
            // 交换页面索引
            (a.Value.pageNumber, b.Value.pageNumber) = (b.Value.pageNumber, a.Value.pageNumber);
            // 刷新页面
            FlipPage(currentPageIndex, true);
        }

        /// <summary>
        /// 刷新所有页面索引
        /// </summary>
        public void RefreshAllPageNumber()
        {
            var index = 0;
            foreach (var page in _pageInfos)
            {
                page.pageNumber = index++;
            }
        }
        
        #endregion
    }
}