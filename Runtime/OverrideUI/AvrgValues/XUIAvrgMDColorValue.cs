using UnityEngine;
using UnityEngine.UI;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 颜色值组件 — 处理 Color 类型的显示
    /// 通过 Image.color 展示当前颜色值
    /// 颜色修改可通过 RGB 子节点 InputField 或额外的颜色选择逻辑完成
    /// </summary>
    [AddComponentMenu("Xeric Library/UI Action Vessel/Values/XUIAvrgMDColorValue")]
    public class XUIAvrgMDColorValue : XUIAvrgMDValue
    {
        [SerializeField]
        [Tooltip("用于展示颜色的 Image 组件（未设置则自动查找）")]
        private Image _colorDisplay;

        private void Awake()
        {
            if (_colorDisplay == null) _colorDisplay = GetComponent<Image>();
        }

        public override void BindToAccessor(
            IAvrgValueAccessor accessor,
            AvrgMvvmBinding binding,
            AvrgMvvmScheduler scheduler)
        {
            CurrentAccessor = accessor;
            CurrentBinding = binding;

            if (binding == null || accessor == null) return;

            // 数据 → UI
            binding.OnUIDataChanged += value =>
            {
                if (value is Color color && _colorDisplay != null)
                {
                    _colorDisplay.color = color;
                }
            };

            // 首次同步
            var initialValue = accessor.GetValue();
            if (initialValue is Color initColor && _colorDisplay != null)
            {
                _colorDisplay.color = initColor;
            }
        }

        protected override void UpdateDisplayValue(object value)
        {
            if (value is Color color && _colorDisplay != null)
            {
                _colorDisplay.color = color;
            }
        }
    }
}
