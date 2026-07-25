using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

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
        private Dropdown _dropdown;

        private void Awake()
        {
            if (_dropdown == null) _dropdown = GetComponent<Dropdown>();
        }

        public override void BindToAccessor(
            IAvrgValueAccessor accessor,
            AvrgMvvmBinding binding,
            AvrgMvvmScheduler scheduler)
        {
            CurrentAccessor = accessor;
            CurrentBinding = binding;

            if (binding == null || accessor == null || _dropdown == null) return;

            // 构建选项列表
            var options = BuildOptions(accessor.ValueType);
            _dropdown.ClearOptions();
            _dropdown.AddOptions(options);

            // UI → 数据
            _dropdown.onValueChanged.AddListener(index =>
            {
                if (index >= 0 && index < options.Count)
                {
                    object convertedVal = ConvertOptionToValue(options[index], accessor.ValueType);
                    binding.WriteFromUI(convertedVal);
                    scheduler?.MarkUIDirty(binding);
                }
            });

            // 数据 → UI
            binding.OnUIDataChanged += value =>
            {
                int idx = FindOptionIndex(value, options);
                if (idx >= 0)
                    _dropdown.SetValueWithoutNotify(idx);
            };

            // 首次同步
            var initialValue = accessor.GetValue();
            int initialIdx = FindOptionIndex(initialValue, options);
            if (initialIdx >= 0)
                _dropdown.SetValueWithoutNotify(initialIdx);
        }

        private List<string> BuildOptions(System.Type valueType)
        {
            // 1. 从元数据 PresetOptions 获取
            if (BoundMetaData?.PresetOptions != null && BoundMetaData.PresetOptions.Count > 0)
                return BoundMetaData.PresetOptions.ToList();

            // 2. enum 类型：自动提取枚举名
            if (valueType != null && valueType.IsEnum)
                return System.Enum.GetNames(valueType).ToList();

            // 3. 回退
            return new List<string>();
        }

        private int FindOptionIndex(object value, List<string> options)
        {
            if (value == null || options.Count == 0) return -1;

            string valueStr = value.ToString();
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i] == valueStr) return i;
            }
            return -1;
        }

        private static object ConvertOptionToValue(string option, System.Type targetType)
        {
            if (targetType == typeof(string)) return option;
            if (targetType == typeof(int) && int.TryParse(option, out int intVal)) return intVal;
            if (targetType != null && targetType.IsEnum)
                return System.Enum.Parse(targetType, option);
            return option;
        }
    }
}
