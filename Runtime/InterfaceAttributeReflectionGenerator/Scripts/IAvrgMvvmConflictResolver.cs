namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 冲突解决器接口
    /// 用户可实现此接口注册自定义冲突解决逻辑
    /// </summary>
    public interface IAvrgMvvmConflictResolver
    {
        /// <summary>
        /// 尝试解决冲突
        /// </summary>
        /// <param name="binding">发生冲突的绑定</param>
        /// <param name="defaultStrategy">当前配置的默认策略</param>
        /// <param name="resolvedValue">返回解决后的值</param>
        /// <returns>true 表示已解决，false 表示未处理（回退到默认策略）</returns>
        bool TryResolve(AvrgMvvmBinding binding, ConflictResolutionStrategy defaultStrategy, out object resolvedValue);
    }
}
