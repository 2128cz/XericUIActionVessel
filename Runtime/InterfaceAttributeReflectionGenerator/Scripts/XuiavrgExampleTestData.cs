using System;
using System.Collections.Generic;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 测试用枚举 - 用于下拉选择测试
    /// </summary>
    public enum XuiavrgTestQuality
    {
        Low,
        Medium,
        High,
        Epic,
        Legendary
    }

    /// <summary>
    /// 反射界面生成器测试数据类
    /// 涵盖新版 Entry 体系的所有特性：编组、排序、预制体覆盖、多种字段类型
    /// 
    /// 使用方式：
    /// 1. 在 Hierarchy 右键 -&gt; Xeric UI -&gt; Create Reflection UI Example 创建示例节点
    /// 2. 创建 XuiavrgPropertyGroupScriptableObject 并设置 groupName 和 supportedTags
    /// 3. 将编组对应的预制体拖入
    /// 4. 调用 manager.BuildEntries(new XuiavrgExampleTestData()) 构建 Entry
    /// 5. 调用 manager.GenerateUI() 生成界面
    /// 或直接使用兼容方法: manager.SchemaHydration(new XuiavrgExampleTestData())
    /// </summary>
    [Serializable]
    public class XuiavrgExampleTestData
    {
        // ==================== 基础数值类型 ====================

        [XuiavrgFieldOrder(1)]
        [XuiavrgNumberCheck(0, 100)]
        public int intValue = 42;

        [XuiavrgFieldOrder(2)]
        [XuiavrgNumberCheck(0f, 999f)]
        public float floatValue = 3.14f;

        [XuiavrgFieldOrder(3)]
        public double doubleValue = 123.456;

        [XuiavrgFieldOrder(4)]
        [XuiavrgNumberCheck(0, 999999)]
        public long longValue = 100000;

        // ==================== 多字段编组示例 ====================
        // 以下三个字段合并为一个 Entry，共享一个 UI 组件实例
        // UI 组件上需要有 Label、Context、Unit 对应的 IXuiavrgFieldTagProvider

        [XuiavrgFieldEntryGroup("speed", XuiavrgFieldTag.Label)]
        [XuiavrgFieldOrder(5)]
        public string speedLabel = "速度";

        [XuiavrgFieldEntryGroup("speed", XuiavrgFieldTag.Context)]
        [XuiavrgNumberCheck(-999f, 999f)]
        public float speedValue = 0.1f;

        [XuiavrgFieldEntryGroup("speed", XuiavrgFieldTag.Unit)]
        public string speedUnit = "m/s";

        // 第二个编组示例 - 健康值
        [XuiavrgFieldEntryGroup("health", XuiavrgFieldTag.Label)]
        [XuiavrgFieldOrder(6)]
        public string healthLabel = "生命值";

        [XuiavrgFieldEntryGroup("health", XuiavrgFieldTag.Context)]
        [XuiavrgNumberCheck(0, 9999)]
        public float healthValue = 100f;

        [XuiavrgFieldEntryGroup("health", XuiavrgFieldTag.Unit)]
        public string healthUnit = "HP";

        // ==================== 字符串类型 ====================

        [XuiavrgFieldOrder(7)]
        [XuiavrgRequired]
        [XuiavrgStringCheck(minLength: 2, maxLength: 16)]
        public string playerName = "DefaultName";

        [XuiavrgFieldOrder(8)]
        [XuiavrgNotEmpty]
        [XuiavrgStringCheck(onlyEmail: false)]
        public string emailAddress = "example@mail.com";

        [XuiavrgFieldOrder(9)]
        [XuiavrgFieldPrefabOverride("MultilineTextArea")]
        [XuiavrgStringCheck(maxLength: 500)]
        public string biography = "这是一段个人简介...";

        // ==================== 布尔类型 ====================

        [XuiavrgFieldOrder(10)]
        public bool enableFeature = true;

        [XuiavrgFieldOrder(11)]
        public bool advancedMode = false;

        // ==================== 枚举类型 ====================

        [XuiavrgFieldOrder(12)]
        public XuiavrgTestQuality quality = XuiavrgTestQuality.High;

        // ==================== 隐藏属性 ====================

        [XuiavrgHide(true)]
        public string hiddenField = "此字段被隐藏，不会生成 Entry";

        // ==================== 不同尺寸 ====================

        [XuiavrgFieldOrder(13)]
        [XuiavrgAttribute("小号", "size_small", null, size: XuiavrgSize.Small)]
        public string smallSizeField = "小尺寸";

        [XuiavrgFieldOrder(14)]
        [XuiavrgAttribute("大号", "size_large", null, size: XuiavrgSize.Large)]
        public string largeSizeField = "大尺寸";

        // ==================== 集合类型（兼容旧标记） ====================

        [XuiavrgFieldOrder(15)]
        [XuiavrgCollectionCheck(1, 10, true)]
        public List<string> tags = new List<string> { "Unity", "C#", "UI" };

        // ==================== 按钮触发器 ====================

        [XuiavrgFieldOrder(16)]
        public bool submitTrigger = false;
    }
}
