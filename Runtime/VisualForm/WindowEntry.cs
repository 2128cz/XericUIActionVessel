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
        private bool m_Enabled;

        [SerializeField, Tooltip("超出安全区后是否钳制在安全区内")]
        private bool m_ClampToSafeZone;

        [SerializeField, Tooltip("是否参与屏幕空间碰撞分离计算")]
        private bool m_EnableCollision;

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
            set
            {
                m_ScreenOffset = value;
                MarkDirty();
            }
        }

        /// <summary>
        /// 是否启用此窗口。代码修改请通过 AnchorWindow.SetEntryEnabled()
        /// </summary>
        public bool Enabled => m_Enabled;

        /// <summary>
        /// 超出安全区后是否钳制在安全区内。代码修改请通过 AnchorWindow.SetEntryClampToSafeZone()
        /// </summary>
        public bool ClampToSafeZone => m_ClampToSafeZone;

        /// <summary>
        /// 是否参与屏幕空间碰撞分离计算。代码修改请通过 AnchorWindow.SetEntryEnableCollision()
        /// </summary>
        public bool EnableCollision => m_EnableCollision;

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

        /// <summary>
        /// 碰撞偏移量 — 由高级锚点坞碰撞计算后写入，与 ScreenOffset 在同一坐标空间。
        /// 当 EnableCollision 为 true 且存在重叠时，坞用 (ScreenOffset + CollisionOffset) 作为最终偏移。
        /// </summary>
        [System.NonSerialized]
        public Vector2 CollisionOffset;

        /// <summary>
        /// 是否正在参与碰撞（由锚点坞标记，当前帧存在重叠时为true）
        /// </summary>
        [System.NonSerialized]
        public bool IsColliding;

        /// <summary>
        /// 碰撞覆盖偏移量 — 在碰撞检测时，为每边的碰撞矩形扩展的像素边距。
        /// 此值在Canvas本地空间（像素），用于在检测重叠时增加额外间距。
        /// 默认为0，即使用实例RectTransform的原始尺寸检测碰撞。
        /// </summary>
        [System.NonSerialized]
        public float CollisionMargin;

        /// <summary>
        /// 实际生效的偏移量：碰撞中时返回 ScreenOffset + CollisionOffset，否则仅返回 ScreenOffset。
        /// 锚点坞在更新位置时使用此属性获取最终偏移。
        /// </summary>
        public Vector2 EffectiveOffset => (EnableCollision && IsColliding) ? ScreenOffset + CollisionOffset : ScreenOffset;
        
        // --- 内部方法（供 AnchorWindow 调用） ---

        internal void SetEnabledInternal(bool value) { m_Enabled = value; MarkDirty(); }
        internal void SetClampToSafeZoneInternal(bool value) { m_ClampToSafeZone = value; MarkDirty(); }
        internal void SetEnableCollisionInternal(bool value) { m_EnableCollision = value; MarkDirty(); }

        // --- 依赖脏标记 ---

        /// <summary>
        /// 关联的WindowState（由WindowState在初始化时设置）
        /// </summary>
        internal WindowState State { get; set; }

        /// <summary>
        /// 标记此条目为脏，通知锚点坞在下一帧更新中处理此条目。
        /// 在运行时修改ScreenOffset、Enabled、ClampToSafeZone等属性时自动调用。
        /// </summary>
        public void MarkDirty()
        {
            if (State != null)
            {
                State.CoordinateDirty = true;
            }
        }

        // --- 实例生命周期虚方法（重写以自定义窗口行为） ---

        /// <summary>
        /// 实例创建后回调 — 在实例从对象池取出或新实例化后调用。
        /// 可重写以执行额外初始化（如获取组件引用、注册事件）。
        /// </summary>
        /// <param name="state">当前的WindowState中间类（提供锚点、坞、溢出状态等上下文）</param>
        /// <param name="instance">新创建/复用的GameObject实例</param>
        public virtual void OnInstanceCreated(WindowState state, GameObject instance) { }

        /// <summary>
        /// 实例激活回调 — 在锚点Enable、窗口条目应当显示时调用。
        /// 可重写以实现自定义激活逻辑。
        /// </summary>
        /// <param name="state">当前的WindowState中间类</param>
        /// <param name="instance">当前的实例GameObject（可能是原型对象或预制体实例）</param>
        public virtual void OnInstanceEnable(WindowState state, GameObject instance) { }

        /// <summary>
        /// 实例隐藏回调 — 在锚点Disable、窗口条目应当隐藏时调用。
        /// 可重写以实现自定义隐藏逻辑。
        /// </summary>
        /// <param name="state">当前的WindowState中间类</param>
        /// <param name="instance">当前的实例GameObject</param>
        public virtual void OnInstanceDisable(WindowState state, GameObject instance) { }

        /// <summary>
        /// 实例回收回调 — 在实例回收到对象池时调用。
        /// 可重写以清理状态、移除事件监听等。
        /// </summary>
        /// <param name="state">当前的WindowState中间类</param>
        /// <param name="instance">即将被回收的实例GameObject</param>
        public virtual void OnInstanceRecycle(WindowState state, GameObject instance) { }

        /// <summary>
        /// 实例销毁回调 — 在实例即将被Destroy时调用。
        /// 可重写以执行最终清理（通常情况下回收到对象池足矣）。
        /// </summary>
        /// <param name="state">当前的WindowState中间类</param>
        /// <param name="instance">即将被销毁的实例GameObject</param>
        public virtual void OnInstanceDestroy(WindowState state, GameObject instance) { }
    }
}
