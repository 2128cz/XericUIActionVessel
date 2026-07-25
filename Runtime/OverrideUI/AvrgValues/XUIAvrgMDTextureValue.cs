using UnityEngine;
using UnityEngine.UI;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// Texture 值组件 — 通过 RawImage 显示纹理
    /// </summary>
    [AddComponentMenu("Xeric Library/UI Action Vessel/Values/XUIAvrgMDTextureValue")]
    public class XUIAvrgMDTextureValue : XUIAvrgMDValue
    {
        [SerializeField, Tooltip("关联的 RawImage 组件（未设置则自动查找）")]
        private RawImage _rawImage;

#if dUI_TextMeshPro
        [SerializeField, Tooltip("纹理名称标签（可选）")]
        private TMPro.TMP_Text _nameLabel;
#endif

        private void Awake()
        {
            if (_rawImage == null) _rawImage = GetComponent<RawImage>();
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
            // 值显示：刷新 RawImage.texture
            if (_rawImage != null && value is Texture tex)
            {
                _rawImage.texture = tex;
#if dUI_TextMeshPro
                if (_nameLabel != null)
                    _nameLabel.text = tex?.name ?? "";
#endif
            }

            base.UpdateValueDisplay(value, state);
        }
    }
}
