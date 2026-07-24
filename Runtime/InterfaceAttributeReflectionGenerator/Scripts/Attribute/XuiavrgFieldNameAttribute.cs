using System;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 字段显示名称特性
    /// 设置字段在 UI 上的显示标签、单位文本和格式化字符串
    /// 
    /// 使用示例：
    /// [XuiavrgFieldName("角色名称")]
    /// public string playerName = "勇者";
    /// 
    /// [XuiavrgFieldName("速度", "m/s", "{0:F1}")]
    /// public float speed = 5f;
    /// 
    /// 字段定义：
    /// - labelName: UI 上的显示标签（必填）
    /// - unitLabelName: 单位文本（可选），如 "m/s"、"%"、"px"
    /// - formatText: 格式化字符串（可选），如 "{0:F2}"，控制值在 UI 上的显示格式
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public class XuiavrgFieldNameAttribute : Attribute, IXuiavrgAttribute
    {
        /// <summary>字段的显示标签</summary>
        public readonly string LabelName;

        /// <summary>单位文本（可选）</summary>
        public readonly string UnitLabelName;

        /// <summary>格式化字符串（可选），如 "{0:F2}"</summary>
        public readonly string FormatText;

        /// <summary>
        /// 创建字段显示名称标记
        /// </summary>
        /// <param name="labelName">UI 上的显示标签</param>
        /// <param name="unitLabelName">可选的单位文本，如 "m/s"</param>
        /// <param name="formatText">可选的格式化字符串，如 "{0:F2}"</param>
        public XuiavrgFieldNameAttribute(string labelName, string unitLabelName = null, string formatText = null)
        {
            LabelName = labelName;
            UnitLabelName = unitLabelName;
            FormatText = formatText;
        }

        public void BindMetaData(AvrgMemberMetaData metaData)
        {
            metaData.LabelName = LabelName;
            metaData.UnitLabelName = UnitLabelName;
            metaData.FormatText = FormatText;
            // PrefabOverrideName 已移除 — 由类型+Tag 匹配替代
        }
    }
}
