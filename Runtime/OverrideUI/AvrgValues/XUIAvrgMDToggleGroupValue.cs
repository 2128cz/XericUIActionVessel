using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// ToggleGroup 值组件 — 处理 string / int / enum 类型的单选组交互
    /// 适用于标记了 [XuiavrgFieldToggleGroupValue] 的字段
    /// </summary>
    [AddComponentMenu("Xeric Library/UI Action Vessel/Values/XUIAvrgMDToggleGroupValue")]
    public class XUIAvrgMDToggleGroupValue : XUIAvrgMDValue
    {
        [SerializeField]
        [Tooltip("关联的 ToggleGroup 组件（未设置则自动查找）")]
        private ToggleGroup _toggleGroup;

        private List<Toggle> _toggles = new List<Toggle>();
        private List<string> _optionLabels = new List<string>();

        private void Awake()
        {
            if (_toggleGroup == null) _toggleGroup = GetComponent<ToggleGroup>();
            if (_toggleGroup != null)
            {
                _toggles = _toggleGroup.GetComponentsInChildren<Toggle>(true).ToList();
            }
        }

        public override void BindToAccessor(
            IAvrgValueAccessor accessor,
            AvrgMvvmBinding binding,
            AvrgMvvmScheduler scheduler)
        {
            CurrentAccessor = accessor;
            CurrentBinding = binding;

            if (binding == null || accessor == null || _toggleGroup == null) return;

            // 构建选项
            BuildOptions(accessor.ValueType);

            // 为每个 Toggle 注册事件
            for (int i = 0; i < _toggles.Count && i < _optionLabels.Count; i++)
            {
                int idx = i;
                _toggles[i].onValueChanged.AddListener(isOn =>
                {
                    if (isOn)
                    {
                        object convertedVal = ConvertOptionToValue(_optionLabels[idx], accessor.ValueType);
                        binding.WriteFromUI(convertedVal);
                        scheduler?.MarkUIDirty(binding);
                    }
                });
            }

            // 数据 → UI
            binding.OnUIDataChanged += value =>
            {
                string valueStr = value?.ToString();
                for (int i = 0; i < _toggles.Count && i < _optionLabels.Count; i++)
                {
                    if (_optionLabels[i] == valueStr)
                    {
                        _toggles[i].SetIsOnWithoutNotify(true);
                        return;
                    }
                }
            };

            // 首次同步
            var initialValue = accessor.GetValue();
            string initStr = initialValue?.ToString();
            for (int i = 0; i < _toggles.Count && i < _optionLabels.Count; i++)
            {
                if (_optionLabels[i] == initStr)
                {
                    _toggles[i].SetIsOnWithoutNotify(true);
                    break;
                }
            }
        }

        private void BuildOptions(System.Type valueType)
        {
            _optionLabels.Clear();

            // 1. 从元数据 PresetOptions
            if (BoundMetaData?.PresetOptions != null && BoundMetaData.PresetOptions.Count > 0)
            {
                _optionLabels.AddRange(BoundMetaData.PresetOptions);
                return;
            }

            // 2. enum 类型
            if (valueType != null && valueType.IsEnum)
            {
                _optionLabels.AddRange(System.Enum.GetNames(valueType));
                return;
            }
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
