using System;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 值绑定模板
    /// 为单个元数据成员创建的 MVVM 双向绑定，包含读/写委托和脏标记
    /// </summary>
    public class AvrgMvvmBinding
    {
        /// <summary>关联的元数据</summary>
        public AvrgMemberMetaData MetaData { get; set; }

        /// <summary>从数据源读取值的委托</summary>
        public Func<object> ReadFunc { get; set; }

        /// <summary>写入值到数据源的委托</summary>
        public Action<object> WriteAction { get; set; }

        // ============ 脏标记 ============

        /// <summary>UI 侧是否标记为脏</summary>
        public bool IsUIDirty { get; set; }

        /// <summary>数据侧是否标记为脏</summary>
        public bool IsDataDirty { get; set; }

        /// <summary>UI 侧写入的缓存值</summary>
        public object UICachedValue { get; set; }

        /// <summary>数据侧写入的缓存值</summary>
        public object DataCachedValue { get; set; }

        // ============ UI 绑定 ============

        /// <summary>UI 值变更回调委托 — UI 组件订阅此回调以更新显示</summary>
        public Action<object> OnUIDataChanged;

        /// <summary>通知 UI 更新值</summary>
        public void NotifyUIUpdate(object value)
        {
            OnUIDataChanged?.Invoke(value);
        }

        /// <summary>清除所有 UI 变更订阅</summary>
        public void ClearUIEvent()
        {
            OnUIDataChanged = null;
        }

        // ============ 便捷方法 ============

        /// <summary>
        /// UI 侧写入值（标记脏，不立即传播）
        /// </summary>
        public void WriteFromUI(object value)
        {
            UICachedValue = value;
            IsUIDirty = true;
        }

        /// <summary>
        /// 数据侧写入值（标记脏，不立即传播）
        /// </summary>
        public void WriteFromData(object value)
        {
            DataCachedValue = value;
            IsDataDirty = true;
        }

        /// <summary>
        /// 从数据源读取当前值
        /// </summary>
        public object ReadFromDataSource()
        {
            return ReadFunc?.Invoke();
        }

        /// <summary>
        /// 将值实际写入数据源
        /// </summary>
        public void CommitToDataSource(object value)
        {
            WriteAction?.Invoke(value);
        }

        /// <summary>
        /// 重置脏标记
        /// </summary>
        public void ClearDirty()
        {
            IsUIDirty = false;
            IsDataDirty = false;
            UICachedValue = null;
            DataCachedValue = null;
        }
    }
}
