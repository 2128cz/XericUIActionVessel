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
        [SerializeField, Tooltip("值显示文本 — 显示当前 Slider 数值")]
#if dUI_TextMeshPro
        private TMPro.TMP_Text _valueDisplayText;
#else
        private UnityEngine.UI.Text _valueDisplayText;
#endif

        [SerializeField, Tooltip("最小值文本 — 显示 Slider 下限")]
#if dUI_TextMeshPro
        private TMPro.TMP_Text _minValueText;
#else
        private UnityEngine.UI.Text _minValueText;
#endif

        [SerializeField, Tooltip("最大值文本 — 显示 Slider 上限")]
#if dUI_TextMeshPro
        private TMPro.TMP_Text _maxValueText;
#else
        private UnityEngine.UI.Text _maxValueText;
#endif

        [SerializeField, Tooltip("增加按钮（点击按步进值 +）")]
        private Button _incrementButton;

        [SerializeField, Tooltip("减少按钮（点击按步进值 -）")]
        private Button _decrementButton;

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
            if (_incrementButton != null)
                _incrementButton.onClick.RemoveAllListeners();
            if (_decrementButton != null)
                _decrementButton.onClick.RemoveAllListeners();
            if (_slider != null)
                _slider.onValueChanged.RemoveAllListeners();

            // 重置子类文本
            if (_valueDisplayText != null) _valueDisplayText.text = "";
            if (_minValueText != null) _minValueText.text = "";
            if (_maxValueText != null) _maxValueText.text = "";

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

            // 初始化上下限文本
            ApplyRangeTexts();

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

                RefreshValueText(convertedVal);
            });

            // 数据 → UI（带验证状态）
            binding.OnUIDataChanged += (value, state) =>
            {
                float sliderVal = Convert.ToSingle(value);
                _slider.SetValueWithoutNotify(sliderVal);

                RefreshValueText(value);
                ApplyValueTextColor(state);

                base.UpdateValueDisplay(value, state);
            };

            // 验证状态变更
            binding.OnValidationStateChanged += OnValidationStateChanged;

            // 首次同步
            var initialValue = accessor.GetValue();
            if (initialValue != null)
                _slider.SetValueWithoutNotify(Convert.ToSingle(initialValue));

            RefreshValueText(initialValue);
            UpdateValueDisplay(initialValue ?? 0f, ValidationState.Normal);
        }

        // ============ 值显示 ============

        protected override void UpdateValueDisplay(object value, ValidationState state)
        {
            ApplyValueTextColor(state);
            base.UpdateValueDisplay(value, state);
        }

        private void RefreshValueText(object value)
        {
            if (_valueDisplayText == null || CurrentAccessor == null) return;
            _valueDisplayText.text = CurrentAccessor.FormatDisplay(value);
        }

        private void ApplyValueTextColor(ValidationState state)
        {
            if (_valueDisplayText == null) return;

            _valueDisplayText.color = state == ValidationState.Invalid
                ? Color.red
                : state == ValidationState.Warning
                    ? new Color(1f, 0.92f, 0.016f)
                    : Color.white;
        }

        private void ApplyRangeTexts()
        {
            if (_minValueText != null)
                _minValueText.text = CurrentAccessor?.FormatDisplay(_slider.minValue) ?? _slider.minValue.ToString();
            if (_maxValueText != null)
                _maxValueText.text = CurrentAccessor?.FormatDisplay(_slider.maxValue) ?? _slider.maxValue.ToString();
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

            ApplyValueTextColor(state);
        }

        private static object ConvertValueForAccessor(float val, Type targetType)
        {
            if (targetType == typeof(int)) return (int)val;
            if (targetType == typeof(double)) return (double)val;
            return val;
        }
    }
}
