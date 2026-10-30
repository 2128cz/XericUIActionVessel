using System;
using UnityEngine;
using UnityEngine.UI;
using XericUI.Core;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// InputField 值组件 — 处理 string / float / int / double 类型的文本输入交互
    /// </summary>
    [AddComponentMenu(XericRegistrationPaths.UI + "/Values/XUIAvrgMDInputFieldValue")]
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

        private const float DefaultStep = 1f;

        private void Awake()
        {
#if dUI_TextMeshPro
            if (_inputField == null) _inputField = GetComponent<TMPro.TMP_InputField>();
#else
            if (_inputField == null) _inputField = GetComponent<InputField>();
#endif
        }

        // ============ 对象池生命周期 ============

        public override void OnPoolRelease()
        {
            if (_incrementButton != null)
                _incrementButton.onClick.RemoveAllListeners();
            if (_decrementButton != null)
                _decrementButton.onClick.RemoveAllListeners();
            if (_inputField != null)
                _inputField.onEndEdit.RemoveAllListeners();

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

            if (binding == null || accessor == null || _inputField == null) return;

            // 根据类型设置输入模式
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

            // 数据 → UI（带验证状态）
            binding.OnUIDataChanged += (value, state) =>
            {
                _inputField.SetTextWithoutNotify(accessor.FormatDisplay(value));
                base.UpdateValueDisplay(value, state);
            };

            // 验证状态变更
            binding.OnValidationStateChanged += OnValidationStateChanged;

            // 首次同步
            var initialValue = accessor.GetValue();
            _inputField.SetTextWithoutNotify(accessor.FormatDisplay(initialValue));
            UpdateValueDisplay(initialValue, ValidationState.Normal);
        }

        // ============ 步进 ============

        private void OnIncrementClicked()
        {
            if (CurrentAccessor?.IsReadOnly == true || _inputField == null) return;
            if (!float.TryParse(_inputField.text, out float current)) return;
            float newVal = current + DefaultStep;
            _inputField.text = CurrentAccessor.FormatDisplay(
                ConvertValueForAccessor(newVal, CurrentAccessor.ValueType));
        }

        private void OnDecrementClicked()
        {
            if (CurrentAccessor?.IsReadOnly == true || _inputField == null) return;
            if (!float.TryParse(_inputField.text, out float current)) return;
            float newVal = current - DefaultStep;
            _inputField.text = CurrentAccessor.FormatDisplay(
                ConvertValueForAccessor(newVal, CurrentAccessor.ValueType));
        }

        // ============ 验证状态回调 ============

        protected override void OnValidationStateChanged(ValidationState state)
        {
            base.OnValidationStateChanged(state);

            // InputField 文字颜色随验证状态变化（通过 Text 组件）
            if (_inputField != null)
            {
                var textComponent = _inputField.textComponent;
                if (textComponent != null)
                {
                    textComponent.color = state == ValidationState.Invalid
                        ? Color.red
                        : state == ValidationState.Warning
                            ? new Color(1f, 0.92f, 0.016f)
                            : Color.white;
                }
            }
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
