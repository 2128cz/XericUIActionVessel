using UnityEngine;
using UnityEngine.UI;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// Color 值组件 — 通过 Image 显示颜色，可选点击弹出颜色选择器
    /// </summary>
    [AddComponentMenu("Xeric Library/UI Action Vessel/Values/XUIAvrgMDColorValue")]
    public class XUIAvrgMDColorValue : XUIAvrgMDValue
    {
        [SerializeField, Tooltip("关联的 Image 组件（未设置则自动查找）")]
        private Image _colorImage;

        private void Awake()
        {
            if (_colorImage == null) _colorImage = GetComponent<Image>();
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
            // 值显示：刷新 Image.color
            if (_colorImage != null && value is Color c)
                _colorImage.color = c;

            base.UpdateValueDisplay(value, state);
        }
    }
}
