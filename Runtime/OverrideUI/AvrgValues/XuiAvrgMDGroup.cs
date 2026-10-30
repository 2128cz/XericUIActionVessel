using UnityEngine;
using XericUI.Core;

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
    [AddComponentMenu(XericRegistrationPaths.UI + "/XuiAvrgMDGroup")]
    public class XuiAvrgMDGroup : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("编组路径，对应元数据的 GroupPath")]
        private string _groupName;

        [SerializeField]
        [Tooltip("编组排序值，越小越靠前")]
        private int _groupOrder;

        /// <summary>内容挂载点 — 子 UI 组件或子组应放入此 Transform</summary>
        public Transform contentRoot;

        /// <summary>父编组（为 null 表示根组，直接挂在 Manager 下）</summary>
        public XuiAvrgMDGroup ParentGroup { get; private set; }

        /// <summary>编组路径对应的组名</summary>
        public string GroupName => _groupName;

        /// <summary>编组排序值</summary>
        public int GroupOrder => _groupOrder;

        /// <summary>上下文层级深度（如 "Stats/Speed" → depth=2）</summary>
        public int ContextDepth { get; private set; }

        /// <summary>
        /// 初始化编组容器
        /// </summary>
        public void Initialize(string groupPath, int order, XuiAvrgMDGroup parent = null)
        {
            _groupName = groupPath;
            _groupOrder = order;
            ParentGroup = parent;
            ContextDepth = string.IsNullOrEmpty(groupPath) ? 0 : groupPath.Split('/').Length;

            gameObject.name = $"Group [{groupPath}]";

            if (contentRoot == null)
                contentRoot = transform;
        }
    }
}
