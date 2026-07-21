using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif
using UnityEngine;
using UnityEngine.Pool;
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
    /// 管理器在添加数据时不能自己直接生成容器类型，但可以通过重写对象池，重设委托，或直接手动定义提供间接的页面类型；
    /// 页面类型是管理数据的一个重要组成部分，它被池化，便于在各个地方进行管理。
    ///
    /// 使用FlipPage()翻页，这会立刻执行，并触发OnFlipPage函数，
    /// currentPageIndex可以获取当前页数，Currentpage获取当前页面数据；
    /// 在页面数据中pageNumber可以获取自己所在的页数，itemInfos获取页面中的所有数据项目。
    /// </code>
    [AddComponentMenu("Xeric UI Vessel/FlipPage/Basic FilpPage Database")]
    public class XericFlipPageIndexVesselBase : XericUIBehaviour
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

        #region 字段属性

        /// <summary>
        /// 页面对象池，对象池仅管理页面本身，不参与任何数据容器的管理。
        /// </summary>
        private ObjectPool<PageInfo> _pageInfoPool = null;

        protected ObjectPool<PageInfo> PageInfoPool
        {
            get
            {
                if (_pageInfoPool != null)
                    return _pageInfoPool;
                _pageInfoPool = new ObjectPool<PageInfo>(CreatPageInfo, GetPageInfo, ReleasePageInfo, DestroyPageInfo);
                return _pageInfoPool;
            }
        }

        protected virtual Func<PageInfo> HowToCreatPageInfo => () => throw new Exception("需要翻页管理器实现相应的页面创建逻辑，并重写添加默认数据方法后才可以使用。");

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
        private readonly LinkedList<PageInfo> _pageInfos = new LinkedList<PageInfo>();

        /// <summary>
        /// 所有列表项目
        /// </summary>
        private readonly HashSet<ItemInfo> _itemInfos = HashSetPool<ItemInfo>.Get();


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
        public IEnumerable<ItemInfo> AllItemInfos => _pageInfos.SelectMany(page => page.ItemInfos);

        /// <summary>
        /// 页面默认最大项目数量
        /// </summary>
        protected int DefaultItemCountPerPage
            => defaultItemCountPerPage < 0 ? int.MaxValue : defaultItemCountPerPage;

        /// <summary>
        /// 分布所有页面上的所有项目数量
        /// </summary>
        public int TotalItemCount => AllItemInfos.Count();

        /// <summary>
        /// 所有页面数量
        /// </summary>
        public int TotalPageCount => PageInfos.Count();

#if UNITY_EDITOR && ODIN_INSPECTOR
        [LabelText("所有页面")]
        [Obsolete("显示调试列表 属性仅编辑时有效，不要调用")]
        private List<PageInfo> _pageInfoLists_ => _pageInfos.ToList();
#endif

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

        protected override void OnDestroy()
        {
            base.OnDestroy();
            HashSetPool<ItemInfo>.Release(_itemInfos);
        }

        #endregion

        #region 翻页管理

        /// <summary>
        /// 当发生翻页时回调 (这会在整个翻页动作流程的最后执行，翻页过程中回调会在此之前播放)
        /// </summary>
        protected virtual void OnFlipPage()
        {
        }

        /// <summary>
        /// 通过指令翻到页面
        /// </summary>
        /// <remarks>
        /// 手动输入页数时执行此方法。
        /// </remarks>
        /// <param name="pageNumber">页数编号（或其他命令，但需要手动实现处理，默认只处理页数）</param>
        /// <param name="forceRefresh">强制刷新（默认情况下如果当前页就是目标页时不会执行后续的翻页动作）</param>
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
        /// <remarks>
        /// 翻页时，当前页会触发一次 OnPageInvisible，目标页会触发一次 OnPageVisible；
        /// 翻到目标页数过程中除当前页和目标页之外的所有页面会触发一次OnPageVisible，OnPageInvisible的快速翻页；
        /// 在重写相关事件时，利用快速翻页状态可以跳过动画加载，仅执行相关的触发器。
        /// 如果压根不希望调用期间所有页面的翻页浏览事件，可以执行 JumpPage。
        /// </remarks>
        /// <param name="pageIndex">目标页数</param>
        /// <param name="forceRefresh">强制刷新（默认情况下如果当前页就是目标页时不会执行后续的翻页动作）</param>
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
            var quickPage = false; // 快速浏览页
            bool visitOffsetDirIsNext;
            var lastPage = CurrentPageNode;
            // 如果节点不存在，通过索引直接命中
            if (CurrentPageNode == null)
            {
                CurrentPageNode = _pageInfos.GetOrderNode().LandedIndex(currentPageIndex);
                if (CurrentPageNode == null)
                    Debug.LogError($"索引{currentPageIndex}下的节点不存在(总数{_pageInfos.Count}页), 请检查后再试", this);
                else
                    Currentpage.OnPageVisible(false);
            }
            // 如果索引偏移，逐个移动
            else if (pageIndexOffset != 0)
            {
                // 判断方向
                visitOffsetDirIsNext = pageIndexOffset > 0;
                pageIndexOffset = Mathf.Abs(pageIndexOffset);
                // 先关闭页面元素
                CurrentPageNode.Value.CallChildItemInvisible();
                for (var i = 1; i < pageIndexOffset; i++)
                    SwitchPage(false);
                SwitchPage(true);
            }
            // 没有翻动有效页，直接退出
            else
                return;

            // 可能时当前不存在任何页面
            if (CurrentPageNode == null)
                return;
            // 页面元素激活
            CurrentPageNode.Value.CallChildItemVisible();
            currentPageIndex = pageIndex;
            OnFlipPage();
            return;

            void SwitchPage(bool isEndPage)
            {
                Currentpage.OnPageInvisible(quickPage);
                quickPage = !isEndPage;
                // 先检查页面，再执行委托
                var nextPage = visitOffsetDirIsNext ? CurrentPageNode.Next : CurrentPageNode.Previous;
                // 如果都正常，执行生命周期
                if (nextPage is { Value: not null })
                {
                    nextPage.Value.OnPageVisible(quickPage);
                }
                // 如果跳转失败，说明到头了，检查是否允许循环索引
                else if (canPassIndexBoundary)
                {
                    CurrentPageNode = visitOffsetDirIsNext ? _pageInfos.First : _pageInfos.Last;
                    CurrentPageNode.Value.OnPageVisible(quickPage);
                }
                // 不允许循环索引就报个错然后什么都不做
                else
                {
                    Debug.LogWarning("翻页超出范围，由于页面不允许循环索引，所以现在翻到了空页面");
                    CurrentPageNode = null;
                    return;
                }

                CurrentPageNode = nextPage;
            }
        }

        /// <summary>
        /// 径直跳转到页 (skip quick Page)
        /// </summary>
        /// <remarks>
        /// 相比 FlipPage 方法，JumpPage会直接空降到页，不会调用期间其他页的浏览事件。
        /// </remarks>
        public virtual void JumpPage(int pageIndex, bool forceRefresh = false)
        {
            // 子项隐藏
            CurrentPageNode?.Value.CallChildItemInvisible();

            // 计算索引对应的页面
            var page = _pageInfos.First;
            while (pageIndex-- > 0 && page.Next != null)
                page = page.Next;
            // 只执行非快速浏览的回调
            Currentpage?.OnPageInvisible(false);
            CurrentPageNode = page;
            Currentpage?.OnPageVisible(false);
            currentPageIndex = pageIndex;
            // 子项显示
            CurrentPageNode?.Value.CallChildItemVisible();
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
        /// <remarks>
        /// 注意统一页面类型，如果没有指定自定义的页面，那么
        /// </remarks>
        public virtual PageInfo AddPage(PageInfo pageInfo = null, bool isFirstOtherwiseLast = false)
        {
            if (pageInfo == null)
                pageInfo = PageInfoPool.Get();

            if (isFirstOtherwiseLast)
            {
                // 添加页面(页面下标默认0)
                _pageInfos.AddFirst(pageInfo);
                // 更新后面页面的索引
                RefreshAllPageNumber();
            }
            else
            {
                // 添加页面下标
                pageInfo.pageNumber = _pageInfos.Count;
                // 添加页面
                _pageInfos.AddLast(pageInfo);
            }

            // GetPageInfo(pageInfo);
            // 执行页面生命周期
            pageInfo.OnPageCreated(this);
            // 刷新页面
            FlipPage(currentPageIndex, true);
            return pageInfo;
        }

        /// <summary>
        /// 按对象引用删除页面
        /// </summary>
        /// <param name="pageInfo"></param>
        public virtual void RemovePage(PageInfo pageInfo)
        {
            var index = PageInfos.IndexOf(a => a == pageInfo);
            RemovePage(index);
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
            // 更新删除后页面的索引
            var tempPage = page;
            var tempPageNumber = page.Value.pageNumber;
            while (tempPage.Next != null)
            {
                tempPage = tempPage.Next;
                tempPage.Value.pageNumber = tempPageNumber++;
            }

            // 刷新页面
            FlipPage(currentPageIndex, true);
        }

        /// <summary>
        /// 获取索引下的页面
        /// </summary>
        /// <param name="pageIndex"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T IndexOfPageInfo<T>(int pageIndex)
            where T : PageInfo
            => IndexOfPageInfo(pageIndex) as T;

        /// <summary>
        /// 获取索引下的页面
        /// </summary>
        /// <param name="pageIndex"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public PageInfo IndexOfPageInfo(int pageIndex)
            => _pageInfos.LandedIndex(pageIndex);

        /// <summary>
        /// 清除所有页面 (所有页面数据也会一同移除)
        /// </summary>
        /// <param name="resetPool">同时重置页面池</param>
        /// <remarks>
        /// 页面的引用会从管理器中释放，同时需要自行重写页面 OnPageDestroy 确保页面准确释放了各种东西。
        /// 除非你确认你的页面数据对象类型是一致的，否则无论如何都建议清空页面时也清空对象池，不然会带来严重的类型污染。
        /// </remarks>
#if ODIN_INSPECTOR
        [HorizontalGroup("数据"), Button("清除所有页面")]
#endif
        public virtual void ClearPages(bool resetPool = true)
        {
            if (CurrentPageNode != null)
                Currentpage.OnPageInvisible(true);

            var tempPageInfo = ListPool<PageInfo>.Get();
            tempPageInfo.AddRange(_pageInfos);
            foreach (var pageInfo in tempPageInfo)
            {
                pageInfo.ItemInfos.Clear();
                PageInfoPool.Release(pageInfo);
            }

            ListPool<PageInfo>.Release(tempPageInfo);
            // 清空池
            if (resetPool)
                PageInfoPool?.Clear();
            // 重置当前页面
            CurrentPageNode = null;
            currentPageIndex = 0;
            // 清空缓存
            _pageInfos.Clear();
            _itemInfos.Clear();
        }

        /// <summary>
        /// 关闭所有页面
        /// </summary>
        public void InactivePages() => SetPagesActive(false);

        /// <summary>
        /// 重新激活所有页面
        /// </summary>
        public void ReactivePages() => SetPagesActive(true);

        /// <summary>
        /// 设置所有页面的激活状态
        /// </summary>
        /// <param name="active"></param>
        public void SetPagesActive(bool active)
        {
            foreach (var pageInfo in _pageInfos)
                pageInfo.pageActive = active;
            FlipPage(0, true);
        }

        /// <summary>
        /// 使用自定义的方法设置激活状态
        /// </summary>
        /// <param name="isActive"></param>
        /// <typeparam name="T"></typeparam>
        public void SetPagesActive<T>(Func<T, bool> isActive)
            where T : PageInfo
        {
            foreach (var pageInfo in _pageInfos)
                pageInfo.pageActive = isActive((T)pageInfo);
            FlipPage(0, true);
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

        #region 数据项目池

        /// <summary>
        /// 定义对象池如何创建页面（建议直接覆盖委托）
        /// </summary>
        /// <returns></returns>
        protected virtual PageInfo CreatPageInfo() => HowToCreatPageInfo();

        /// <summary>
        /// 定义对象池如何执行获取页面后的事件
        /// </summary>
        /// <param name="info"></param>
        protected virtual void GetPageInfo(PageInfo info)
        {
            // 检查代码中重复调用
            if (!info.IsAlreadyRelease)
            {
                Debug.LogError($"流程错误，页面尝试重复获取，请检查代码流程({info.GetHashCode():X16})\n_>{info.name}");
                return;
            }

            info.OnPageCreated(this);
            return;
        }

        /// <summary>
        /// 定义对象池如何执行释放页面后的事件
        /// </summary>
        /// <param name="info"></param>
        protected virtual void ReleasePageInfo(PageInfo info)
        {
            // 检查代码中重复调用
            if (info.IsAlreadyRelease)
            {
                Debug.LogError("流程错误，页面尝试重复释放，请检查代码流程");
                return;
            }

            if (CurrentPageNode != null && Currentpage == info)
            {
                info.OnPageInvisible(true);
                CurrentPageNode = null;
            }

            info.OnPageDestroy(this);
            _pageInfos.Remove(info);
            return;
        }

        /// <summary>
        /// 定义对象池如何执行销毁页面后的事件
        /// </summary>
        /// <param name="info"></param>
        protected virtual void DestroyPageInfo(PageInfo info)
        {
        }

        #endregion

        #region 数据项目

        /// <summary>
        /// 添加一组数据项目(方法待优化)
        /// </summary>
        /// <param name="items"></param>
        /// <param name="customPage"></param>
        public virtual void AddItems(IEnumerable<ItemInfo> items, PageInfo customPage = null)
        {
            foreach (var item in items)
                AddItem(item, customPage);
        }

        /// <summary>
        /// 添加数据项目
        /// </summary>
        /// <param name="item"></param>
        /// <typeparam name="T">提供一个指定的数据类型，默认添加这个类型的页面</typeparam>
        public virtual void AddItem<T>(ItemInfo item)
            where T : PageInfo, new()
            => AddItem(item, new T());

        /// <summary>
        /// 添加数据项目
        /// </summary>
        /// <remarks>
        /// 添加项目到页面中
        /// </remarks>
        public virtual void AddItem(ItemInfo item, PageInfo customPage = null)
        {
            var page = _pageInfos.Count <= 0 ? null : _pageInfos.Last.Value;
            if (page != null &&
                page.CheckPageAnyLack(item) &&
                page.ItemInfos.Count < DefaultItemCountPerPage)
            {
                page.ItemInfos.Add(item);
                _itemInfos.Add(item);
                return;
            }

            // 新建一个页面存放数据
            page = customPage ?? PageInfoPool.Get();
            AddPage(page);
            page.ItemInfos.Add(item);
            _itemInfos.Add(item);
        }

        /// <summary>
        /// 移除所有包含给定项目的位置（这会在页面上留下空缺）
        /// </summary>
        /// <param name="item"></param>
        public virtual void RemoveItem(ItemInfo item)
        {
            _itemInfos.Remove(item);
            foreach (var a in AllItemInfos
                         .Adjoint()
                         .Where(a => a.obj == item)
                         .Reverse())
            {
                ItemIndex2PageIndex(a.index);
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
            => itemIndex / DefaultItemCountPerPage;

        /// <summary>
        /// 当前翻页管理器中包含给定数据项目
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool Contains(ItemInfo item)
            => _itemInfos.Contains(item);

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
            [SerializeField]
#if ODIN_INSPECTOR
            [LabelText("页面项目信息")]
#endif
            private List<ItemInfo> _itemInfos;

            public List<ItemInfo> ItemInfos
            {
                get => _itemInfos ??= ListPool<ItemInfo>.Get();
                set => _itemInfos = value;
            }

            /// <summary>
            /// 从零开始计数的页面下标
            /// </summary>
#if ODIN_INSPECTOR
            [LabelText("页面索引[0, n]")]
#endif
            public int pageNumber { get; internal set; }

            public bool pageActive;

            /// <summary>
            /// 随生命周期标识当前是否已经被释放
            /// </summary>
            public bool IsAlreadyRelease { get; internal set; } = true;

            #endregion

            #region 生命周期

            /// <summary>
            /// 页面创建时
            /// </summary>
            protected internal virtual void OnPageCreated(XericFlipPageIndexVesselBase parent)
            {
                IsAlreadyRelease = false;
            }

            /// <summary>
            /// 页面销毁时
            /// </summary>
            protected internal virtual void OnPageDestroy(XericFlipPageIndexVesselBase parent)
            {
                IsAlreadyRelease = true;
                // 从哈希表中移除
                foreach (var itemInfo in ItemInfos)
                    parent._itemInfos.Remove(itemInfo);
                // 释放列表
                if (ItemInfos != null)
                {
                    ListPool<ItemInfo>.Release(ItemInfos);
                    ItemInfos = null;
                }
            }

            /// <summary>
            /// 页面显示时回调
            /// </summary>
            /// <param name="quickVisit">快速浏览(用于区分动画和逻辑，为真时，不应当在动画事件后继续加载数据)</param>
            /// <remarks>
            /// 页面中的成员会自动执行其相应的激活函数，不需要在这里手动调用。
            /// 快速浏览主要为了区分当前执行的是动画事件还是纯逻辑。
            /// </remarks>
            protected internal abstract void OnPageVisible(bool quickVisit);

            /// <summary>
            /// 页面隐藏时回调
            /// </summary>
            /// <param name="quickVisit">快速浏览(用于区分动画和逻辑，为真时，不应当在动画事件后继续加载数据)</param>
            /// <remarks>
            /// 页面中的成员会自动执行其相应的激活函数，不需要在这里手动调用。
            /// 快速浏览主要为了区分当前执行的是动画事件还是纯逻辑。
            /// </remarks>
            protected internal abstract void OnPageInvisible(bool quickVisit);

            #endregion

            /// <summary>
            /// 子项激活回调（应呼叫子项激活）
            /// </summary>
            /// <remarks>
            /// 仅页面触发快速浏览，子项没有这个功能
            /// </remarks>
            protected internal virtual void CallChildItemVisible()
            {
                ItemInfos
                    .Where(a => a != null)
                    .ForEachDo(itemInfo => itemInfo.OnItemVisible());
            }

            /// <summary>
            /// 子项非激活回调（应呼叫子项隐藏）
            /// </summary>
            /// <remarks>
            /// 仅页面触发快速浏览，子项没有这个功能
            /// </remarks>
            protected internal virtual void CallChildItemInvisible()
            {
                ItemInfos
                    .Where(a => a != null)
                    .ForEachDo(itemInfo => itemInfo.OnItemInvisible());
            }

            /// <summary>
            /// 检查页面是否存在可以放下对象的空缺
            /// </summary>
            /// <returns></returns>
            protected internal virtual bool CheckPageAnyLack(ItemInfo item = null)
            {
                return true;
            }

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
            /// 当项目显示时回调
            /// </summary>
            /// <code>
            /// 这会在页面显示之后执行
            /// </code>
            protected internal abstract void OnItemVisible();

            /// <summary>
            /// 当项目隐藏时回调
            /// </summary>
            /// <code>
            /// 这会在页面关闭之前执行
            /// </code>
            protected internal abstract void OnItemInvisible();

            #endregion

            public override string ToString()
            {
                return $"[{nameof(ItemInfo)}] ({name})";
            }
        }

        #endregion
    }
}