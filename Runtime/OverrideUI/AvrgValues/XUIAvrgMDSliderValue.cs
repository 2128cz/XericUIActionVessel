using System;
using UnityEngine;
using UnityEngine.UI;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// Slider 值组件 — 处理 float / int / double 类型的滑块交互
    /// 适用于 Shader Range 属性或标记了 [XuiavrgFieldTag(Slider)] 的字段
    /// </summary>
    [AddComponentMenu("Xeric Library/UI Action Vessel/Values/XUIAvrgMDSliderValue")]
    public class XUIAvrgMDSliderValue : XUIAvrgMDValue
    {
        [SerializeField]
        [Tooltip("关联的 Slider 组件（未设置则自动查找）")]
        private Slider _slider;

        [SerializeField]
        [Tooltip("值显示标签（可选，未设置则不更新）")]
        private TMPro.TMP_Text _valueLabel;

        private void Awake()
        {
            if (_slider == null) _slider = GetComponent<Slider>();
        }

        public override void BindToAccessor(
            IAvrgValueAccessor accessor,
            AvrgMvvmBinding binding,
            AvrgMvvmScheduler scheduler)
        {
            CurrentAccessor = accessor;
            CurrentBinding = binding;

            if (binding == null || accessor == null || _slider == null) return;

            // 设置 Slider 参数
            _slider.wholeNumbers = accessor.ValueType == typeof(int);

            if (BoundMetaData != null)
            {
                if (BoundMetaData.RangeMin.HasValue)
                    _slider.minValue = BoundMetaData.RangeMin.Value;
                if (BoundMetaData.RangeMax.HasValue)
                    _slider.maxValue = BoundMetaData.RangeMax.Value;
            }

            // UI → 数据
            _slider.onValueChanged.AddListener(val =>
            {
                object convertedVal = ConvertValueForAccessor(val, accessor.ValueType);
                binding.WriteFromUI(convertedVal);
                scheduler?.MarkUIDirty(binding);

                if (_valueLabel != null)
                    _valueLabel.text = accessor.FormatDisplay(convertedVal);
            });

            // 数据 → UI
            binding.OnUIDataChanged += value =>
            {
                float sliderVal = Convert.ToSingle(value);
                _slider.SetValueWithoutNotify(sliderVal);

                if (_valueLabel != null)
                    _valueLabel.text = accessor.FormatDisplay(value);
            };

            // 首次同步
            var initialValue = accessor.GetValue();
            if (initialValue != null)
            {
                _slider.SetValueWithoutNotify(Convert.ToSingle(initialValue));
                if (_valueLabel != null)
                    _valueLabel.text = accessor.FormatDisplay(initialValue);
            }
        }

        private static object ConvertValueForAccessor(float val, Type targetType)
        {
            if (targetType == typeof(int)) return (int)val;
            if (targetType == typeof(double)) return (double)val;
            return val;
        }
    }
}
