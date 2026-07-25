using System;
using System.Collections.Generic;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 值绑定模板
    /// 为单个元数据成员创建的 MVVM 双向绑定，包含读/写委托和脏标记
    /// 支持通过 SubBindings 实现拆分属性的子绑定
    /// </summary>
    public class AvrgMvvmBinding
    {
        /// <summary>关联的元数据</summary>
        public AvrgMemberMetaData MetaData { get; set; }

        /// <summary>从数据源读取值的委托</summary>
        public Func<object> ReadFunc { get; set; }

        /// <summary>写入值到数据源的委托</summary>
        public Action<object> WriteAction { get; set; }

        /// <summary>
        /// 值访问器引用（在 BuildMDVM 时创建，供 UI 组件使用）
        /// 提供 ValueType / IsReadOnly / FormatDisplay 等扩展能力
        /// </summary>
        public IAvrgValueAccessor Accessor { get; set; }

        /// <summary>
        /// 拆分属性子绑定列表
        /// 由 [XuiavrgSplit] 特性展开生成，每个子绑定对应一个拆分后的子属性
        /// </summary>
        public List<AvrgMvvmBinding> SubBindings { get; set; }

        // ============ 脏标记 ============

        /// <summary>UI 侧是否标记为脏</summary>
        public bool IsUIDirty { get; set; }

        /// <summary>数据侧是否标记为脏</summary>
        public bool IsDataDirty { get; set; }

        /// <summary>UI 侧写入的缓存值</summary>
        public object UICachedValue { get; set; }

        /// <summary>数据侧写入的缓存值</summary>
        public object DataCachedValue { get; set; }

        // ============ 验证状态 ============

        private ValidationState _validationState = ValidationState.Normal;

        /// <summary>当前验证状态</summary>
        public ValidationState CurrentValidationState => _validationState;

        /// <summary>验证状态变更事件 — UI 组件订阅以切换样式</summary>
        public event Action<ValidationState> OnValidationStateChanged;

        /// <summary>
        /// 校验值是否满足元数据约束
        /// 返回：Normal（通过）、Warning（放行但警告）、Invalid（驳回）
        /// </summary>
        public ValidationState ValidateValue(object value)
        {
            if (MetaData == null) return ValidationState.Normal;

            // 1. 空值检查（Required）
            if (MetaData.Required && (value == null || (value is string s && string.IsNullOrEmpty(s))))
                return ValidationState.Invalid;

            // 2. 范围检查（RangeMin/RangeMax）
            if (value is IConvertible)
            {
                float num = Convert.ToSingle(value);

                if (MetaData.RangeMin.HasValue && num < MetaData.RangeMin.Value)
                    return ValidationState.Invalid;

                if (MetaData.RangeMax.HasValue && num > MetaData.RangeMax.Value)
                    return ValidationState.Invalid;
            }

            return ValidationState.Normal;
        }

        /// <summary>更新验证状态并触发回调（仅状态变化时触发）</summary>
        internal void SetValidationState(ValidationState newState)
        {
            if (_validationState != newState)
            {
                _validationState = newState;
                OnValidationStateChanged?.Invoke(_validationState);
            }
        }

        /// <summary>重置验证状态为 Normal（不触发事件）</summary>
        internal void ResetValidationState()
        {
            _validationState = ValidationState.Normal;
        }

        // ============ UI 绑定 ============

        /// <summary>UI 值变更回调委托 — UI 组件订阅此回调以更新显示（附带验证状态）</summary>
        public Action<object, ValidationState> OnUIDataChanged;

        /// <summary>通知 UI 更新值（带验证状态）</summary>
        public void NotifyUIUpdate(object value, ValidationState state)
        {
            OnUIDataChanged?.Invoke(value, state);
        }

        /// <summary>清除所有 UI 变更订阅</summary>
        public void ClearUIEvent()
        {
            OnUIDataChanged = null;
            OnValidationStateChanged = null;
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
