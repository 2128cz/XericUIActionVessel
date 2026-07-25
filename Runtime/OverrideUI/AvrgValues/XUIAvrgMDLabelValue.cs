using UnityEngine;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 标签值组件 — 处理只读文本的纯展示
    /// 适用于 Label / Context / Unit / ReadOnly 等展示型 Tag
    /// </summary>
    [AddComponentMenu("Xeric Library/UI Action Vessel/Values/XUIAvrgMDLabelValue")]
    public class XUIAvrgMDLabelValue : XUIAvrgMDValue
    {
        [SerializeField]
        [Tooltip("关联的 Text 组件（未设置则自动查找）")]
        private UnityEngine.UI.Text _uiText;

#if dUI_TextMeshPro
        [SerializeField]
        [Tooltip("关联的 TMP_Text 组件（未设置则自动查找）")]
        private TMPro.TMP_Text _tmpText;
#endif

        private void Awake()
        {
            if (_uiText == null) _uiText = GetComponent<UnityEngine.UI.Text>();
#if dUI_TextMeshPro
            if (_tmpText == null) _tmpText = GetComponent<TMPro.TMP_Text>();
#endif
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
                SetText(value);
            };

            // 首次同步
            var initialValue = accessor.GetValue();
            SetText(initialValue);
        }

        private void SetText(object value)
        {
            string displayStr = value?.ToString() ?? "";

#if dUI_TextMeshPro
            if (_tmpText != null)
            {
                _tmpText.text = displayStr;
                return;
            }
#endif

            if (_uiText != null)
                _uiText.text = displayStr;
        }
    }
}
