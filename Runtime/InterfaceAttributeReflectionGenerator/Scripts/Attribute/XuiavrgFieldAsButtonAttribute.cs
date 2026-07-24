using System;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 字段作为按钮（Button）的专用特性
    /// 自动设置 Tag = Button，无额外配置参数
    /// 
    /// 使用示例：
    /// [XuiavrgFieldAsButton]
    /// public bool trigger;
    /// </summary>
    public class XuiavrgFieldAsButtonAttribute : XuiavrgFieldAsAttribute
    {
        public override XuiavrgFieldTag TargetTag => XuiavrgFieldTag.Button;
    }
}
