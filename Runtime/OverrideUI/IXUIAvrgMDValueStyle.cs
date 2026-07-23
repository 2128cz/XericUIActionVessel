namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// MDVM 值组件样式扩展接口
    /// 实现此接口的脚本可挂载到 XUIAvrgMDValue 预制体的子节点上，
    /// 在元数据绑定阶段由 XUIAvrgMDValue 自动调用 ApplyMetaData
    /// </summary>
    public interface IXUIAvrgMDValueStyle
    {
        /// <summary>
        /// 应用元数据以控制组件样式
        /// </summary>
        /// <param name="metaData">当前绑定的元数据</param>
        void ApplyMetaData(AvrgMemberMetaData metaData);
    }
}
