using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// MDVM 值组件基类 — 预制体上的标记组件
    /// 
    /// 职责：
    /// 1. 提供 EntryTag/TagIndex 供 XuiFieldTempEntry 匹配
    /// 2. 持有绑定的元数据引用，管理验证状态
    /// 3. 通过 virtual BindToAccessor() 让子类自行管理 UI 交互事件
    /// 4. 对象池生命周期：OnPoolGet(meta) 初始化、OnPoolRelease() 重置
    /// 5. 定义绑定槽（BindingSlots），供拆分属性子绑定匹配
    /// 6. 通过 UpdateValueDisplay() 合成标签文本（名称 + 只读 + 警告标记）
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

        // ============ 匹配标记 ============

        [SerializeField]
        [Tooltip("功能标记，用于与 Entry 的 Tag 匹配")]
        private XuiavrgFieldTag _entryTag = XuiavrgFieldTag.None;

        [SerializeField]
        [Tooltip("同 Tag 多实例时区分（0 表示不限制）")]
        private int _tagIndex;

        [SerializeField]
        [Tooltip("拆分属性子绑定槽列表 — 用于 XuiavrgSplit 的子属性匹配")]
        private List<AvrgBindingSlot> _bindingSlots = new List<AvrgBindingSlot>();

        // ============ UI 文本插槽 ============

        [Header("UI 文本插槽")]
        [SerializeField, Tooltip("字段标签文本 — 显示 \"名称 (只读) ⚠警告\"，为 null 则不显示标签")]
#if dUI_TextMeshPro
        protected TMPro.TMP_Text _fieldLabelText;
#else
        protected UnityEngine.UI.Text _fieldLabelText;
#endif

        [SerializeField, Tooltip("值显示文本 — 可为 null（Slider/Color/Texture 子类不依赖文本）")]
#if dUI_TextMeshPro
        protected TMPro.TMP_Text _valueDisplayText;
#else
        protected UnityEngine.UI.Text _valueDisplayText;
#endif

        [SerializeField, Tooltip("单位文本 — 可为 null")]
#if dUI_TextMeshPro
        protected TMPro.TMP_Text _unitText;
#else
        protected UnityEngine.UI.Text _unitText;
#endif

        // ============ 公共属性 ============

        /// <summary>绑定的元数据引用</summary>
        public AvrgMemberMetaData BoundMetaData { get; protected set; }

        /// <summary>当前关联的值访问器</summary>
        public IAvrgValueAccessor CurrentAccessor { get; protected set; }

        /// <summary>当前关联的 MVVM 绑定</summary>
        public AvrgMvvmBinding CurrentBinding { get; protected set; }

        /// <summary>当前验证状态</summary>
        public ValidationState CurrentValidationState { get; protected set; } = ValidationState.Normal;

        public XuiavrgFieldTag EntryTag => _entryTag;
        public int TagIndex => _tagIndex;

        /// <summary>只读访问绑定槽列表</summary>
        public IReadOnlyList<AvrgBindingSlot> AvailableBindingSlots => _bindingSlots;

        // ============ 对象池生命周期 ============

        /// <summary>
        /// 从对象池取出时调用 — 注入元数据并初始化子属性
        /// 子类可覆写以初始化专属组件参数（Slider.minValue/maxValue 等）
        /// </summary>
        public virtual void OnPoolGet(AvrgMemberMetaData metaData)
        {
            BoundMetaData = metaData;
            CurrentValidationState = ValidationState.Normal;

            // 子类覆写点
        }

        /// <summary>
        /// 回收到对象池时调用 — 重置所有状态到初始值
        /// 子类可覆写以清理专属组件事件
        /// </summary>
        public virtual void OnPoolRelease()
        {
            CurrentAccessor = null;
            CurrentBinding = null;
            BoundMetaData = null;
            CurrentValidationState = ValidationState.Normal;

            // 重置 UI 文本
            if (_fieldLabelText != null) _fieldLabelText.text = "";
            if (_valueDisplayText != null) _valueDisplayText.text = "";
            if (_unitText != null) _unitText.text = "";
        }

        // ============ 核心绑定接口 ============

        /// <summary>
        /// 由 XuiFieldTempEntry 调用，将值访问器和 MVVM 绑定连接到此 UI 组件
        /// </summary>
        public virtual void BindToAccessor(
            IAvrgValueAccessor accessor,
            AvrgMvvmBinding binding,
            AvrgMvvmScheduler scheduler)
        {
            CurrentAccessor = accessor;
            CurrentBinding = binding;

            if (binding == null || accessor == null) return;

            // 数据 → UI（带验证状态）
            binding.OnUIDataChanged += (value, state) =>
            {
                UpdateValueDisplay(value, state);
            };

            // 验证状态变更
            binding.OnValidationStateChanged += OnValidationStateChanged;

            // 首次同步
            var initialValue = accessor.GetValue();
            UpdateValueDisplay(initialValue, ValidationState.Normal);
        }

        // ============ UI 更新 ============

        /// <summary>
        /// 根据值和验证状态更新 UI 显示
        /// 子类可覆写以处理专属组件更新（Slider.value, Image.color 等）
        /// 基类负责合成标签文本、值文本颜色、单位文本
        /// </summary>
        protected virtual void UpdateValueDisplay(object value, ValidationState state)
        {
            // 1. 字段标签合成
            if (_fieldLabelText != null)
            {
                string label = BuildLabel(state);
                _fieldLabelText.text = label;
            }

            // 2. 值显示
            if (_valueDisplayText != null)
            {
                string display = CurrentAccessor?.FormatDisplay(value) ?? value?.ToString() ?? "";
                _valueDisplayText.text = display;

                _valueDisplayText.color = state == ValidationState.Invalid
                    ? Color.red
                    : state == ValidationState.Warning
                        ? new Color(1f, 0.92f, 0.016f) // yellow
                        : Color.white;
            }

            // 3. 单位文本
            if (_unitText != null)
                _unitText.text = BoundMetaData?.UnitLabelName ?? "";
        }

        /// <summary>
        /// 合成标签文本：名称 + (只读) + 警告标记
        /// </summary>
        protected string BuildLabel(ValidationState state)
        {
            var meta = BoundMetaData;
            var sb = new StringBuilder();
            sb.Append(meta?.LabelName ?? meta?.MemberName ?? "");

            // 必填标记
            if (meta != null && meta.Required)
                sb.Append(" *");

            // 只读标记
            if (CurrentAccessor?.IsReadOnly == true)
                sb.Append(" (只读)");

            // 警告/无效标记（用富文本颜色）
            if (state == ValidationState.Warning)
                sb.Append(" <color=yellow>⚠警告</color>");
            else if (state == ValidationState.Invalid)
                sb.Append(" <color=red>⚠无效值</color>");

            return sb.ToString();
        }

        // ============ 验证状态回调 ============

        /// <summary>
        /// 验证状态变更回调 — 子类可覆写以实现专属样式切换
        /// 如 Slider 填充色变黄/红、InputField 文字变色等
        /// </summary>
        protected virtual void OnValidationStateChanged(ValidationState state)
        {
            CurrentValidationState = state;

            // 刷新标签文本（警告/无效标记在标签上）
            if (_fieldLabelText != null)
                _fieldLabelText.text = BuildLabel(state);

            // 值文本颜色
            if (_valueDisplayText != null)
            {
                _valueDisplayText.color = state == ValidationState.Invalid
                    ? Color.red
                    : state == ValidationState.Warning
                        ? new Color(1f, 0.92f, 0.016f)
                        : Color.white;
            }
        }

        // ============ 元数据广播 ============

        /// <summary>
        /// 由绑定系统调用，传递元数据并广播到 IXUIAvrgMDValueStyle 子组件
        /// </summary>
        public void InitializeWithMetaData(AvrgMemberMetaData metaData)
        {
            BoundMetaData = metaData;

            if (metaData == null) return;

            var styles = GetComponentsInChildren<IXUIAvrgMDValueStyle>(true);
            foreach (var style in styles)
            {
                if (style is MonoBehaviour mb && mb.gameObject == gameObject)
                    continue;

                style.ApplyMetaData(metaData);
            }
        }
    }
}
