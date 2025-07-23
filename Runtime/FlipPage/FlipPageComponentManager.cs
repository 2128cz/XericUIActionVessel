using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;
using XericUI.Core.Vessel;

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
        [LabelText("总页面数量文本")] [BoxGroup("组件")]
#endif
        public ObjectVessel<TMPro.TMP_Text> labelTextTotalPageCount =
            new ObjectVessel<TMPro.TMP_Text>();

        /// <summary>
        /// 总页面数量文本
        /// </summary>
#if ODIN_INSPECTOR
        [LabelText("总页面数量文本")] [BoxGroup("组件")]
        // [ListDrawerSettings(ShowIndexLabels = true)]
#endif
        public List<ObjectVessel<Button, TMPro.TMP_Text>> pageSelectButton =
            new List<ObjectVessel<Button, TMPro.TMP_Text>>();

        /// <summary>
        /// 页面跳转输入框
        /// </summary>
#if ODIN_INSPECTOR
        [LabelText("页面跳转输入框")] [BoxGroup("组件")]
#endif
        public ObjectVessel<TMPro.TMP_InputField> inputFieldFlipPageIndex =
            new ObjectVessel<TMPro.TMP_InputField>();

        /// <summary>
        /// 翻页到索引按钮池
        /// </summary>
#if ODIN_INSPECTOR
        [LabelText("翻页到索引按钮池")] [BoxGroup("组件")]
#endif
        public XericLibrary.Runtime.MacroLibrary.MacroPool.UnionSet pageButtonPool = new XericLibrary.Runtime.MacroLibrary.MacroPool.UnionSet();

        #endregion

        #region 生命周期

        protected override void Awake()
        {
            base.Awake();
            if (buttonStartPage.IsValid()) // 翻到开头
                buttonStartPage.Target.onClick.AddListener(FlipPageToStart);
            if (buttonPreviousPage.IsValid()) // 翻到上一页
                buttonPreviousPage.Target.onClick.AddListener(FlipPageToPrevious);
            if (buttonNextPage.IsValid()) // 翻到下一页
                buttonNextPage.Target.onClick.AddListener(FlipPageToNext);
            if (buttoneEndPage.IsValid()) // 翻到结尾
                buttoneEndPage.Target.onClick.AddListener(FlipPageToEnd);
            pageButtonPool.Init();
        }

        private void Update()
        {
            if (labelTextTotalPageCount.IsValid()) // 页数 
                labelTextTotalPageCount.Target.text = PageInfos.Count.ToString();
        }

        public override void FlipPage(int pageIndex, bool forceRefresh = false)
        {
            base.FlipPage(pageIndex, forceRefresh);
            
        }

        public override void AddPage(PageInfo pageInfo)
        {
            base.AddPage(pageInfo);
            pageSelectButton.Add(GetPageIndexButton());
        }

        #endregion

        #region 组件方法

        /// <summary>
        /// 获取页面索引按钮
        /// </summary>
        /// <returns></returns>
        protected virtual ObjectVessel<Button, TMPro.TMP_Text> GetPageIndexButton()
        {
            var obj = pageButtonPool.Get();
            var button = obj.GetComponent<Button>() ?? obj.GetComponentInChildren<Button>();
            var text = obj.GetComponent<TMPro.TMP_Text>() ?? obj.GetComponentInChildren<TMPro.TMP_Text>();
            // var vessel = new ObjectVessel<Button, TMPro.TMP_Text>(button, text);
            // return vessel;
            return null;
        }

        /// <summary>
        /// 释放页面索引按钮
        /// </summary>
        protected virtual void ReleasePageIndexButton(Component comp)
        {
            pageButtonPool.IndirectRelease(comp);
        }

        #endregion
    }
}