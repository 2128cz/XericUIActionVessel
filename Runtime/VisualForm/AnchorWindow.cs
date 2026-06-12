using UnityEngine;

namespace XericUI.VisualForm
{
    /// <summary>
    /// 锚点窗口 - WorldAnchor的子类，持有UI窗口对象的引用。
    /// 窗口对象可以是实际的场景对象，也可以是预制体。
    /// 如果窗口锚点坞在子项中找不到对应对象，则视为预制体，需要通过实例化方法创建。
    /// </summary>
    [AddComponentMenu("Xeric UI Vessel/AnchorWindow/AnchorWindow")]
    public class AnchorWindow : WorldAnchor
    {
        [Header("窗口配置")]
        [SerializeField, Tooltip("UI窗口对象（可以是子项中的实例或预制体资源）")]
        private GameObject m_WindowObject;

        [SerializeField, Tooltip("启用UI显示开关")]
        private bool m_EnableUI = true;

        /// <summary>
        /// 窗口对象引用（可以是实例或预制体）
        /// </summary>
        public GameObject WindowObject
        {
            get => m_WindowObject;
            set => m_WindowObject = value;
        }

        /// <summary>
        /// UI启用开关，设为false时窗口实例将被隐藏
        /// </summary>
        public bool EnableUI
        {
            get => m_EnableUI;
            set
            {
                if (m_EnableUI == value) return;
                m_EnableUI = value;
                ApplyUIState();
            }
        }

        /// <summary>
        /// 判断窗口对象是否为预制体（不在锚点坞的子项中则为预制体）
        /// 此标记由WindowState在初始化时设置
        /// </summary>
        [System.NonSerialized]
        public bool IsPrefab;

        /// <summary>
        /// 获取当前有效的窗口Transform（实例或自身）
        /// </summary>
        public Transform GetWindowTransform()
        {
            if (m_WindowObject != null)
                return m_WindowObject.transform;
            return transform;
        }

        #region 工具方法

        /// <summary>
        /// 从所属锚点坞获取自己的中间窗口状态类
        /// </summary>
        /// <returns>WindowState实例，不存在则返回null</returns>
        public WindowState GetWindowState()
        {
            if (TargetDock == null)
                return null;
            return TargetDock.GetWindowState(this);
        }

        /// <summary>
        /// 获取窗口实例对象（预制体实例化后的对象，或子项中已有的对象）
        /// </summary>
        /// <returns>窗口GameObject实例</returns>
        public GameObject GetWindowInstance()
        {
            var state = GetWindowState();
            return state?.WindowInstance;
        }

        /// <summary>
        /// 获取窗口当前的屏幕坐标（容器内本地坐标）
        /// </summary>
        /// <returns>屏幕本地坐标</returns>
        public Vector2 GetScreenPosition()
        {
            var state = GetWindowState();
            if (state == null) return Vector2.zero;

            var instance = state.WindowInstance;
            if (instance == null) return Vector2.zero;

            var rt = instance.transform as RectTransform;
            if (rt != null)
                return rt.localPosition;

            return instance.transform.localPosition;
        }

        /// <summary>
        /// 获取锚点当前的世界坐标
        /// </summary>
        /// <returns>世界坐标</returns>
        public Vector3 GetWorldPosition()
        {
            var state = GetWindowState();
            if (state != null)
                return state.LastWorldPosition;
            return CachedTransform.position;
        }

        /// <summary>
        /// 检查窗口实例是否处于活动状态
        /// </summary>
        public bool IsWindowActive()
        {
            var state = GetWindowState();
            if (state == null) return false;
            return state.IsEnabled && m_EnableUI;
        }

        #endregion

        #region 内部方法

        /// <summary>
        /// 将UI开关状态应用到窗口实例
        /// </summary>
        private void ApplyUIState()
        {
            var state = GetWindowState();
            if (state == null) return;

            var instance = state.WindowInstance;
            if (instance == null) return;

            if (m_EnableUI && state.IsEnabled)
                instance.SetActive(true);
            else
                instance.SetActive(false);
        }

        #endregion
    }
}
