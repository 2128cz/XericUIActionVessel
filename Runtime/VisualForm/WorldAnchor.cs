using UnityEngine;
using XericUI.Core;

namespace XericUI.VisualForm
{
    /// <summary>
    /// 世界对象锚点 - 挂载在场景对象上，将世界空间位置传递给锚点坞进行屏幕空间定位
    /// </summary>
    [AddComponentMenu(XericRegistrationPaths.AnchorWindow + "/WorldAnchor")]
    public class WorldAnchor : XvFormBase
    {
        [SerializeField, Tooltip("目标锚点坞，不填则自动在父级中查找")]
        private AnchorDock m_TargetDock;

        /// <summary>
        /// 目标锚点坞引用
        /// </summary>
        public AnchorDock TargetDock
        {
            get => m_TargetDock;
            set => m_TargetDock = value;
        }

        /// <summary>
        /// 缓存Transform组件
        /// </summary>
        [System.NonSerialized]
        private Transform _cachedTransform;

        /// <summary>
        /// 缓存Transform，避免重复GetComponent
        /// </summary>
        public Transform CachedTransform
        {
            get
            {
                if (_cachedTransform == null)
                    _cachedTransform = transform;
                return _cachedTransform;
            }
        }

        protected void OnEnable()
        {
            // 尝试自动查找锚点坞
            if (m_TargetDock == null)
                m_TargetDock = GetComponentInParent<AnchorDock>();

            // 回退到静态单例
            if (m_TargetDock == null)
                m_TargetDock = AnchorDock.Main;

            if (m_TargetDock != null)
            {
                m_TargetDock.AddAnchor(this);
            }
            else
            {
                // 暂存到全局待注册列表，等待坞就绪后处理
                AnchorDock.s_PendingAnchors.Add(this);
            }
        }

        protected void OnDisable()
        {
            UnregisterFromDock();
        }

        protected void OnDestroy()
        {
            UnregisterFromDock();
        }

        /// <summary>
        /// 从锚点坞或待注册列表中注销此锚点。
        /// OnDisable 和 OnDestroy 共用，确保所有退出路径都执行清理。
        /// </summary>
        private void UnregisterFromDock()
        {
            if (m_TargetDock != null)
                m_TargetDock.RemoveAnchor(this);
            else
                AnchorDock.s_PendingAnchors.Remove(this);
        }
    }
}
