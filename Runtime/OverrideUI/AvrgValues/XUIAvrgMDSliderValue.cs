using System;
using UnityEngine;
using UnityEngine.UI;
using XericLibrary.Runtime.SuperStyleSheet;

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

        [Header("Slider 专属插槽")]
        [SerializeField, Tooltip("增加按钮（点击按步进值 +）")]
        private Button _incrementButton;

        [SerializeField, Tooltip("减少按钮（点击按步进值 -）")]
        private Button _decrementButton;

#if dUI_TextMeshPro
        [SerializeField]
        [Tooltip("值显示标签（可选，未设置则不更新）")]
        private TMPro.TMP_Text _valueLabel;
#endif

        // ============ 样式表参数 ============
        private readonly StyleMember _styleDefaultStep = new() { StylePath = "xuiavrg/slider/defaultStep" };
        private readonly StyleMember _styleMinStep     = new() { StylePath = "xuiavrg/slider/minStep" };
        private readonly StyleMember _styleMaxStep     = new() { StylePath = "xuiavrg/slider/maxStep" };

        // ============ 生命周期 ============

        private void Awake()
        {
            if (_slider == null) _slider = GetComponent<Slider>();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _styleDefaultStep.Register();
            _styleMinStep.Register();
            _styleMaxStep.Register();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            _styleDefaultStep.Unregister();
            _styleMinStep.Unregister();
            _styleMaxStep.Unregister();
        }

        // ============ 核心绑定 ============

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

            // 增减按钮
            if (_incrementButton != null)
                _incrementButton.onClick.AddListener(OnIncrementClicked);
            if (_decrementButton != null)
                _decrementButton.onClick.AddListener(OnDecrementClicked);

            // UI → 数据
            _slider.onValueChanged.AddListener(val =>
            {
                object convertedVal = ConvertValueForAccessor(val, accessor.ValueType);
                binding.WriteFromUI(convertedVal);
                scheduler?.MarkUIDirty(binding);

#if dUI_TextMeshPro
                if (_valueLabel != null)
                    _valueLabel.text = accessor.FormatDisplay(convertedVal);
#endif
            });

            // 数据 → UI
            binding.OnUIDataChanged += value =>
            {
                float sliderVal = Convert.ToSingle(value);
                _slider.SetValueWithoutNotify(sliderVal);

#if dUI_TextMeshPro
                if (_valueLabel != null)
                    _valueLabel.text = accessor.FormatDisplay(value);
#endif
            };

            // 首次同步
            var initialValue = accessor.GetValue();
            if (initialValue != null)
            {
                _slider.SetValueWithoutNotify(Convert.ToSingle(initialValue));
#if dUI_TextMeshPro
                if (_valueLabel != null)
                    _valueLabel.text = accessor.FormatDisplay(initialValue);
#endif
            }

            RefreshCommonNodes();
        }

        // ============ 步进值 ============

        private float GetStepValue()
        {
            var val = GetStyleValue(_styleDefaultStep);
            if (val != null)
            {
                float step = val.GetValueAsFloat();
                float min = GetStyleValue(_styleMinStep)?.GetValueAsFloat() ?? 0.001f;
                float max = GetStyleValue(_styleMaxStep)?.GetValueAsFloat() ?? 1000f;
                return Mathf.Clamp(step, min, max);
            }
            return _slider.wholeNumbers ? 1f : 0.1f;
        }

        private void OnIncrementClicked()
        {
            if (CurrentAccessor?.IsReadOnly == true || _slider == null) return;
            float step = GetStepValue();
            float current = Convert.ToSingle(CurrentAccessor.GetValue());
            float newVal = Mathf.Clamp(current + step, _slider.minValue, _slider.maxValue);
            _slider.value = newVal;
        }

        private void OnDecrementClicked()
        {
            if (CurrentAccessor?.IsReadOnly == true || _slider == null) return;
            float step = GetStepValue();
            float current = Convert.ToSingle(CurrentAccessor.GetValue());
            float newVal = Mathf.Clamp(current - step, _slider.minValue, _slider.maxValue);
            _slider.value = newVal;
        }

        private static object ConvertValueForAccessor(float val, Type targetType)
        {
            if (targetType == typeof(int)) return (int)val;
            if (targetType == typeof(double)) return (double)val;
            return val;
        }
    }
}
