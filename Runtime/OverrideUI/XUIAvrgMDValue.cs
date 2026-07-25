using System.Collections.Generic;
using UnityEngine;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// MDVM 值组件基类 — 预制体上的标记组件
    /// 
    /// 职责：
    /// 1. 提供 EntryTag/TagIndex 供 XuiFieldTempEntry 匹配
    /// 2. 持有绑定的元数据引用
    /// 3. 通过 virtual BindToAccessor() 让子类自行管理 UI 交互事件和显示更新
    /// 4. 初始化时广播元数据到所有子节点上的 IXUIAvrgMDValueStyle
    /// 5. 定义绑定槽（BindingSlots），供拆分属性子绑定匹配
    /// </summary>
    [AddComponentMenu("Xeric Library/UI Action Vessel/XUIAvrgMDValue")]
    public class XUIAvrgMDValue : MonoBehaviour
    {
        /// <summary>
        /// 绑定槽定义 — 用于拆分属性（Split）的子绑定匹配
        /// propertyName 匹配 XuiavrgSplit 的 PropertyPath
        /// tag 在预制体内查找对应子节点的 XUIAvrgMDValue
        /// </summary>
        [System.Serializable]
        public class AvrgBindingSlot
        {
            [Tooltip("绑定的子属性名（匹配 XuiavrgSplit 的 PropertyPath，如 \"x\"、\"y\"）")]
            public string propertyName;

            [Tooltip("子属性在预制体上的 Tag 标记，用于查找子节点 XUIAvrgMDValue")]
            public XuiavrgFieldTag tag = XuiavrgFieldTag.Context;
        }

        [SerializeField]
        [Tooltip("功能标记，用于与 Entry 的 Tag 匹配")]
        private XuiavrgFieldTag _entryTag = XuiavrgFieldTag.None;

        [SerializeField]
        [Tooltip("同 Tag 多实例时区分（0 表示不限制）")]
        private int _tagIndex;

        [SerializeField]
        [Tooltip("拆分属性子绑定槽列表 — 用于 XuiavrgSplit 的子属性匹配")]
        private List<AvrgBindingSlot> _bindingSlots = new List<AvrgBindingSlot>();

        /// <summary>绑定的元数据引用</summary>
        public AvrgMemberMetaData BoundMetaData { get; protected set; }

        /// <summary>当前关联的值访问器</summary>
        public IAvrgValueAccessor CurrentAccessor { get; protected set; }

        /// <summary>当前关联的 MVVM 绑定</summary>
        public AvrgMvvmBinding CurrentBinding { get; protected set; }

        public XuiavrgFieldTag EntryTag => _entryTag;
        public int TagIndex => _tagIndex;

        /// <summary>只读访问绑定槽列表</summary>
        public IReadOnlyList<AvrgBindingSlot> AvailableBindingSlots => _bindingSlots;

        /// <summary>
        /// 由 XuiFieldTempEntry 调用，将值访问器和 MVVM 绑定连接到此 UI 组件
        /// 
        /// 基类实现提供一个默认的通用绑定逻辑（适用于 Label/Context/Unit 等简单展示型 Tag）。
        /// 子类（如 SliderValue, DropdownValue）应覆写此方法以处理其专属 UI 交互。
        /// </summary>
        /// <param name="accessor">值访问器 — 提供统一的数据读写</param>
        /// <param name="binding">MVVM 绑定 — 提供脏标记和 UI→数据通信通道</param>
        /// <param name="scheduler">MVVM 调度器 — 可空，用于标记脏状态</param>
        public virtual void BindToAccessor(
            IAvrgValueAccessor accessor,
            AvrgMvvmBinding binding,
            AvrgMvvmScheduler scheduler)
        {
            CurrentAccessor = accessor;
            CurrentBinding = binding;

            if (binding == null || accessor == null) return;

            // 默认：数据 → UI 单向同步
            binding.OnUIDataChanged += value =>
            {
                UpdateDisplayValue(value);
            };

            // 首次同步
            var initialValue = accessor.GetValue();
            UpdateDisplayValue(initialValue);
        }

        /// <summary>
        /// 更新 UI 显示值（基类默认仅查找 Text/TMP_Text 设置文本）
        /// 子类可覆写以实现专属更新逻辑（如 Slider.value, Image.color 等）
        /// </summary>
        protected virtual void UpdateDisplayValue(object value)
        {
            if (value == null) return;

            // 尝试 Text
            var uiText = GetComponent<UnityEngine.UI.Text>();
            if (uiText != null)
            {
                uiText.text = CurrentAccessor?.FormatDisplay(value) ?? value.ToString();
                return;
            }

            // 尝试 TMP_Text
            var tmpText = GetComponent<TMPro.TMP_Text>();
            if (tmpText != null)
            {
                tmpText.text = CurrentAccessor?.FormatDisplay(value) ?? value.ToString();
            }
        }

        /// <summary>
        /// 由绑定系统调用，传递元数据并广播到 IXUIAvrgMDValueStyle 子组件
        /// </summary>
        public void InitializeWithMetaData(AvrgMemberMetaData metaData)
        {
            BoundMetaData = metaData;

            if (metaData == null) return;

            // 查找子节点上的所有样式扩展组件并广播元数据
            var styles = GetComponentsInChildren<IXUIAvrgMDValueStyle>(true);
            foreach (var style in styles)
            {
                // 跳过自身（XUIAvrgMDValue 不是 IXUIAvrgMDValueStyle）
                if (style is MonoBehaviour mb && mb.gameObject == gameObject)
                    continue;

                style.ApplyMetaData(metaData);
            }
        }
    }
}
