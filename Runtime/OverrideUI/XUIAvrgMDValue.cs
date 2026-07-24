using System.Collections.Generic;
using UnityEngine;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// MDVM 值组件 — 统一 UI 脚本组件
    /// 替换所有 XericUIrAttributeXXX 作为预制体上的标记组件
    /// 
    /// 职责：
    /// 1. 提供 EntryTag/TagIndex 供 XuiFieldTempEntry 匹配
    /// 2. 持有绑定的 AvrgMemberMetaData 引用
    /// 3. 初始化时广播元数据到所有子节点上的 IXUIAvrgMDValueStyle
    /// 4. 定义绑定槽（BindingSlots），供拆分属性子绑定匹配
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
        public AvrgMemberMetaData BoundMetaData { get; private set; }

        public XuiavrgFieldTag EntryTag => _entryTag;
        public int TagIndex => _tagIndex;

        /// <summary>只读访问绑定槽列表</summary>
        public IReadOnlyList<AvrgBindingSlot> AvailableBindingSlots => _bindingSlots;

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
