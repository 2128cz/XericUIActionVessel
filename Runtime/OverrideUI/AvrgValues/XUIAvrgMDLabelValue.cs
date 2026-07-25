using UnityEngine;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// Label 只读值组件 — 纯文本展示，无交互控件
    /// 拥有自己的 _valueDisplayText 用于显示值
    /// </summary>
    [AddComponentMenu("Xeric Library/UI Action Vessel/Values/XUIAvrgMDLabelValue")]
    public class XUIAvrgMDLabelValue : XUIAvrgMDValue
    {
        [SerializeField, Tooltip("值显示文本 — 显示当前字段的值")]
#if dUI_TextMeshPro
        private TMPro.TMP_Text _valueDisplayText;
#else
        private UnityEngine.UI.Text _valueDisplayText;
#endif

        public override void OnPoolRelease()
        {
            if (_valueDisplayText != null) _valueDisplayText.text = "";
            base.OnPoolRelease();
        }

        public override void BindToAccessor(
            IAvrgValueAccessor accessor,
            AvrgMvvmBinding binding,
            AvrgMvvmScheduler scheduler)
        {
            CurrentAccessor = accessor;
            CurrentBinding = binding;

            if (binding == null || accessor == null) return;

            binding.OnUIDataChanged += (value, state) =>
            {
                UpdateValueDisplay(value, state);
            };
            binding.OnValidationStateChanged += OnValidationStateChanged;

            var initialValue = accessor.GetValue();
            UpdateValueDisplay(initialValue, ValidationState.Normal);
        }

        protected override void UpdateValueDisplay(object value, ValidationState state)
        {
            if (_valueDisplayText != null)
            {
                _valueDisplayText.text = CurrentAccessor?.FormatDisplay(value) ?? value?.ToString() ?? "";

                _valueDisplayText.color = state == ValidationState.Invalid
                    ? Color.red
                    : state == ValidationState.Warning
                        ? new Color(1f, 0.92f, 0.016f)
                        : Color.white;
            }

            base.UpdateValueDisplay(value, state);
        }

        protected override void OnValidationStateChanged(ValidationState state)
        {
            base.OnValidationStateChanged(state);

            if (_valueDisplayText != null)
            {
                _valueDisplayText.color = state == ValidationState.Invalid
                    ? Color.red
                    : state == ValidationState.Warning
                        ? new Color(1f, 0.92f, 0.016f)
                        : Color.white;
            }
        }
    }
}
