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
        [SerializeField]
        [Tooltip("关联的 Toggle 组件（未设置则自动查找）")]
        private Toggle _toggle;

        private void Awake()
        {
            if (_toggle == null) _toggle = GetComponent<Toggle>();
        }

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

            // 数据 → UI
            binding.OnUIDataChanged += value =>
            {
                _toggle.SetIsOnWithoutNotify(value is bool b && b);
            };

            // 首次同步
            var initialValue = accessor.GetValue();
            _toggle.SetIsOnWithoutNotify(initialValue is bool bv && bv);
        }
    }
}
