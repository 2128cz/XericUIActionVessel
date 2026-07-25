using System;
using UnityEngine;
using UnityEngine.UI;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// Slider 值组件 — 处理 float / int / double 类型的滑块交互
    /// 适用 Shader Range 属性或标记了 [XuiavrgFieldTag(Slider)] 的字段
    /// </summary>
    [AddComponentMenu("Xeric Library/UI Action Vessel/Values/XUIAvrgMDSliderValue")]
    public class XUIAvrgMDSliderValue : XUIAvrgMDValue
    {
        [SerializeField, Tooltip("关联的 Slider 组件（未设置则自动查找）")]
        private Slider _slider;

        [Header("Slider 专属插槽")]
        [SerializeField, Tooltip("增加按钮（点击按步进值 +）")]
        private Button _incrementButton;

        [SerializeField, Tooltip("减少按钮（点击按步进值 -）")]
        private Button _decrementButton;

#if dUI_TextMeshPro
        [SerializeField, Tooltip("值显示标签（可选，未设置则不更新）")]
        private TMPro.TMP_Text _valueLabel;
#endif

        private const float DefaultStep = 0.1f;

        private void Awake()
        {
            if (_slider == null) _slider = GetComponent<Slider>();
        }

        // ============ 对象池生命周期 ============

        public override void OnPoolGet(AvrgMemberMetaData metaData)
        {
            base.OnPoolGet(metaData);

            if (_slider == null) return;

            // 从元数据初始化 Slider 参数
            if (metaData?.RangeMin.HasValue == true)
                _slider.minValue = metaData.RangeMin.Value;
            if (metaData?.RangeMax.HasValue == true)
                _slider.maxValue = metaData.RangeMax.Value;
            _slider.wholeNumbers = metaData?.ValueType == typeof(int);
        }

        public override void OnPoolRelease()
        {
            // 清除按钮监听
            if (_incrementButton != null)
                _incrementButton.onClick.RemoveAllListeners();
            if (_decrementButton != null)
                _decrementButton.onClick.RemoveAllListeners();
            if (_slider != null)
                _slider.onValueChanged.RemoveAllListeners();

            base.OnPoolRelease();
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

            // 数据 → UI（带验证状态）
            binding.OnUIDataChanged += (value, state) =>
            {
                float sliderVal = Convert.ToSingle(value);
                _slider.SetValueWithoutNotify(sliderVal);

#if dUI_TextMeshPro
                if (_valueLabel != null)
                    _valueLabel.text = accessor.FormatDisplay(value);
#endif

                // 委托基类处理标签合成 + 值文本
                base.UpdateValueDisplay(value, state);
            };

            // 验证状态变更
            binding.OnValidationStateChanged += OnValidationStateChanged;

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
            UpdateValueDisplay(initialValue ?? 0f, ValidationState.Normal);
        }

        // ============ 步进值 ============

        private float GetStepValue()
        {
            return _slider.wholeNumbers ? 1f : DefaultStep;
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

        // ============ 验证状态回调 ============

        protected override void OnValidationStateChanged(ValidationState state)
        {
            base.OnValidationStateChanged(state);

            // Slider 填充色随验证状态变化
            if (_slider != null)
            {
                var fillArea = _slider.fillRect;
                if (fillArea != null && fillArea.TryGetComponent<Image>(out var fill))
                {
                    fill.color = state switch
                    {
                        ValidationState.Invalid => Color.red,
                        ValidationState.Warning => new Color(1f, 0.92f, 0.016f),
                        _ => Color.white
                    };
                }
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
