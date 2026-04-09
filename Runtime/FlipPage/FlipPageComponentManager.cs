using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Deconstruction.Runtime.UI;
using JetBrains.Annotations;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using XericLibrary.Runtime.MacroLibrary;
using XericUI.Core.Vessel;
using Component = UnityEngine.Component;

namespace XericUI.FlipPage
{
    /// <summary>
    /// 翻页组件管理器(UI层)
    /// </summary>
    /// <code>
    /// 翻页组件层（UI）脚本，约束基本用法：
    /// 
    /// FlipPageComponentManager : XericFlipPageIndexVesselBase 
    /// |-PageComponentInfo 页面组件结构
    /// | |-ItemComponentInfo 项目组件结构
    ///
    /// 组件层使用组件容器封装，预设的组件引用可以实现类似下面的样式
    /// |＜  ＜ 2,3,4 ＞  ＞|
    /// 两侧是固定的按钮，中间是一个横向的排列容器，在指定了按钮对象池后，将会自动生成绑定好的按钮用于翻页。
    /// 页数按钮需要让button在预制体最外层，它的子集中需要包含一个TMP_Text。  
    /// </code>
    [AddComponentMenu("Xeric UI Vessel/FlipPage/Basic FilpPage Manager")]
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
        protected bool inMaximumCandidatesNumber = false;

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
        [Space(2)] [LabelText("总页面数量文本")] [BoxGroup("组件")]
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
        [LabelText("页面选择组件列表")]
        [BoxGroup("组件")]
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
        public XericLibrary.Runtime.MacroLibrary.MacroPool.UnionSet pageButtonPool =
            new XericLibrary.Runtime.MacroLibrary.MacroPool.UnionSet();

        #endregion

        #region 字段属性

        private ToggleGroup _toggleGroup;

        protected ToggleGroup ToggleGroup
        {
            get
            {
                if (_toggleGroup != null)
                    return _toggleGroup;
                _toggleGroup = transform.GetComponentAnyway<ToggleGroup>();
                return _toggleGroup;
            }
        }

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
            internal ObjectVessel<Selectable, TMPro.TMP_Text> pageSelectableVessel;


            protected internal override void OnPageVisible(bool quickVisit)
            {
                if (quickVisit) return;

                // 自动补全外部交互动画
                if (pageSelectableVessel.IsValidVessel())
                    SwitchSelectableVesselType(pageSelectableVessel.Target0,
                        asToggle: toggle =>
                        {
                            if (!toggle.isOn)
                                toggle.SetIsOnWithoutNotify(true);
                        });
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
            // 使用默认的
            HowToCreatPageInfo = () => new PageComponentInfo();
            
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
            pageButtonPool.Initialize();
            pageButtonPool.joinOnRelease += o =>
            {
                SwitchSelectableVesselType(o.GetComponent<Selectable>(), 
                    button => button.onClick.RemoveAllListeners(),
                    toggle => toggle.onValueChanged.RemoveAllListeners());
            };
            // 根据组件类型创建相关内容
            // var prefabSelectable = pageButtonPool.Prefab.GetComponent<Selectable>();
        }

        #endregion

        #region 翻页功能
        
        protected override void OnFlipPage()
        {
            base.OnFlipPage();

            // 刷新所有成员的层级顺序
            for (int i = 0; i < pageSelectButton.Count; i++)
            {
                pageSelectButton[i].Target0.transform.SetSiblingIndex(i);
            }

            if (Currentpage is PageComponentInfo componentPage &&
                componentPage.pageSelectableVessel.IsValidVessel())
            {
                SwitchSelectableVesselType(componentPage.pageSelectableVessel.Target0,
                    asToggle: toggle =>
                    {
                        if (!toggle.isOn)
                            toggle.isOn = true;
                    });
            }
        }

        public override void FlipPage(int pageIndex, bool forceRefresh = false)
        {
            base.FlipPage(pageIndex, forceRefresh);

            RefreshPageIndeButtonState();
        }
        
        public override PageInfo AddPage(PageInfo pageInfo = null, bool isFirstOtherwiseLast = false)
        {
            if (pageInfo is not PageComponentInfo componentPageInfo)
            {
                Debug.LogError(
                    $"页面容器{Currentpage}不支持当前管理器，其必须由{nameof(FlipPageComponentManager)}.{nameof(PageComponentInfo)}继承/声名，否则无法支持后续功能",
                    this);
                return null;
            }

            // 页面基础初始化
            var result = base.AddPage(pageInfo, isFirstOtherwiseLast);
            // 添加选择按钮
            AddPageIndexButton(componentPageInfo);
            // 刷新选择按钮显示状态
            RefreshPageIndeButtonState();
            // 刷新总页数
            RefreshLabelTextTotalPageCount();
            return result;
        }

        public override void RemovePage(int pageIndex)
        {
            base.RemovePage(pageIndex);

            pageButtonPool.DirectlyRelease(pageSelectButton[pageIndex].Target0.gameObject);
            pageSelectButton.RemoveAt(pageIndex);

            RefreshPageIndeButtonState();
            RefreshLabelTextTotalPageCount();
        }

        public override void ClearPages(bool resetPool = true)
        {
            PageInfos
                .OfType<PageComponentInfo>()
                .Where(a => a.pageSelectableVessel.IsValidVessel())
                .ForEachDo(a => pageButtonPool.DirectlyRelease(a.pageSelectableVessel!.Target0.gameObject));
            pageSelectButton.Clear();
            base.ClearPages(resetPool);
            RefreshLabelTextTotalPageCount();
        }

        /// <summary>
        /// 添加页面对应的翻页按钮
        /// </summary>
        /// <param name="pageInfo"></param>
        protected virtual void AddPageIndexButton(PageComponentInfo pageInfo)
        {
            // 检查页面上的按钮引用
            ObjectVessel<Selectable, TMP_Text> pageIndexSelectable = null;
            if (pageInfo.pageSelectableVessel == null)
            {
                pageIndexSelectable = GetPageIndexButton();
                pageInfo.pageSelectableVessel = pageIndexSelectable;
            }
            else
                pageIndexSelectable = pageInfo.pageSelectableVessel;

            // 将按钮对象添加到列表中便于管理
            pageSelectButton.Add(pageIndexSelectable);

            SwitchSelectableVesselType(pageIndexSelectable.Target0,
                button =>
                {
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(() => FlipPage(pageInfo.pageNumber));
                },
                toggle =>
                {
                    toggle.SetIsOnWithoutNotify(pageInfo.pageNumber == currentPageIndex);
                    toggle.group = ToggleGroup;
                    toggle.onValueChanged.RemoveAllListeners();
                    toggle.onValueChanged.AddListener(b =>
                    {
                        if (b) FlipPage(pageInfo.pageNumber);
                    });
                });
            RefreshPageSelectableVesselLabel(pageInfo);
        }

        /// <summary>
        /// 刷新所有页面按钮的可见性，索引
        /// </summary>
        private void RefreshPageIndeButtonState()
        {
            var min = currentPageIndex - (int)(candidatesNumber / 2);

            min = Math.Max(min, 0);
            min = pageSelectButton.Count - candidatesNumber > 0
                ? Math.Min(min, (int)(pageSelectButton.Count - candidatesNumber))
                : 0;
            var max = min + (int)candidatesNumber;

            for (int i = 0; i < pageSelectButton.Count; i++)
            {
                pageSelectButton[i].Target0.transform.SetSiblingIndex(i);

                // 显示数量控制
                if (inMaximumCandidatesNumber)
                    pageSelectButton[i].Target0.gameObject.SetActive(min <= i && i <= max);
                else
                    pageSelectButton[i].Target0.gameObject.SetActive(min <= i && i < max);
                // 看不见的内容不刷新
                if (!pageSelectButton[i].Target0.gameObject.activeSelf)
                    continue;

                // 更新索引
                pageSelectButton[i].Target1.text = MacroRegex.RegexReplace(pageIndexFormat, i + 1);

                // SwitchSelectableVesselType(pageSelectButton[i].Target0,
                //     null,
                //     toggle =>
                //     {
                //         toggle.SetIsOnWithoutNotify(currentPageIndex == i);
                //     });
            }
        }

        /// <summary>
        /// 更新总页数
        /// </summary>
        private void RefreshLabelTextTotalPageCount()
        {
            if (labelTextTotalPageCount.IsValidVessel()) // 总页数更新 
                labelTextTotalPageCount.Target.text =
                    MacroRegex.RegexReplace(totalPageCountFormat, PageInfos.Count.ToString());
        }

        /// <summary>
        /// 刷新翻页按钮索引为当前页面索引
        /// </summary>
        public void RefreshPageSelectableVesselLabel(PageComponentInfo page, object customInput = null)
        {
            if (page.pageSelectableVessel.IsValidVessel())
                page.pageSelectableVessel!.Target1.text =
                    MacroRegex.RegexReplace(pageIndexFormat, customInput ?? page.pageNumber);
        }

        #endregion

        #region 组件方法

        /// <summary>
        /// 获取页面索引按钮(获取这个按钮的池化对象，随后应当将其绑定到页面上)
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

        /// <summary>
        /// 注册一个委托到翻页按钮上。(事件会在调用容器清空后被自动回收)
        /// </summary>
        /// <param name="selectableVessel"></param>
        /// <param name="selectPageAction"></param>
        protected virtual void ListenPageSelectable(ObjectVessel<Selectable, TMPro.TMP_Text> selectableVessel,
            Action<int> selectPageAction)
        {
            if (selectableVessel.Target0 is Button button)
            {
                button.onClick.AddListener(() => { selectPageAction?.Invoke(currentPageIndex); });
            }
        }

        
        
        /// <summary>
        /// 处理可选对象类型.(如果要支持更多的类型，应优先改此方法，以便获得其他所有位置的编译器提示)
        /// </summary>
        /// <param name="selectable"></param>
        /// <param name="asButton">如果是按钮</param>
        /// <param name="asToggle">如果是单选项</param>
        /// <returns></returns>
        private static bool SwitchSelectableVesselType(Selectable selectable, 
            Action<Button> asButton = null,
            Action<Toggle> asToggle = null)
        {
            switch (selectable)
            {
                case CircleSlider circleSlider: break; // asCircleSlider?.Invoke(circleSlider); break;
                case TMP_Dropdown tmpDropdown: break; // asTmpDropdown?.Invoke(tmpDropdown); break;
                case TMP_InputField tmpInputField: break; // asTmpInputField?.Invoke(tmpInputField); break;
                case Button button:
                    asButton?.Invoke(button);
                    return true;
                case Dropdown dropdown: break; // asDropdown?.Invoke(dropdown); break;
                case InputField inputField: break; // asInputField?.Invoke(inputField); break;
                case Scrollbar scrollbar: break; // asScrollbar?.Invoke(scrollbar); break;
                case Slider slider: break; // asSlider?.Invoke(slider); break;
                case Toggle toggle:
                    asToggle?.Invoke(toggle);
                    return true;
                default: break;
            }

            Debug.LogError($"目前不支持的可选类型:{selectable}");
            return false;
        }

        #endregion
    }
}