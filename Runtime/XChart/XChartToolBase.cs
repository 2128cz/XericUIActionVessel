using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace XericUI.XChart
{
    /// <summary>
    /// 图表工具抽象基类。
    ///
    /// 每个工具对应一个独立的视觉元素（背景网格、折线、散点等），
    /// 由 ChartToolsManager 统一编排生命周期。
    ///
    /// 生命周期顺序：
    ///   InternalInitialize (构造阶段)
    ///   → OnInitialize (派生初始化)
    ///   → OnUpdate (每帧更新)
    ///   → OnRender (每帧渲染)
    ///   → OnDispose (销毁)
    /// </summary>
    public abstract class XChartToolBase : IDisposable
    {
        // ── 编辑器/运行时统一 hideFlags ────────────────────────────

        private const HideFlags k_EditorFlags = HideFlags.DontSaveInEditor | HideFlags.HideInHierarchy;
        private const HideFlags k_PlayFlags = HideFlags.HideAndDontSave;

        protected static HideFlags CurrentFlags =>
            Application.isPlaying ? k_PlayFlags : k_EditorFlags;

        // ── 基础属性 ──────────────────────────────────────────────

        protected XChartToolsManager Manager { get; private set; }

        /// <summary>
        /// 注册序（越小越先建、越先更新）。<b>它不是绘制序</b>——绘制序看 <see cref="DrawOrder"/>：
        /// 以前一个值同时管两件事，于是"想让轴先算但后画"根本表达不出来（§11.4）。
        /// </summary>
        public int OrderIndex { get; set; }

        /// <summary>
        /// 绘制序（画家序）：小者先画、大者盖在上面。只在<b>同一分组内</b>比较——
        /// 定义/背景/前景三组之间永远按分组先后画（否则前景的散点会被背景的网格盖住）。
        /// </summary>
        public int DrawOrder { get; set; }

        /// <summary>
        /// 生命周期分组：<b>类型标记</b>（由工具自己声明），不入序列化。
        /// 定义几何/范围/插槽的工具报 <see cref="XChartToolCategory.Definition"/>，它们先跑，
        /// 别的工具当帧就能取到插槽（§11.4）。
        /// </summary>
        public virtual XChartToolCategory Category => XChartToolCategory.Foreground;

        public GameObject RootObject { get; private set; }
        public RectTransform RootTransform { get; private set; }
        public string ToolName { get; private set; }
        public bool IsEnabled { get; set; } = true;
        public bool IsInitialized { get; protected set; }

        /// <summary>
        /// 渲染上下文（chart 提供）。空间、数据、显示范围都只从这里拿：
        /// <c>Context?.Space</c> / <c>Context?.Data</c> / <c>Context.TryResolveDomain(...)</c>。
        ///
        /// <para>可能为 null（初始化早期），工具必须判空跳过这一帧——不去找邻居、也不拿旧引用凑数。
        /// 规范见 <c>Documentation/XChart-RenderFramework.md</c> §2。</para>
        /// </summary>
        protected IXChartRenderContext Context => Manager?.Context;

        /// <summary>数据中转口的简写（<c>Context.Data</c>）。可能为 null，用之前判。</summary>
        protected IXChartDataSource Data => Manager?.Context?.Data;

        // ── 构造 ──────────────────────────────────────────────────

        protected XChartToolBase() { }

        internal void InternalInitialize(XChartToolsManager manager, string name,
            GameObject rootObject, int orderIndex)
        {
            Manager = manager ?? throw new ArgumentNullException(nameof(manager));
            ToolName = name;
            RootObject = rootObject ?? throw new ArgumentNullException(nameof(rootObject));
            RootTransform = rootObject.GetComponent<RectTransform>();
            if (RootTransform == null)
                RootTransform = rootObject.AddComponent<RectTransform>();
            OrderIndex = orderIndex;

            rootObject.hideFlags = CurrentFlags;

            OnInitialize();
            IsInitialized = true;
        }

        // ── 生命周期 ──────────────────────────────────────────────

        protected virtual void OnInitialize() { }
        public virtual void OnUpdate() { }
        public virtual void OnRender() { }
        protected virtual void OnDispose() { }

        public void Dispose() => Dispose(true);

        /// <summary>
        /// <paramref name="destroyRoot"/>=false：整条层级正在被 Unity 销毁（Undo 删 GameObject 也走这条路），
        /// 自己的根会跟着一起没——而那个窗口里调 DestroyImmediate 会撞进 Unity 的销毁遍历
        /// （实测 SIGSEGV，见 <c>XChartBase.Teardown</c>）。该记的账照记，只是不拆对象。
        /// </summary>
        public void Dispose(bool destroyRoot)
        {
            // 先从池里注销，再拆自己的根：顺序反了就会把随根一起销毁的标签当成"可归还"塞回空闲表
            Manager?.ForgetTextOwner(this);
            OnDispose();
            if (destroyRoot && RootObject != null)
            {
                UnityEngine.Object.DestroyImmediate(RootObject);
            }
            RootObject = null;
            IsInitialized = false;
        }

        // ── 文本槽位（申请与读取；发落在 chart 的池调度里）────────────

        private static readonly IReadOnlyList<TMP_Text> k_NoTexts = new TMP_Text[0];

        private RectTransform m_TextHost;
        private int m_RequestedTexts;

        /// <summary>
        /// 标签的宿主节点（第一次申请时惰性建，挂在工具自己的根下）。
        /// 工具根随立方体走，所以标签天然跟着转。
        /// </summary>
        protected RectTransform TextHost => m_TextHost ?? (m_TextHost = CreateStretchChild("GeneratedTexts"));

        /// <summary>
        /// 已发落的文本槽位。<b>长度可以短于申请数</b>（申请到发落之间隔一次调度），
        /// 所以取值一律 <c>i &lt; Texts.Count</c> 并跳过 null——不 NRE，也不静默画错。
        /// 每次现问池要：池换回列表时旧引用就作废了，缓存它等于读一份没人维护的副本。
        /// </summary>
        protected IReadOnlyList<TMP_Text> Texts
        {
            get
            {
                var pool = Manager != null ? Manager.TextPool : null;
                return pool != null ? pool.SlotsOf(this) : k_NoTexts;
            }
        }

        /// <summary>还欠着件（手上的数量不等于申请数量）</summary>
        protected bool TextSlotsPending
        {
            get
            {
                var pool = Manager != null ? Manager.TextPool : null;
                return pool == null ? m_RequestedTexts > 0 : pool.HeldCount(this) != m_RequestedTexts;
            }
        }

        /// <summary>本工具申请到的数量（报表用：want 与 have 的差就是欠的件）</summary>
        protected int RequestedTextCount => m_RequestedTexts;

        /// <summary>
        /// 申请<b>一批</b>文本槽位：<paramref name="count"/> 是总数而不是增量，而且只登记需求（置脏），
        /// 不当场给件（规范 §5.1）。没变且已经发落到位时直接跳过，不重复打扰池。
        /// </summary>
        protected void RequestTextSlots(int count)
        {
            if (count < 0) count = 0;
            // 宿主一旦建起来就一直带着：否则 count 归 0 时池会读到 host==null，
            // 把手上的件当"无效"丢弃而不是回收归还。
            var host = count > 0 ? TextHost : m_TextHost;
            if (count == m_RequestedTexts && ReferenceEquals(host, m_TextHost) && !TextSlotsPending) return;
            m_RequestedTexts = count;
            Manager?.RequestTextSlots(this, host, count);
        }

        // ── 子组件管理 ────────────────────────────────────────────

        protected RectTransform CreateChild(string childName)
        {
            var child = new GameObject(childName, typeof(RectTransform));
            child.transform.SetParent(RootTransform, false);
            child.hideFlags = CurrentFlags;

            var rt = child.GetComponent<RectTransform>();
            if (rt == null) rt = child.AddComponent<RectTransform>();
            return rt;
        }

        protected RectTransform CreateStretchChild(string childName)
        {
            var child = CreateChild(childName);
            child.anchorMin = Vector2.zero;
            child.anchorMax = Vector2.one;
            child.offsetMin = Vector2.zero;
            child.offsetMax = Vector2.zero;
            return child;
        }

        protected T CreateChildComponent<T>(string childName) where T : Component
        {
            var child = CreateChild(childName);
            var comp = child.gameObject.GetComponent<T>();
            if (comp == null) comp = child.gameObject.AddComponent<T>();
            return comp;
        }

        public virtual void SetVisible(bool visible)
        {
            if (RootObject != null) RootObject.SetActive(visible);
        }

#if UNITY_EDITOR
        /// <summary>
        /// 调试报表里本工具的一行：只写"这一帧到底有没有画、没画的话卡在哪"的关键状态（规范 §6）。
        /// 派生类接上去补自己的判据；基类这份只报通用的可见性。
        /// </summary>
        internal virtual void AppendDebugLine(System.Text.StringBuilder sb)
        {
            sb.Append("enabled=").Append(IsEnabled)
              .Append(" init=").Append(IsInitialized)
              .Append(" root=");
            if (RootObject == null) sb.Append("NULL");
            else sb.Append(RootObject.activeSelf ? "active" : "inactive");

            // 顺序的三个读数必须分开印："谁先算"（分组＋注册序）、"谁盖谁"（分组＋绘制序＋兄弟序）
            sb.Append(" category=").Append(Category)
              .Append(" reg=").Append(OrderIndex)
              .Append(" draw=").Append(DrawOrder)
              .Append(" sibling=").Append(RootTransform != null ? RootTransform.GetSiblingIndex().ToString() : "NULL");
        }
#endif
    }
}
