using UnityEngine;

namespace XericUI.VisualForm
{
    /// <summary>
    /// 窗口条目数据 — 锚点窗口上每个窗口对象的配置集合。
    /// 包含窗口对象引用、屏幕二维偏移量和激活状态。
    /// 支持一个锚点同时生成多个定位窗口。
    /// 运行时修改通过 AnchorWindow 的 SetEntryXxx 方法，自动同步到 WindowState。
    /// </summary>
    [System.Serializable]
    public class WindowEntry
    {
        [SerializeField, Tooltip("窗口对象（可以是子项中的实例或预制体资源）")]
        private GameObject m_WindowObject;

        [SerializeField, Tooltip("屏幕二维偏移量")]
        private Vector2 m_ScreenOffset;

        [SerializeField, Tooltip("是否启用此窗口")]
        private bool m_Enabled = true;

        [SerializeField, Tooltip("超出安全区后是否钳制在安全区内")]
        private bool m_ClampToSafeZone;

        /// <summary>
        /// 窗口对象引用（实例或预制体）
        /// </summary>
        public GameObject WindowObject
        {
            get => m_WindowObject;
            set => m_WindowObject = value;
        }

        /// <summary>
        /// 屏幕二维偏移量
        /// </summary>
        public Vector2 ScreenOffset
        {
            get => m_ScreenOffset;
            set => m_ScreenOffset = value;
        }

        /// <summary>
        /// 是否启用此窗口。代码修改请通过 AnchorWindow.SetEntryEnabled()
        /// </summary>
        public bool Enabled => m_Enabled;

        /// <summary>
        /// 超出安全区后是否钳制在安全区内。代码修改请通过 AnchorWindow.SetEntryClampToSafeZone()
        /// </summary>
        public bool ClampToSafeZone => m_ClampToSafeZone;

        // --- 运行时状态（非序列化） ---

        /// <summary>
        /// 实际生成的窗口实例（原型或预制体实例）
        /// </summary>
        [System.NonSerialized]
        public GameObject Instance;

        /// <summary>
        /// 此窗口对象是否被判定为预制体（不在坞子项中）
        /// </summary>
        [System.NonSerialized]
        public bool IsPrefab;

        /// <summary>
        /// 当前帧的溢出状态（逐条目跟踪，用于检测变化触发事件）
        /// </summary>
        [System.NonSerialized]
        public OverflowState CurrentOverflow;

        // --- 内部方法（供 AnchorWindow 调用） ---

        internal void SetEnabledInternal(bool value) => m_Enabled = value;
        internal void SetClampToSafeZoneInternal(bool value) => m_ClampToSafeZone = value;
    }
}
