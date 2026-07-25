using UnityEngine;
using UnityEngine.UI;
using XericLibrary.Runtime.SuperStyleSheet;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// Toggle 值组件 — 处理 bool 类型的开关交互
    /// 支持样式表控制开关颜色
    /// </summary>
    [AddComponentMenu("Xeric Library/UI Action Vessel/Values/XUIAvrgMDToggleValue")]
    public class XUIAvrgMDToggleValue : XUIAvrgMDValue
    {
        [SerializeField]
        [Tooltip("关联的 Toggle 组件（未设置则自动查找）")]
        private Toggle _toggle;

        // ============ 样式表参数 ============
        private readonly StyleMember _styleOnColor  = new() { StylePath = "xuiavrg/toggle/onColor" };
        private readonly StyleMember _styleOffColor = new() { StylePath = "xuiavrg/toggle/offColor" };

        // ============ 生命周期 ============

        private void Awake()
        {
            if (_toggle == null) _toggle = GetComponent<Toggle>();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _styleOnColor.Register();
            _styleOffColor.Register();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            _styleOnColor.Unregister();
            _styleOffColor.Unregister();
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
                ApplyToggleColor(val);
            });

            // 数据 → UI
            binding.OnUIDataChanged += value =>
            {
                bool isOn = value is bool b && b;
                _toggle.SetIsOnWithoutNotify(isOn);
                ApplyToggleColor(isOn);
            };

            // 首次同步
            var initialValue = accessor.GetValue();
            bool initialOn = initialValue is bool bv && bv;
            _toggle.SetIsOnWithoutNotify(initialOn);
            ApplyToggleColor(initialOn);

            RefreshCommonNodes();
        }

        // ============ 样式颜色 ============

        private void ApplyToggleColor(bool isOn)
        {
            var target = _toggle.targetGraphic;
            if (target == null) return;

            var colorVal = isOn ? GetStyleValue(_styleOnColor) : GetStyleValue(_styleOffColor);
            if (colorVal != null)
                target.color = colorVal;
        }
    }
}
