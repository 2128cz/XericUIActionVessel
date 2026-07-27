using UnityEngine;
using UnityEngine.UI;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// Toggle 值组件 — 处理 bool 类型的开关交互
    /// </summary>
    [AddComponentMenu("Xeric Library/UI Action Vessel/Values/XUIAvrgMDToggleValue")]
    public class XUIAvrgMDToggleValue : XUIAvrgMDValue
    {
        [SerializeField, Tooltip("关联的 Toggle 组件（未设置则自动查找）")]
        private Toggle _toggle;

        private void Awake()
        {
            if (_toggle == null) _toggle = GetComponent<Toggle>();
        }

        // ============ 对象池生命周期 ============

        public override void OnPoolRelease()
        {
            if (_toggle != null)
                _toggle.onValueChanged.RemoveAllListeners();
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

            if (binding == null || accessor == null || _toggle == null) return;

            // 只读模式下禁用交互
            _toggle.interactable = !accessor.IsReadOnly;

            // UI → 数据
            _toggle.onValueChanged.AddListener(val =>
            {
                binding.WriteFromUI(val);
                scheduler?.MarkUIDirty(binding);
            });

            // 数据 → UI（带验证状态）
            binding.OnUIDataChanged += (value, state) =>
            {
                bool isOn = value is bool b && b;
                _toggle.SetIsOnWithoutNotify(isOn);
                ApplyToggleColor(isOn, state);

                base.UpdateValueDisplay(value, state);
            };

            // 验证状态变更
            binding.OnValidationStateChanged += OnValidationStateChanged;

            // 首次同步
            var initialValue = accessor.GetValue();
            bool initialOn = initialValue is bool bv && bv;
            _toggle.SetIsOnWithoutNotify(initialOn);
            ApplyToggleColor(initialOn, ValidationState.Normal);
            UpdateValueDisplay(initialValue, ValidationState.Normal);
        }

        // ============ 验证状态回调 ============

        protected override void OnValidationStateChanged(ValidationState state)
        {
            base.OnValidationStateChanged(state);

            // Switch 验证状态也影响 targetGraphic 颜色
            if (_toggle != null)
            {
                ApplyToggleColor(_toggle.isOn, state);
            }
        }

        private void ApplyToggleColor(bool isOn, ValidationState state)
        {
            var target = _toggle?.targetGraphic;
            if (target == null) return;

            if (state == ValidationState.Invalid)
                target.color = Color.red;
            else if (state == ValidationState.Warning)
                target.color = new Color(1f, 0.92f, 0.016f);
            else
                target.color = isOn ? Color.green : new Color(0.5f, 0.5f, 0.5f, 1f);
        }
    }
}
