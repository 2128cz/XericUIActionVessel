using System;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 字段作为滑动条（Slider）的专用特性
    /// 自动设置 Tag = Slider，并携带最小值和最大值
    /// 
    /// 使用示例：
    /// [XuiavrgFieldAsSlider(-100f, 100f)]
    /// public float speed;
    /// 
    /// 对于 Vector2 类型（范围滑块），UI 端可通过 ValueType 判断切换 MinMax 模式，
    /// 此时 Slider 的 Min/Max 作为边界值
    /// 
    /// UI 侧查询方式：
    /// metaData.Attributes.OfType<XuiavrgFieldAsSliderAttribute>()
    ///     .FirstOrDefault()
    /// </summary>
    public class XuiavrgFieldAsSliderAttribute : XuiavrgFieldAsAttribute
    {
        /// <summary>滑动条最小值</summary>
        public float MinValue { get; }

        /// <summary>滑动条最大值</summary>
        public float MaxValue { get; }

        public override XuiavrgFieldTag TargetTag => XuiavrgFieldTag.Slider;

        /// <summary>
        /// 创建滑动条专用特性
        /// </summary>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        public XuiavrgFieldAsSliderAttribute(float min, float max)
        {
            MinValue = min;
            MaxValue = max;
        }
    }
}
