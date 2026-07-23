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
    /// </summary>
    [AddComponentMenu("Xeric Library/UI Action Vessel/XUIAvrgMDValue")]
    public class XUIAvrgMDValue : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("功能标记，用于与 Entry 的 Tag 匹配")]
        private XuiavrgFieldTag _entryTag = XuiavrgFieldTag.None;

        [SerializeField]
        [Tooltip("同 Tag 多实例时区分（0 表示不限制）")]
        private int _tagIndex;

        /// <summary>绑定的元数据引用</summary>
        public AvrgMemberMetaData BoundMetaData { get; private set; }

        public XuiavrgFieldTag EntryTag => _entryTag;
        public int TagIndex => _tagIndex;

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
