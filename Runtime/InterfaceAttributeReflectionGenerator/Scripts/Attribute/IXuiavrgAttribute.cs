namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 反射 UI Attribute 标记接口
    /// 所有标记 Attribute 实现此接口，在元数据提取阶段按序执行 BindMetaData
    /// </summary>
    public interface IXuiavrgAttribute
    {
        /// <summary>
        /// 将 Attribute 的配置绑定到元数据对象
        /// </summary>
        /// <param name="metaData">目标元数据</param>
        public void BindMetaData(AvrgMemberMetaData metaData);
    }
}
