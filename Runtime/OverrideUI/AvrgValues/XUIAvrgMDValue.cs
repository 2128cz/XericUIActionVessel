using System.Collections.Generic;
using UnityEngine;
using XericLibrary.Runtime.SuperStyleSheet;

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
    /// 6. 管理通用 UI 插槽（标签、警告、只读、单位、值显示）+ SuperStyleSheet 样式读取
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

        // ============ 通用 UI 插槽 ============

        [Header("通用 UI 插槽")]
        [SerializeField, Tooltip("字段标签节点 — 显示字段名称 + 只读前缀。为 null 则不显示标签行")]
        protected GameObject fieldLabelNode;

        [SerializeField, Tooltip("警告样式节点 — 验证失败时 SetActive(true)，颜色由样式表控制")]
        protected GameObject warningNode;

        [SerializeField, Tooltip("只读标记节点 — IsReadOnly 时 SetActive(true)")]
        protected GameObject readOnlyNode;

        [SerializeField, Tooltip("单位文本节点 — 显示元数据的 UnitLabelName")]
        protected GameObject unitTextNode;

        [SerializeField, Tooltip("值显示文本节点 — 可为 null（子类如 Slider/Color 不依赖文本）")]
        protected GameObject valueTextNode;

        // ============ 样式表参数（路径硬编码，命名空间从 BoundMetaData.StyleNamespace 获取）============

        private readonly StyleMember _styleStepValue      = new() { StylePath = "xuiavrg/base/stepValue" };
        private readonly StyleMember _styleReadOnlyPrefix = new() { StylePath = "xuiavrg/base/readOnlyPrefix" };
        private readonly StyleMember _styleWarningColor   = new() { StylePath = "xuiavrg/base/warningColor" };

        // ============ 公共属性 ============

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

        // ============ 生命周期 ============

        protected virtual void OnEnable()
        {
            _styleStepValue.Register();
            _styleReadOnlyPrefix.Register();
            _styleWarningColor.Register();
        }

        protected virtual void OnDisable()
        {
            _styleStepValue.Unregister();
            _styleReadOnlyPrefix.Unregister();
            _styleWarningColor.Unregister();
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

            // 默认：数据 → UI 单向同步
            binding.OnUIDataChanged += value =>
            {
                UpdateDisplayValue(value);
            };

            // 首次同步
            var initialValue = accessor.GetValue();
            UpdateDisplayValue(initialValue);

            // 刷新通用插槽
            RefreshCommonNodes();
        }

        /// <summary>
        /// 更新 UI 显示值（基类默认仅查找 Text/TMP_Text 设置文本）
        /// 子类可覆写以实现专属更新逻辑（如 Slider.value, Image.color 等）
        /// </summary>
        protected virtual void UpdateDisplayValue(object value)
        {
            if (value == null) return;

#if dUI_TextMeshPro
            var tmpText = GetComponent<TMPro.TMP_Text>();
            if (tmpText != null)
            {
                tmpText.text = CurrentAccessor?.FormatDisplay(value) ?? value.ToString();
                return;
            }
#endif

            var uiText = GetComponent<UnityEngine.UI.Text>();
            if (uiText != null)
            {
                uiText.text = CurrentAccessor?.FormatDisplay(value) ?? value.ToString();
            }
        }

        /// <summary>
        /// 由绑定系统调用，传递元数据并广播到 IXUIAvrgMDValueStyle 子组件
        /// </summary>
        public void InitializeWithMetaData(AvrgMemberMetaData metaData)
        {
            BoundMetaData = metaData;

            if (metaData == null) return;

            // 广播元数据到子节点样式组件
            var styles = GetComponentsInChildren<IXUIAvrgMDValueStyle>(true);
            foreach (var style in styles)
            {
                if (style is MonoBehaviour mb && mb.gameObject == gameObject)
                    continue;

                style.ApplyMetaData(metaData);
            }
        }

        // ============ 样式读取 ============

        /// <summary>
        /// 从样式表获取值（自动使用 BoundMetaData.StyleNamespace）
        /// </summary>
        protected StyleValue GetStyleValue(StyleMember member)
        {
            if (member == null) return null;
            string ns = BoundMetaData?.StyleNamespace ?? "default";
            return StyleManager.GetValue(ns, member.StylePath);
        }

        // ============ 通用 UI 插槽刷新 ============

        /// <summary>刷新所有通用 UI 插槽（标签、单位、只读、警告标记）</summary>
        protected void RefreshCommonNodes()
        {
            var meta = BoundMetaData;
            if (meta == null) return;

            // 1. 字段标签节点
            if (fieldLabelNode != null)
            {
                string prefix = "";
                if (CurrentAccessor?.IsReadOnly == true)
                {
                    var pv = GetStyleValue(_styleReadOnlyPrefix);
                    prefix = pv?.GetValueAsString() ?? "[只读] ";
                }
                SetNodeText(fieldLabelNode, prefix + (meta.LabelName ?? meta.MemberName));
            }

            // 2. 单位文本节点
            if (unitTextNode != null)
                SetNodeText(unitTextNode, meta.UnitLabelName ?? "");

            // 3. 只读标记节点
            if (readOnlyNode != null)
                readOnlyNode.SetActive(CurrentAccessor?.IsReadOnly == true);

            // 4. warningNode 由外部验证系统触发，此方法不直接控制
        }

        /// <summary>在节点及其子节点上查找 Text / TMP_Text 并设置文本</summary>
        protected static void SetNodeText(GameObject node, string text)
        {
            if (node == null) return;
#if dUI_TextMeshPro
            var t = node.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (t != null) { t.text = text; return; }
#endif
            var ut = node.GetComponentInChildren<UnityEngine.UI.Text>(true);
            if (ut != null) ut.text = text;
        }
    }
}
