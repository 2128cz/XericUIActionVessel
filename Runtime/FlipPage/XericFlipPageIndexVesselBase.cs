using System;
using System.Collections.Generic;
using System.Linq;
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
    /// 翻页管理器(逻辑层)
    /// </summary>
    /// <code>
    /// 翻页逻辑层脚本，约束基本用法：
    /// 
    /// XericFlipPageIndexVesselBase 逻辑层管理器
    /// |-PageInfo 页面数据结构
    /// | |-ItemInfo 项目数据结构
    /// 
    /// 基本用法应该是，添加数据项目，这会自动分配到有效的页面中；
    /// 如果页面项目已满，则新建一个页面继续填充项目。
    /// 或者直接创建页面，将需要的数据项目直接写在页面数据中。
    ///
    /// 使用FlipPage()翻页，这会立刻执行，并触发OnFlipPage函数，
    /// currentPageIndex可以获取当前页数，Currentpage获取当前页面数据；
    /// 在页面数据中pageNumber可以获取自己所在的页数，itemInfos获取页面中的所有数据项目。
    /// </code>
    [AddComponentMenu("Xeric UI Vessel/FlipPage/Basic FilpPage Database")]
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
        private int defaultItemCountPerPage = -1;

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
            /// 页面创建时
            /// </summary>
            protected internal virtual void OnPageCreated(XericFlipPageIndexVesselBase parent)
            {
                
            }

            /// <summary>
            /// 页面销毁时
            /// </summary>
            protected internal virtual void OnPageDestroy(XericFlipPageIndexVesselBase parent)
            {
                
            }

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

            /// <summary>
            /// 从零开始计数的项目下标
            /// </summary>
#if ODIN_INSPECTOR
            [LabelText("项目索引[0, n]")]
#endif
            public int itemNumber { get; internal set; }

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

            
            /// <summary>
            /// 是否通过过滤器
            /// </summary>
            /// <param name="manager">传入管理器，建议将过滤器放在管理器中，然后具体的通过规则写在这里，根据对象的不同，通过的规则都不一样。</param>
            /// <returns></returns>
            public virtual bool PassFilter(XericFlipPageIndexVesselBase manager)
            {
                return true;
            }
            
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

#if UNITY_EDITOR && ODIN_INSPECTOR
        /// <summary>
        /// 显示调试列表
        /// </summary>
        private List<PageInfo> PageInfoLists => _pageInfos.ToList();
#endif

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
        protected LinkedListNode<PageInfo> CurrentPageNode = null;

        /// <summary>
        /// 所有页面
        /// </summary>
        public LinkedList<PageInfo> PageInfos => _pageInfos;

        /// <summary>
        /// 当前选中页面内容
        /// </summary>
        public PageInfo Currentpage => CurrentPageNode?.Value;

        /// <summary>
        /// 分布于所有页面上的所有数据项目
        /// </summary>
        public IEnumerable<ItemInfo> AllItemInfos => _pageInfos.SelectMany(page => page.itemInfos);
        
        /// <summary>
        /// 页面默认最大项目数量
        /// </summary>
        protected int DefaultItemCountPerPage 
            => defaultItemCountPerPage < 0 ? int.MaxValue : defaultItemCountPerPage;

        
        private List<ItemInfo> _backupItemInfos;
        public List<ItemInfo> BackItemInfos => _backupItemInfos;


        /// <summary>
        /// 分布所有页面上的所有项目数量
        /// </summary>
        public int TotalItemCount => AllItemInfos.Count();

        /// <summary>
        /// 所有页面数量
        /// </summary>
        public int TotalPageCount => PageInfos.Count();
        
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
                FlipPage((int)index - 1, forceRefresh);
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
                pageIndex = MacroMath.PMod(pageIndex, _pageInfos.Count);
            // 检查翻页数量有效
            if (!forceRefresh && currentPageIndex == pageIndex)
                return;
            // 翻页计算
            var pageIndexOffset = pageIndex - currentPageIndex;
            var lastPageIndex = currentPageIndex;
            // 如果节点不存在，通过索引寻找
            if (CurrentPageNode == null)
                CurrentPageNode = _pageInfos.GetOrderNode().LandedIndex(currentPageIndex);
            // 如果索引偏移，逐个移动
            else if (pageIndexOffset > 0)
            {
                for (var i = 0; i < pageIndexOffset; i++)
                {
                    var next = CurrentPageNode!.Next;
                    SwitchPage(next, _pageInfos.Last, i + 1 < pageIndexOffset);
                }
            }
            else if (pageIndexOffset < 0)
            {
                for (var i = 0; i > pageIndexOffset; i--)
                {
                    var previa = CurrentPageNode!.Previous;
                    SwitchPage(previa, _pageInfos.First, i - 1 > pageIndexOffset);
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
                    Currentpage.OnPageInvisible(quickPage);
                    CurrentPageNode = newPageNode;
                    Currentpage.OnPageVisible(quickPage);
                }

                if (canPassIndexBoundary)
                {
                    Currentpage.OnPageInvisible(quickPage);
                    CurrentPageNode = defualtPageNode;
                    Currentpage.OnPageVisible(quickPage);
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
        public virtual PageInfo AddPageFirst(PageInfo pageInfo)
        {
            if (pageInfo == null)
            {
                Debug.LogError($"不能添加一个空的页面内容到管理器中！", this);
                return null;
            }
            // 添加页面下标
            pageInfo.pageNumber = 0;
            // 添加页面
            _pageInfos.AddFirst(pageInfo);
            // 更新后面页面的索引
            RefreshAllPageNumber();
            // 执行页面生命周期
            pageInfo.OnPageCreated(this);
            // 刷新页面
            FlipPage(currentPageIndex, true);
            return pageInfo;
        }

        /// <summary>
        /// 添加页面在结尾
        /// </summary>
        public virtual PageInfo AddPageLast(PageInfo pageInfo)
        {
            if (pageInfo == null)
            {
                Debug.LogError($"不能添加一个空的页面内容到管理器中！", this);
                return null;
            }
            // 添加页面下标
            pageInfo.pageNumber = _pageInfos.Count;
            // 添加页面
            _pageInfos.AddLast(pageInfo);
            // 执行页面生命周期
            pageInfo.OnPageDestroy(this);
            // 刷新页面
            FlipPage(currentPageIndex, true);
            return pageInfo;
        }
        
        /// <summary>
        /// 移除页面（页面上的数据内容也会一同移除）
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
            if (CurrentPageNode == page)
            {
                page.Value.OnPageInvisible(true);
                CurrentPageNode = null;
            }
            // 执行页面生命周期
            page.Value.OnPageDestroy(this);
            // 页面索引移除
            _pageInfos.Remove(page); 
            // 刷新页面
            FlipPage(currentPageIndex, true);
        }

        /// <summary>
        /// 仅移除页面，不会移除数据
        /// </summary>
        /// <param name="pageIndex"></param>
        public void RemovePageWithoutItems(int pageIndex)
        {
            BackupItemsFormPages();
            RemovePage(pageIndex);
            RestoreItemsToPages();
        }
        
        /// <summary>
        /// 获取索引下的页面
        /// </summary>
        /// <param name="pageIndex"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T IndexOfPageInfo<T>(int pageIndex)
            where T : PageInfo
            => _pageInfos.LandedIndex(pageIndex) as T;
        
        /// <summary>
        /// 清除所有页面 (所有页面数据也会一同移除)
        /// </summary>
#if ODIN_INSPECTOR
        [HorizontalGroup("数据"), Button("清除所有页面")]
#endif
        public virtual void ClearPages()
        {
            _pageInfos.Clear();
            CurrentPageNode = null;
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

        #region 数据项目

        /// <summary>
        /// 添加数据项目
        /// </summary>
        /// <param name="item"></param>
        /// <exception cref="Exception"></exception>
        public virtual void AddItem(ItemInfo item)
        {
            AddItem(item, () => throw new Exception("需要翻页管理器实现相应的页面创建逻辑，并重写添加默认数据方法后才可以使用。"));
        }
        
        /// <summary>
        /// 添加数据项目
        /// </summary>
        public void AddItem(ItemInfo item, Func<PageInfo> addPage)
        {
            // 向前寻找空缺（这在页面都被准确填充的情况下最忧）
            var firstVacancy = _pageInfos
                .GetReversedOrderNode()
                .LastOrDefault(a
                    => a.Value.itemInfos.Count < DefaultItemCountPerPage);
            if (firstVacancy != null)
            {
                firstVacancy.Value.itemInfos.Add(item);
                return;
            }
            if (addPage == null)
            {
                Debug.LogError("当前页面已满，数据项目顺位添加到新页面中，但未指定页面创建函数，添加过程已退出", this);
                return;
            }
            // 新建一个页面存放数据
            addPage.Invoke().itemInfos.Add(item);
        }

        /// <summary>
        /// 移除所有包含给定项目的位置（这会在页面上留下空缺）
        /// </summary>
        /// <param name="item"></param>
        public void RemoveItem(ItemInfo item)
        {
            foreach (var a in AllItemInfos
                         .Adjoint()
                         .Where(a => a.obj == item)
                         .Reverse())
            {
                ItemIndex2PageIndex(a.index);
            }
        }

        public void RemoveItem(int indexItem)
        {
            
        }

        /// <summary>
        /// 保留页面的状态下清空所有数据
        /// </summary>
#if ODIN_INSPECTOR
        [HorizontalGroup("数据"), Button("清除所有数据")]
#endif
        public void ClearItemsWithoutPages()
        {
            foreach (var page in _pageInfos)
            {
                page.itemInfos.Clear();
            }
        }
        
        /// <summary>
        /// 使用索引定位到数据
        /// </summary>
        /// <param name="itemIndex"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T IndexOfItemInfo<T>(int itemIndex)
            where T : ItemInfo
            => AllItemInfos.LandedIndex(itemIndex) as T;

        /// <summary>
        /// 将项目索引转为页面索引
        /// </summary>
        /// <param name="itemIndex"></param>
        /// <returns></returns>
        public virtual int ItemIndex2PageIndex(int itemIndex)
        {
            return itemIndex / DefaultItemCountPerPage;
        }
        
        /// <summary>
        /// 获取备份所有数据项目
        /// </summary>
        /// <param name="backupAtLocal">是否在本地备份？（默认启用，启用后返回的枚举器内容来自备份数据，允许推迟处理过程，但也会覆盖上一次备份）</param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public IEnumerable<T> BackupItemsFormPages<T>(bool backupAtLocal = true)
            where T : ItemInfo
        {
            if (backupAtLocal)
            {
                BackupItemsFormPages();
                return _backupItemInfos.OfType<T>();
            }
            return AllItemInfos.OfType<T>();
        }
        
        /// <summary>
        /// 备份所有数据项目
        /// </summary>
        public List<ItemInfo> BackupItemsFormPages()
        {
            _backupItemInfos = AllItemInfos.ToList();
            return _backupItemInfos;
        }

        /// <summary>
        /// 强制重排所有数据项目（从备份中恢复数据）
        /// </summary>
        public void RestoreItemsToPages()
        {
            RestoreItemsToPages(_backupItemInfos);
        }
        /// <summary>
        /// 强制重排所有数据项目（从备份中通过过滤器恢复数据）
        /// </summary>
        public void RestoreItemsByFilterToPages()
        {
            RestoreItemsToPages(_backupItemInfos.Where(a => a.PassFilter(this)).ToList());
        }
        
        /// <summary>
        /// 强制重排所有数据项目（从给定备份中恢复数据）
        /// </summary>
        public void RestoreItemsToPages<T>(List<T> overrideDatas)
            where T : ItemInfo
        {
            if (overrideDatas == null || overrideDatas.Count <= 0)
            {
                Debug.LogError($"重排数据需要一个有效的数据对象列表，如果希望将数据重置为空，可以使用Clear方法，而不是重排操作。", this);
                return;
            }
            if (ItemIndex2PageIndex(overrideDatas.Count - 1) > _pageInfos.Count)
            {
                Debug.LogError($"重排数据时，原有数据数量({overrideDatas.Count})超出现有面板数量({_pageInfos.Count})，已拒绝重排", this);
                return;
            }
            // 执行重排
            var page = _pageInfos.First;
            page.Value.itemInfos.Clear();
            var pageIndex = 0;
            for (int i = 0; i < overrideDatas.Count; i++)
            {
                var newPageIndex = ItemIndex2PageIndex(i);
                if (pageIndex != newPageIndex)
                {
                    pageIndex = newPageIndex;
                    page = page.Next;
                    if (page == null)
                    {
                        Debug.LogError($"重排数据时，页面链接错误，重排被迫中断", this);
                        return;
                    }
                    page.Value.itemInfos.Clear();
                }
                page.Value.itemInfos.Add(overrideDatas[i]);
            }
        }

        #endregion
        
    }
}