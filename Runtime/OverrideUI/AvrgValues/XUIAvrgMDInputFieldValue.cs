using System;
using UnityEngine;
using UnityEngine.UI;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// InputField 值组件 — 处理 string / float / int 类型的文本输入交互
    /// </summary>
    [AddComponentMenu("Xeric Library/UI Action Vessel/Values/XUIAvrgMDInputFieldValue")]
    public class XUIAvrgMDInputFieldValue : XUIAvrgMDValue
    {
        [SerializeField]
        [Tooltip("关联的 InputField 组件（未设置则自动查找）")]
        private InputField _inputField;

        private void Awake()
        {
            if (_inputField == null) _inputField = GetComponent<InputField>();
        }

        public override void BindToAccessor(
            IAvrgValueAccessor accessor,
            AvrgMvvmBinding binding,
            AvrgMvvmScheduler scheduler)
        {
            CurrentAccessor = accessor;
            CurrentBinding = binding;

            if (binding == null || accessor == null || _inputField == null) return;

            // 设置输入类型
            if (accessor.ValueType == typeof(float) || accessor.ValueType == typeof(int)
                || accessor.ValueType == typeof(double))
            {
                _inputField.contentType = InputField.ContentType.DecimalNumber;
            }

            // UI → 数据
            _inputField.onEndEdit.AddListener(val =>
            {
                object convertedVal = ConvertStringToValue(val, accessor.ValueType);
                binding.WriteFromUI(convertedVal);
                scheduler?.MarkUIDirty(binding);
            });

            // 数据 → UI
            binding.OnUIDataChanged += value =>
            {
                _inputField.SetTextWithoutNotify(accessor.FormatDisplay(value));
            };

            // 首次同步
            var initialValue = accessor.GetValue();
            _inputField.SetTextWithoutNotify(accessor.FormatDisplay(initialValue));
        }

        private static object ConvertStringToValue(string s, Type targetType)
        {
            if (targetType == typeof(string)) return s;
            if (targetType == typeof(float) && float.TryParse(s, out float fVal)) return fVal;
            if (targetType == typeof(int) && int.TryParse(s, out int iVal)) return iVal;
            if (targetType == typeof(double) && double.TryParse(s, out double dVal)) return dVal;
            return s;
        }
    }
}
