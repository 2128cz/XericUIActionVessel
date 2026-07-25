using System;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 字段 UI 功能标记枚举
    /// 用于在数据对象字段和 UI 组件之间建立功能映射
    /// 每个标记代表 UI 组件上的一种功能元素
    /// </summary>
    [Flags]
    public enum XuiavrgFieldTag
    {
        None        = 0,

        /// <summary>标题文本 / Label</summary>
        Label       = 1 << 0,

        /// <summary>内容值 / 数值上下文</summary>
        Context     = 1 << 1,

        /// <summary>单位文本</summary>
        Unit        = 1 << 2,

        /// <summary>布尔开关 / Toggle</summary>
        Toggle      = 1 << 3,

        /// <summary>按钮触发器</summary>
        Button      = 1 << 4,

        /// <summary>滑块 / 进度条</summary>
        Slider      = 1 << 5,

        /// <summary>下拉选择</summary>
        Dropdown    = 1 << 6,

        /// <summary>输入值 / InputField</summary>
        Input       = 1 << 7,

        /// <summary>单选项 / Radio</summary>
        Select      = 1 << 8,

        /// <summary>符号 / 图标</summary>
        Sign        = 1 << 9,

        /// <summary>编辑区域 / 多行文本</summary>
        Edit        = 1 << 10,

        /// <summary>输出区域 / 只读展示</summary>
        Output      = 1 << 11,

        /// <summary>颜色 / Color</summary>
        Color       = 1 << 12,

        /// <summary>纹理预览 / Texture</summary>
        Texture     = 1 << 13,

        /// <summary>向量值组 / Vector (Vector2/3/4)</summary>
        Vector      = 1 << 14,

        /// <summary>开关组 / ToggleGroup</summary>
        ToggleGroup = 1 << 15,

        /// <summary>只读展示 / ReadOnly</summary>
        ReadOnly    = 1 << 16,
    }
}
