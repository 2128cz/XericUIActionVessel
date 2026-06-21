using System;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 预制体覆盖特性
    /// 覆盖对应 Entry 或普通字段生成 UI 时使用的预制体名称
    /// 
    /// 使用示例：
    /// [XuiavrgFieldPrefabOverride("CustomSpeedPrefab")] public float speed;
    /// 
    /// Manager 在 GenerateUI 阶段会优先使用此名称查找 PropertyGroup
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public class XuiavrgFieldPrefabOverrideAttribute : Attribute, IXuiavrgAttribute
    {
        /// <summary>覆盖的预制体组名称</summary>
        public readonly string PrefabGroupName;

        /// <summary>
        /// 创建预制体覆盖标记
        /// </summary>
        /// <param name="prefabGroupName">
        /// 目标预制体组名称，与 XuiavrgPropertyGroupScriptableObject.groupName 匹配
        /// </param>
        public XuiavrgFieldPrefabOverrideAttribute(string prefabGroupName)
        {
            PrefabGroupName = prefabGroupName;
        }

        public void BindProperty(XuiavrgInspectorProperty property)
        {
            // 新的 Entry 体系通过 Manager 直接处理此 Attribute
        }
    }
}
