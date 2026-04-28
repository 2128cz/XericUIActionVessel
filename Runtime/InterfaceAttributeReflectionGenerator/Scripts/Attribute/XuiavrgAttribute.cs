using System;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 反射属性标记
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class XuiavrgAttribute : Attribute, IXuiavrgAttribute
    {
        #region 字段属性

        public readonly string LabelName;
        public readonly string UIPropertyName;
        public readonly string UnitLabelName;
        public readonly int InspectorOrder;
        public readonly string GroupName;
        public readonly string OnTriggerCall;
        public readonly XuiavrgSize ArgumentSize;

        /// <summary>
        /// 标记属性
        /// </summary>
        /// <param name="labelName">属性显示名称</param>
        /// <param name="uiPropertyName">属性标记名（对应界面元素名称）</param>
        /// <param name="unitLabelName">属性显示单位名称</param>
        /// <param name="mustFillin">是否标记为必填项目</param>
        /// <param name="inspectorOrder">属性显示顺序</param>
        /// <param name="groupName">属性组名称（可以是以/&\分割的路径）</param>
        /// <param name="onTriggerCall">属性附加的动作，调用此内联反射调用的函数</param>
        /// <param name="size">属性组名称（可以是以/&\分割的路径）</param>
        public XuiavrgAttribute(string labelName, string uiPropertyName, string unitLabelName, 
            int inspectorOrder = 0, string groupName = null, string onTriggerCall = "", XuiavrgSize size = XuiavrgSize.Normal)
        {
            LabelName = labelName;
            UIPropertyName = uiPropertyName;
            UnitLabelName = unitLabelName;
            InspectorOrder = inspectorOrder;
            GroupName = groupName;
            OnTriggerCall = onTriggerCall;
            ArgumentSize = size;
        }

        #endregion

        #region 生成器

        public void BindProperty(XuiavrgInspectorProperty property)
        {
            property.labelName = LabelName;
            property.uiPropertyName = UIPropertyName;
            property.unitLabelName = UnitLabelName;
            property.inspectorOrder = InspectorOrder;
            property.groupName = GroupName;
            property.onTriggerCall = OnTriggerCall;
            property.argumentSize = ArgumentSize;
        }

        #endregion
    }

    /// <summary>
    /// 属性尺寸
    /// </summary>
    public enum XuiavrgSize
    {
        None = 0,

        Tiny = 1000 - 2,
        Small = 1000 - 1,
        Normal = 1000,
        Big,
        Large,
    }
}