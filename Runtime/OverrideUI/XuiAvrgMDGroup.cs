using UnityEngine;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// MDVM 编组容器脚本
    /// 管理 GroupPath 对应的编组容器，容纳子 UI 组件
    /// 
    /// 外部组件挂载后尺寸由父级影响
    /// 内部子元素通过锚点系统获得布局
    /// 可通过挂载 TMP_Text / Image 等组件实现组标题/背景装饰
    /// </summary>
    [AddComponentMenu("Xeric Library/UI Action Vessel/XuiAvrgMDGroup")]
    public class XuiAvrgMDGroup : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("编组路径，对应元数据的 GroupPath")]
        private string _groupName;

        [SerializeField]
        [Tooltip("编组排序值，越小越靠前")]
        private int _groupOrder;

        /// <summary>内容挂载点 — 子 UI 组件应放入此 Transform</summary>
        public Transform contentRoot;

        /// <summary>编组路径对应的组名</summary>
        public string GroupName => _groupName;

        /// <summary>编组排序值</summary>
        public int GroupOrder => _groupOrder;

        /// <summary>上下文层级深度（如 "group1/subgroup" → depth=2）</summary>
        public int ContextDepth { get; private set; }

        /// <summary>
        /// 初始化编组容器
        /// </summary>
        public void Initialize(string groupPath, int order)
        {
            _groupName = groupPath;
            _groupOrder = order;
            ContextDepth = string.IsNullOrEmpty(groupPath) ? 0 : groupPath.Split('/').Length;

            gameObject.name = $"Group [{groupPath}]";

            // 如果没有指定 contentRoot，自动使用自身作为挂载点
            if (contentRoot == null)
                contentRoot = transform;
        }
    }
}
