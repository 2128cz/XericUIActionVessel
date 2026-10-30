using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using XericUI.Core;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// ToggleGroup 值组件 — 处理 string / int / enum 类型的单选组交互
    /// 适用于标记了 [XuiavrgFieldToggleGroupValue] 的字段
    /// 
    /// 选项动态创建：OnPoolGet 时根据元数据 PresetOptions 数量，
    /// 从 _toggleItemPrefab 实例化 Toggle 子对象。
    /// </summary>
    [AddComponentMenu(XericRegistrationPaths.UI + "/Values/XUIAvrgMDToggleGroupValue")]
    public class XUIAvrgMDToggleGroupValue : XUIAvrgMDValue
    {
        [SerializeField, Tooltip("关联的 ToggleGroup 组件（未设置则自动查找）")]
        private ToggleGroup _toggleGroup;

        [Header("选项动态创建")]
        [SerializeField, Tooltip("Toggle 项预制体 — 在 OnPoolGet 时按预设选项数量实例化")]
        private Transform _toggleItemPrefab;

        [SerializeField, Tooltip("选项容器 Transform — 动态创建的 Toggle 挂载到此节点下")]
        private Transform _optionsContainer;

        private List<Toggle> _toggles = new List<Toggle>();
        private List<string> _optionLabels = new List<string>();

        private void Awake()
        {
            if (_toggleGroup == null) _toggleGroup = GetComponent<ToggleGroup>();
        }

        // ============ 对象池生命周期 ============

        public override void OnPoolGet(AvrgMemberMetaData metaData)
        {
            base.OnPoolGet(metaData);

            // 构建选项列表
            BuildOptions(metaData?.ValueType);

            // 动态创建 Toggle 项
            CreateToggleItems();
        }

        public override void OnPoolRelease()
        {
            // 清理动态创建的 Toggle
            foreach (var toggle in _toggles)
            {
                if (toggle != null)
                    toggle.onValueChanged.RemoveAllListeners();
            }
            DestroyToggleItems();

            _toggles.Clear();
            _optionLabels.Clear();

            base.OnPoolRelease();
        }

        // ============ 动态 Toggle 创建 ============

        /// <summary>
        /// 根据 _optionLabels 数量，从 _toggleItemPrefab 实例化 Toggle 子对象
        /// </summary>
        private void CreateToggleItems()
        {
            var parent = _optionsContainer != null ? _optionsContainer : transform;
            if (_toggleItemPrefab == null || _optionLabels.Count == 0) return;

            for (int i = 0; i < _optionLabels.Count; i++)
            {
                var item = Instantiate(_toggleItemPrefab, parent);
                item.name = $"Toggle_{i}_{_optionLabels[i]}";
                item.gameObject.SetActive(true);

                var toggle = item.GetComponent<Toggle>();
                if (toggle != null)
                {
                    toggle.group = _toggleGroup;
                    _toggles.Add(toggle);

                    // 设置标签文本
                    var label = item.GetComponentInChildren<Text>(true);
                    if (label != null)
                        label.text = _optionLabels[i];
                }
            }
        }

        /// <summary>
        /// 销毁动态创建的所有 Toggle 子对象
        /// </summary>
        private void DestroyToggleItems()
        {
            var parent = _optionsContainer != null ? _optionsContainer : transform;

            foreach (var toggle in _toggles)
            {
                if (toggle != null)
                    Destroy(toggle.gameObject);
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

        // ============ 核心绑定 ============

        public override void BindToAccessor(
            IAvrgValueAccessor accessor,
            AvrgMvvmBinding binding,
            AvrgMvvmScheduler scheduler)
        {
            CurrentAccessor = accessor;
            CurrentBinding = binding;

            if (binding == null || accessor == null || _toggleGroup == null || _toggles.Count == 0) return;

            // 只读模式下禁用所有 Toggle
            if (accessor.IsReadOnly)
            {
                foreach (var t in _toggles)
                    t.interactable = false;
            }

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

            // 数据 → UI（带验证状态）
            binding.OnUIDataChanged += (value, state) =>
            {
                string valueStr = value?.ToString();
                for (int i = 0; i < _toggles.Count && i < _optionLabels.Count; i++)
                {
                    if (_optionLabels[i] == valueStr)
                    {
                        _toggles[i].SetIsOnWithoutNotify(true);
                        break;
                    }
                }

                base.UpdateValueDisplay(value, state);
            };

            // 验证状态变更
            binding.OnValidationStateChanged += OnValidationStateChanged;

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
