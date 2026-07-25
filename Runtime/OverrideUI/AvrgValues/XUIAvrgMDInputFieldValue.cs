using System;
using UnityEngine;
using UnityEngine.UI;
using XericLibrary.Runtime.SuperStyleSheet;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// InputField 值组件 — 处理 string / float / int 类型的文本输入交互
    /// </summary>
    [AddComponentMenu("Xeric Library/UI Action Vessel/Values/XUIAvrgMDInputFieldValue")]
    public class XUIAvrgMDInputFieldValue : XUIAvrgMDValue
    {
        [SerializeField]
        [Tooltip("关联的 InputField 组件")]
#if dUI_TextMeshPro
        private TMPro.TMP_InputField _inputField;
#else
        private InputField _inputField;
#endif

        [Header("InputField 专属插槽")]
        [SerializeField, Tooltip("增加按钮（点击按步进值 +）")]
        private Button _incrementButton;

        [SerializeField, Tooltip("减少按钮（点击按步进值 -）")]
        private Button _decrementButton;

        // ============ 样式表参数 ============
        private readonly StyleMember _styleStepValue = new() { StylePath = "xuiavrg/input/stepValue" };

        // ============ 生命周期 ============

        private void Awake()
        {
#if dUI_TextMeshPro
            if (_inputField == null) _inputField = GetComponent<TMPro.TMP_InputField>();
#else
            if (_inputField == null) _inputField = GetComponent<InputField>();
#endif
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _styleStepValue.Register();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            _styleStepValue.Unregister();
        }

        // ============ 核心绑定 ============

        public override void BindToAccessor(
            IAvrgValueAccessor accessor,
            AvrgMvvmBinding binding,
            AvrgMvvmScheduler scheduler)
        {
            CurrentAccessor = accessor;
            CurrentBinding = binding;

            if (binding == null || accessor == null || _inputField == null) return;

#if dUI_TextMeshPro
            if (accessor.ValueType == typeof(float) || accessor.ValueType == typeof(int)
                || accessor.ValueType == typeof(double))
            {
                _inputField.contentType = TMPro.TMP_InputField.ContentType.DecimalNumber;
            }
#else
            if (accessor.ValueType == typeof(float) || accessor.ValueType == typeof(int)
                || accessor.ValueType == typeof(double))
            {
                _inputField.contentType = InputField.ContentType.DecimalNumber;
            }
#endif

            // 增减按钮
            if (_incrementButton != null)
                _incrementButton.onClick.AddListener(OnIncrementClicked);
            if (_decrementButton != null)
                _decrementButton.onClick.AddListener(OnDecrementClicked);

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

            RefreshCommonNodes();
        }

        // ============ 步进 ============

        private float GetStepValue()
        {
            var val = GetStyleValue(_styleStepValue);
            return val?.GetValueAsFloat() ?? 1f;
        }

        private void OnIncrementClicked()
        {
            if (CurrentAccessor?.IsReadOnly == true || _inputField == null) return;
            if (!float.TryParse(_inputField.text, out float current)) return;
            float step = GetStepValue();
            float newVal = current + step;
            _inputField.text = CurrentAccessor.FormatDisplay(
                ConvertValueForAccessor(newVal, CurrentAccessor.ValueType));
        }

        private void OnDecrementClicked()
        {
            if (CurrentAccessor?.IsReadOnly == true || _inputField == null) return;
            if (!float.TryParse(_inputField.text, out float current)) return;
            float step = GetStepValue();
            float newVal = current - step;
            _inputField.text = CurrentAccessor.FormatDisplay(
                ConvertValueForAccessor(newVal, CurrentAccessor.ValueType));
        }

        private static object ConvertValueForAccessor(float val, Type targetType)
        {
            if (targetType == typeof(int)) return (int)val;
            if (targetType == typeof(double)) return (double)val;
            return val;
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
