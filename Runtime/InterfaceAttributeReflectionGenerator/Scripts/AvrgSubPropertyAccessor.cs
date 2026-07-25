using System;
using System.Reflection;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 子属性值访问器 — 通过父访问器 + PropertyPath 访问结构的子字段
    /// 
    /// 用于拆分属性（[XuiavrgSplit]）场景，如 Vector3 的 x/y/z 分量。
    /// GetValue 通过反射读取父值的子字段/属性；SetValue 由绑定层的 WriteAction 处理。
    /// </summary>
    internal class AvrgSubPropertyAccessor : IAvrgValueAccessor
    {
        private readonly IAvrgValueAccessor _parentAccessor;
        private readonly string _propertyPath;
        private readonly AvrgMemberMetaData _metaData;

        public AvrgSubPropertyAccessor(
            IAvrgValueAccessor parentAccessor,
            string propertyPath,
            AvrgMemberMetaData metaData)
        {
            _parentAccessor = parentAccessor ?? throw new ArgumentNullException(nameof(parentAccessor));
            _propertyPath = propertyPath ?? throw new ArgumentNullException(nameof(propertyPath));
            _metaData = metaData;
        }

        public Type ValueType => _metaData?.ValueType ?? typeof(float);

        public bool IsReadOnly => _parentAccessor.IsReadOnly;

        public object GetValue()
        {
            var parentValue = _parentAccessor.GetValue();
            if (parentValue == null || string.IsNullOrEmpty(_propertyPath))
                return null;

            var type = parentValue.GetType();
            var field = type.GetRuntimeField(_propertyPath);
            if (field != null) return field.GetValue(parentValue);

            var prop = type.GetRuntimeProperty(_propertyPath);
            return prop?.GetValue(parentValue);
        }

        /// <summary>
        /// 子属性设置由绑定层的 WriteAction 统一处理（ReadSubProperty/WriteSubProperty），
        /// 此方法不应被直接调用。
        /// </summary>
        public void SetValue(object value)
        {
            var parentValue = _parentAccessor.GetValue();
            if (parentValue == null) return;

            var type = parentValue.GetType();
            var field = type.GetRuntimeField(_propertyPath);
            if (field != null)
            {
                field.SetValue(parentValue, value);
                _parentAccessor.SetValue(parentValue);
                return;
            }

            var prop = type.GetRuntimeProperty(_propertyPath);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(parentValue, value);
                _parentAccessor.SetValue(parentValue);
            }
        }

        public string FormatDisplay(object value)
        {
            if (_metaData?.FormatText != null && value is IFormattable formattable)
                return formattable.ToString(_metaData.FormatText, null);
            return value?.ToString() ?? "";
        }
    }
}
