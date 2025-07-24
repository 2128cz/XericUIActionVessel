using System;
using System.Collections.Generic;
using System.ComponentModel;
using JetBrains.Annotations;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;
using XericLibrary.Runtime.MacroLibrary;
using XericUI.Core.Vessel;
using Component = UnityEngine.Component;

namespace XericUI.FlipPage
{
    /// <summary>
    /// 翻页组件管理器
    ///
    /// 对翻页组件的管理
    /// </summary>
    public class FlipPageComponentManager : XericFlipPageIndexVesselBase
    {
        #region 界面属性

        /// <summary>
        /// 候选页数量
        /// </summary>
#if ODIN_INSPECTOR
        [LabelText("候选页按最多显示")]
        [BoxGroup("设定", centerLabel: true)]
#endif
        [Tooltip("候选页按最多数量展示")]
        [SerializeField]
        protected bool inMaximumCandidatesNumber = true;
        
        /// <summary>
        /// 首页按钮
        /// </summary>
#if ODIN_INSPECTOR
        [LabelText("首页按钮")] [BoxGroup("组件", centerLabel: true)]
#endif
        public ObjectVessel<Button> buttonStartPage;

        /// <summary>
        /// 上一页按钮
        /// </summary>
#if ODIN_INSPECTOR
        [LabelText("上一页按钮")] [BoxGroup("组件")]
#endif
        public ObjectVessel<Button> buttonPreviousPage;

        /// <summary>
        /// 下一页按钮
        /// </summary>
#if ODIN_INSPECTOR
        [LabelText("下一页按钮")] [BoxGroup("组件")]
#endif
        public ObjectVessel<Button> buttonNextPage;

        /// <summary>
        /// 下一页按钮
        /// </summary>
#if ODIN_INSPECTOR
        [LabelText("尾页按钮")] [BoxGroup("组件")]
#endif
        public ObjectVessel<Button> buttoneEndPage;

        /// <summary>
        /// 总页面数量文本
        /// </summary>
#if ODIN_INSPECTOR
        [Space(2)]
        [LabelText("总页面数量文本")] [BoxGroup("组件")]
#endif
        public ObjectVessel<TMPro.TMP_Text> labelTextTotalPageCount =
            new ObjectVessel<TMPro.TMP_Text>();
        
        /// <summary>
        /// 总页面数量文本格式化
        /// </summary>
#if ODIN_INSPECTOR
        [LabelText("总页面数量文本格式化")] [BoxGroup("设定")]
#endif
        public string totalPageCountFormat = "共$1页";
        
        /// <summary>
        /// 页面索引文本格式化
        /// </summary>
#if ODIN_INSPECTOR
        [LabelText("页面索引文本格式化")] [BoxGroup("设定")]
#endif
        public string pageIndexFormat = "$1";

        /// <summary>
        /// 页面选择组件列表
        /// </summary>
#if ODIN_INSPECTOR
        [LabelText("页面选择组件列表")] [BoxGroup("组件")]
        // [ListDrawerSettings(ShowIndexLabels = true)]
#endif
        [Tooltip("首个组件应当作为主要的节点管理")]
        public List<ObjectVessel<Selectable, TMPro.TMP_Text>> pageSelectButton =
            new List<ObjectVessel<Selectable, TMPro.TMP_Text>>();

        /// <summary>
        /// 页面跳转输入框
        /// </summary>
#if ODIN_INSPECTOR
        [LabelText("页面跳转输入框")] [BoxGroup("组件")]
#endif
        public ObjectVessel<TMPro.TMP_InputField> inputFieldFlipPageIndex;

        /// <summary>
        /// 翻页到索引按钮池
        /// </summary>
#if ODIN_INSPECTOR
        [LabelText("翻页到索引按钮池")] [BoxGroup("组件")]
#endif
        public XericLibrary.Runtime.MacroLibrary.MacroPool.UnionSet pageButtonPool = new XericLibrary.Runtime.MacroLibrary.MacroPool.UnionSet();

        #endregion

        #region 字段属性

        protected ToggleGroup ToggleGroup;

        #endregion
        
        #region 类内类型

        /// <summary>
        /// 页面与组件信息
        /// </summary>
        public class PageComponentInfo : PageInfo
        {
            /// <summary>
            /// 页面翻页按钮
            /// </summary>
#if ODIN_INSPECTOR
            [LabelText("页面翻页按钮")] 
#endif
            [CanBeNull]
            internal ObjectVessel<Selectable, TMPro.TMP_Text> pageIndexSelectable;
            
            protected internal override void OnPageVisible(bool quickVisit)
            {
                if (quickVisit)
                    return;

                // 自动补全外部交互动画
                if (pageIndexSelectable.IsValidVessel() && 
                    pageIndexSelectable.Target0 is Toggle toggle &&
                    !toggle.isOn)
                {
                    toggle.SetIsOnWithoutNotify(true);
                    
                }
            }

            protected internal override void OnPageInvisible(bool quickVisit)
            {
            }
        }

        /// <summary>
        /// 项目与组件信息
        /// </summary>
        public class ItemComponentInfo : ItemInfo
        {
            protected internal override void OnItemVisible()
            {
            }

            protected internal override void OnItemInvisible()
            {
            }
        }

        #endregion
        
        #region 生命周期

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            // pageButtonPool.Prefab.GetComponent<Selectable>();
        }
#endif
        
        protected override void Awake()
        {
            base.Awake();
            if (buttonStartPage.IsValidVessel()) // 翻到开头
                buttonStartPage.Target.onClick.AddListener(FlipPageToStart);
            if (buttonPreviousPage.IsValidVessel()) // 翻到上一页
                buttonPreviousPage.Target.onClick.AddListener(FlipPageToPrevious);
            if (buttonNextPage.IsValidVessel()) // 翻到下一页
                buttonNextPage.Target.onClick.AddListener(FlipPageToNext);
            if (buttoneEndPage.IsValidVessel()) // 翻到结尾
                buttoneEndPage.Target.onClick.AddListener(FlipPageToEnd);
            if (inputFieldFlipPageIndex.IsValidVessel()) // 翻到指定
                inputFieldFlipPageIndex.Target.onValueChanged.AddListener(s => FlipPage(s));
            pageButtonPool.Init();
            
            // 根据组件类型创建相关内容
            var prefabSelectable = pageButtonPool.Prefab.GetComponent<Selectable>();
            switch (prefabSelectable)
            {
                // 单选项就创建但选项组
                case Toggle toggle:
                    ToggleGroup = transform.GetComponentAnyway<ToggleGroup>();
                    break;
                case Button button: 
                    break;
                default: 
                    break;
            }
        }

        protected override void OnFlipPage()
        {
            base.OnFlipPage();

            
            // 刷新所有成员的层级顺序
            for (int i = 0; i < pageSelectButton.Count; i++)
            {
                pageSelectButton[i].Target0.transform.SetSiblingIndex(i);
            }
        }

        public override void FlipPage(int pageIndex, bool forceRefresh = false)
        {
            base.FlipPage(pageIndex, forceRefresh);
            
        }

        public override void AddPageFirst(PageInfo pageInfo)
        {
            if (pageInfo is not PageComponentInfo componentPageInfo)
            {
                Debug.LogError($"页面容器{currentPage}不支持当前管理器，其必须由{nameof(FlipPageComponentManager)}.{nameof(PageComponentInfo)}继承/声名，否则无法支持后续功能", this);
                return;
            }

            AddPageIndexButton(componentPageInfo);
            base.AddPageFirst(pageInfo);
            
            RefreshLabelTextTotalPageCount();
            RefreshPageIndeButtonState();
        }

        public override void AddPageLast(PageInfo pageInfo)
        {
            if (pageInfo is not PageComponentInfo componentPageInfo)
            {
                Debug.LogError($"页面容器{currentPage}不支持当前管理器，其必须由{nameof(FlipPageComponentManager)}.{nameof(PageComponentInfo)}继承/声名，否则无法支持后续功能", this);
                return;
            }

            AddPageIndexButton(componentPageInfo);
            base.AddPageLast(pageInfo);
            
            RefreshLabelTextTotalPageCount();
            RefreshPageIndeButtonState();
        }

        public override void RemovePage(int pageIndex)
        {
            base.RemovePage(pageIndex);

            pageButtonPool.DirectlyRelease(pageSelectButton[pageIndex].Target0.gameObject);
            pageSelectButton.RemoveAt(pageIndex);

            RefreshPageIndeButtonState();
        }

        /// <summary>
        /// 在页面上添加按钮
        /// </summary>
        /// <param name="pageInfo"></param>
        private void AddPageIndexButton(PageComponentInfo pageInfo)
        {
            var pageIndexSelectable = GetPageIndexButton();
            pageInfo.pageIndexSelectable = pageIndexSelectable;
        }

        /// <summary>
        /// 刷新所有页面按钮的可见性，索引
        /// </summary>
        private void RefreshPageIndeButtonState()
        {
            var min = currentPageIndex - candidatesNumber / 2;
            var max = currentPageIndex + candidatesNumber / 2;
            for (int i = 0; i < pageSelectButton.Count; i++)
            {
                pageSelectButton[i].Target0.transform.SetSiblingIndex(i);
                
                // 显示数量控制
                if (inMaximumCandidatesNumber)
                    pageSelectButton[i].Target0.gameObject.SetActive(min <= i && i <= max);
                else
                    pageSelectButton[i].Target0.gameObject.SetActive(min < i && i < max);
                
                // 更新索引
                if (pageSelectButton[i].Target0.gameObject.activeSelf)
                {
                    pageSelectButton[i].Target1.text = MacroRegex.RegexReplace(pageIndexFormat, i);
                }
            }   
        }
        
        /// <summary>
        /// 更新总页数
        /// </summary>
        private void RefreshLabelTextTotalPageCount()
        {
            if (labelTextTotalPageCount.IsValidVessel()) // 总页数更新 
                labelTextTotalPageCount.Target.text = MacroRegex.RegexReplace(totalPageCountFormat, PageInfos.Count.ToString());
        }

        #endregion

        #region 组件方法

        /// <summary>
        /// 获取页面索引按钮
        /// </summary>
        /// <returns></returns>
        protected virtual ObjectVessel<Selectable, TMPro.TMP_Text> GetPageIndexButton()
        {
            var obj = pageButtonPool.Get();
            var selectable = obj.GetComponent<Selectable>() ?? obj.GetComponentInChildren<Selectable>();
            var text = obj.GetComponent<TMPro.TMP_Text>() ?? obj.GetComponentInChildren<TMPro.TMP_Text>();
            var vessel = new ObjectVessel<Selectable, TMPro.TMP_Text>(selectable, text);
            return vessel;
        }
        
        protected virtual void ListenPageSelectable(ObjectVessel<Selectable, TMPro.TMP_Text> selectableVessel, Action<int> selectPageAction)
        {
            if (selectableVessel.Target0 is Button button)
            {
                button.onClick.AddListener(() =>
                {
                    selectPageAction?.Invoke(currentPageIndex);
                });
            }
        }
        
        #endregion
    }
}