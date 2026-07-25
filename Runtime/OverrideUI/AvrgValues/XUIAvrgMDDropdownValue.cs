using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// Dropdown 值组件 — 处理 string / int / enum 类型的下拉选择交互
    /// 预设选项来源：元数据的 PresetOptions 或 enum 类型的枚举名
    /// </summary>
    [AddComponentMenu("Xeric Library/UI Action Vessel/Values/XUIAvrgMDDropdownValue")]
    public class XUIAvrgMDDropdownValue : XUIAvrgMDValue
    {
        [SerializeField]
        [Tooltip("关联的 Dropdown 组件（未设置则自动查找）")]
#if dUI_TextMeshPro
        private TMPro.TMP_Dropdown _dropdown;
#else
        private Dropdown _dropdown;
#endif

        private List<string> _options;

        private void Awake()
        {
#if dUI_TextMeshPro
            if (_dropdown == null) _dropdown = GetComponent<TMPro.TMP_Dropdown>();
#else
            if (_dropdown == null) _dropdown = GetComponent<Dropdown>();
#endif
        }

        // ============ 对象池生命周期 ============

        public override void OnPoolGet(AvrgMemberMetaData metaData)
        {
            base.OnPoolGet(metaData);
            _options = BuildOptions(metaData?.ValueType);
        }

        public override void OnPoolRelease()
        {
            if (_dropdown != null)
                _dropdown.onValueChanged.RemoveAllListeners();
            _options = null;
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

            if (binding == null || accessor == null || _dropdown == null) return;

            // 填充选项
            _dropdown.ClearOptions();
            _dropdown.AddOptions(_options);

            // UI → 数据
            _dropdown.onValueChanged.AddListener(index =>
            {
                if (index >= 0 && index < _options.Count)
                {
                    object convertedVal = ConvertOptionToValue(_options[index], accessor.ValueType);
                    binding.WriteFromUI(convertedVal);
                    scheduler?.MarkUIDirty(binding);
                }
            });

            // 数据 → UI（带验证状态）
            binding.OnUIDataChanged += (value, state) =>
            {
                int idx = FindOptionIndex(value);
                if (idx >= 0)
                    _dropdown.SetValueWithoutNotify(idx);

                base.UpdateValueDisplay(value, state);
            };

            // 验证状态变更
            binding.OnValidationStateChanged += OnValidationStateChanged;

            // 首次同步
            var initialValue = accessor.GetValue();
            int initialIdx = FindOptionIndex(initialValue);
            if (initialIdx >= 0)
                _dropdown.SetValueWithoutNotify(initialIdx);
            UpdateValueDisplay(initialValue, ValidationState.Normal);
        }

        private List<string> BuildOptions(Type valueType)
        {
            if (BoundMetaData?.PresetOptions != null && BoundMetaData.PresetOptions.Count > 0)
                return BoundMetaData.PresetOptions.ToList();

            if (valueType != null && valueType.IsEnum)
                return Enum.GetNames(valueType).ToList();

            return new List<string>();
        }

        private int FindOptionIndex(object value)
        {
            if (value == null || _options == null || _options.Count == 0) return -1;

            string valueStr = value.ToString();
            for (int i = 0; i < _options.Count; i++)
            {
                if (_options[i] == valueStr) return i;
            }
            return -1;
        }

        private static object ConvertOptionToValue(string option, Type targetType)
        {
            if (targetType == typeof(string)) return option;
            if (targetType == typeof(int) && int.TryParse(option, out int intVal)) return intVal;
            if (targetType != null && targetType.IsEnum)
                return Enum.Parse(targetType, option);
            return option;
        }
    }
}
