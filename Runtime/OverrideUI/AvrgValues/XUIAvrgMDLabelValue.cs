using UnityEngine;
using UnityEngine.UI;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// Label 只读值组件 — 纯文本展示，无交互控件
    /// </summary>
    [AddComponentMenu("Xeric Library/UI Action Vessel/Values/XUIAvrgMDLabelValue")]
    public class XUIAvrgMDLabelValue : XUIAvrgMDValue
    {
#if dUI_TextMeshPro
        [SerializeField, Tooltip("关联的 TMP_Text 组件（标签文本 + 值文本共用一个 Text 时使用）")]
        private TMPro.TMP_Text _tmpText;
#endif

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
    }
}
