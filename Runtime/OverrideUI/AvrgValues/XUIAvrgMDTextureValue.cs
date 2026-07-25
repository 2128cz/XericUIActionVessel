using UnityEngine;
using UnityEngine.UI;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 纹理值组件 — 处理 Texture 类型的预览显示
    /// 通过 RawImage.texture 展示当前纹理
    /// </summary>
    [AddComponentMenu("Xeric Library/UI Action Vessel/Values/XUIAvrgMDTextureValue")]
    public class XUIAvrgMDTextureValue : XUIAvrgMDValue
    {
        [SerializeField]
        [Tooltip("用于展示纹理的 RawImage 组件（未设置则自动查找）")]
        private RawImage _textureDisplay;

#if dUI_TextMeshPro
        [SerializeField]
        [Tooltip("纹理名称标签（可选，用于显示纹理资源名）")]
        private TMPro.TMP_Text _nameLabel;
#endif

        private void Awake()
        {
            if (_textureDisplay == null) _textureDisplay = GetComponent<RawImage>();
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
                if (value is Texture tex && _textureDisplay != null)
                {
                    _textureDisplay.texture = tex;
                }

#if dUI_TextMeshPro
                if (_nameLabel != null)
                {
                    _nameLabel.text = value is Texture t && t != null ? t.name : "None";
                }
#endif
            };

            // 首次同步
            var initialValue = accessor.GetValue();
            if (initialValue is Texture initTex && _textureDisplay != null)
            {
                _textureDisplay.texture = initTex;
            }

#if dUI_TextMeshPro
            if (_nameLabel != null)
            {
                _nameLabel.text = initialValue is Texture t && t != null ? t.name : "None";
            }
#endif
        }
    }
}
