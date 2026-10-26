using System.Collections.Generic;
using TMPro;
using UnityEngine;
using XericUI.Core;
using XericUI.XChart.Pools;
using XericUI.XChart.Space;

namespace XericUI.XChart
{
    /// <summary>
    /// 图表主行为组件基类。
    ///
    /// <para><b>两条注册路径，同一个管理器</b>：</para>
    /// <list type="number">
    /// <item><b>脚本路径</b>：派生类重写 <see cref="RegisterTools"/>，在里面 <see cref="RegisterTool{T}"/>
    /// 登记工具。写死在代码里、不进序列化，适合固定组合。</item>
    /// <item><b>配置路径</b>：Inspector 的"元素"表（<see cref="XChartElementConfig"/>），
    /// 每个元素自带配置、可增删、可改字段。</item>
    /// </list>
    /// 两条路最终都落到 <see cref="XChartToolsManager"/> 上，靠 <c>OrderIndex</c> 决定绘制顺序。
    ///
    /// <para><b>生命周期（严格分段，钩子见下）</b></para>
    /// <code>
    ///   ── 初始化（Awake / OnEnable，[ExecuteAlways] 下编辑期同样执行）──
    ///     BuildToolsRoot        建 [XChartTools] + 管理器（并把数据中转口交给它）
    ///     RegisterTools()       ★ 脚本注册工具（唯一的集中注册点）
    ///     RebuildElements()     配置路径加载元素
    ///     EnsureSpace()         两条路都没登记空间工具就补一个默认的
    ///     OnChartInitialized()  通用收尾
    ///     OnRuntimeInitialized() / OnEditorInitialized()   按 Application.isPlaying 二选一
    ///     ForceRefresh()        首帧全量刷一次
    ///
    ///   ── 每帧 ──
    ///     Update()      ToolManager.UpdateAll()  +  NeedsRefresh() 时走正常刷新
    ///     LateUpdate()  ToolManager.RenderAll()  （渲染只在 LateUpdate 里做）
    ///
    ///   ── 刷新 ──
    ///     Refresh()       脏标记驱动的正常刷新（数据版本变了，或有人 RequestRefresh()）
    ///     ForceRefresh()  无条件刷新（编辑期改旋钮、重置之后都走它）
    ///     两者都汇到 OnChartRefresh(bool force)，派生类只需实现这一个
    ///
    ///   ── 重置 ──
    ///     ResetRender()  渲染层重建（不碰数据）
    ///     ResetData()    清数据 + 数据缓存，再刷新
    ///     RebuildAll()        回收重建：拆掉全部生成物再走一遍完整初始化（含每个工具）
    /// </code>
    ///
    /// <para><b>脏是怎么判的</b>：用<b>数据版本号</b>（<see cref="IXChartDataSource.Version"/>）与
    /// "本图已消费的版本"比对，而不是共用脏标记再协调清理——版本号不需要谁来清，
    /// 所以"某个消费者清了脏、另一个消费者还没看到"这类互相污染不可能发生。</para>
    /// </summary>
    [AddComponentMenu(XericRegistrationPaths.Chart + "/Chart Base")]
    [ExecuteAlways]
    public abstract partial class XChartBase : MonoBehaviour
    {
        [Header("调试")]
        [SerializeField] private bool m_ShowDebug;
        [SerializeField] private bool m_HideAllTools;
        [SerializeField] private XChartDataModel m_DataModel = new XChartDataModel();

        [Header("元素")]
        // 元素配置：所有元素（网格背景 / 轴线 / 数据图元…）共用这一张表，
        // [SerializeReference] 让每个元素按自己的实际类型序列化并在 Inspector 里展开。
        [SerializeReference] private List<XChartElementConfig> m_Elements = new List<XChartElementConfig>();

        /// <summary>
        /// 这张表的元素被填过没有。<b>它存在的唯一理由</b>是：默认元素只在"从来没有元素"的图上补
        /// （老场景里靠脚本注册的那批），他手工把表清空之后不该又被塞回来。
        /// </summary>
        [SerializeField, HideInInspector] private bool m_ElementsSeen;

        public XChartToolsManager ToolManager { get; private set; }
        public Transform ToolsRoot { get; private set; }
        public XChartDataModel DataModel => m_DataModel;

        /// <summary>
        /// 数据访问中转口：元素（网格背景 / 轴线 / 数据图元）只认这个接口，
        /// 不直接摸记录数组。默认就是 <see cref="XChartDataModel"/> 的直通实现。
        /// </summary>
        public IXChartDataSource DataSource => m_DataModel;

        private long m_ConsumedDataVersion = -1;
        private bool m_RefreshRequested = true;

        // ── hideFlags ─────────────────────────────────────────────

        /// <summary>
        /// 编辑期生成物：<b>既不进资产（DontSaveInEditor），也不出现在 Hierarchy</b>。
        /// 图是脚本画出来的，节点列表里混一堆临时物体只会让人误操作；要看就按调试按钮临时显示。
        /// </summary>
        private const HideFlags k_EditorFlags = HideFlags.DontSaveInEditor | HideFlags.HideInHierarchy;
        private const HideFlags k_PlayFlags = HideFlags.HideAndDontSave;

        internal static HideFlags CurrentFlags =>
            Application.isPlaying ? k_PlayFlags : k_EditorFlags;

        // ══ 阶段一：初始化 ═══════════════════════════════════════

        private void Awake() => EnsureInitialized();

        protected virtual void OnEnable() => EnsureInitialized();

        /// <summary>
        /// <b>“初始化过了没有”只由 <see cref="ToolManager"/> 是否在位来判</b>，不自立一个 bool 标记。
        ///
        /// <para>脚本重载时 Unity 会把托管字段备份再还原：普通 bool 原样回来，
        /// 而 <see cref="XChartToolsManager"/> 这种纯托管引用回不来（变 null）。先前那种
        /// <c>if (m_IsInitialized) return;</c> 的写法在这个时刻就成了"自称初始化完、其实什么都没有"的
        /// 僵尸态——工具建不出来，后续 <c>ReapplyElements/ForceRefresh</c> 又都在
        /// <c>ToolManager == null</c> 上静默 return，Inspector 里怎么改都不响应，只能拆了重挂。
        /// 引用本身判空就不会和它保护的东西失联。</para>
        /// </summary>
        private void EnsureInitialized()
        {
            if (ToolManager != null) return;
            BuildToolsRoot();

            RegisterTools();
            DeclareMissingElements();
            RebuildElements();
            EnsureSpace();

            OnChartInitialized();
            if (Application.isPlaying)
            {
                OnRuntimeInitialized();
            }
            else
            {
                OnEditorInitialized();
            }

            // 重建出来的都是新节点：调试开关（显示生成物 / 隐藏全部工具）必须再过一遍，
            // 否则界面上勾着、实际却没有任何效果
            ApplyDebugVerbs();
            ForceRefresh();
        }

        private void BuildToolsRoot()
        {
            var toolsRoot = transform.Find("[XChartTools]") as RectTransform;
            if (toolsRoot == null)
            {
                var toolsObj = new GameObject("[XChartTools]", typeof(RectTransform));
                toolsRoot = toolsObj.GetComponent<RectTransform>();
                toolsRoot.SetParent(transform, false);
            }
            else
            {
                // 复用到旧根就说明上一个域的生成物还挂着：那些工具的 POCO 主人已经没了，
                // 再也没人更新或清理它们，留着只会变成画不出东西的幽灵节点
                PurgeLeftoverChildren(toolsRoot);
            }

            toolsRoot.anchorMin = Vector2.zero;
            toolsRoot.anchorMax = Vector2.one;
            toolsRoot.offsetMin = Vector2.zero;
            toolsRoot.offsetMax = Vector2.zero;
            toolsRoot.pivot = new Vector2(0.5f, 0.5f);
            toolsRoot.gameObject.hideFlags = CurrentFlags;
            ToolsRoot = toolsRoot;

            PurgeLeftoverPoolRoots();

            ToolManager = new XChartToolsManager
            {
                ToolsRoot = ToolsRoot,
            };
            // 上下文的委托读的是这个字段：重建后旧上下文不会拽着旧空间不放
            ToolManager.Context = new XChartRenderContext(m_DataModel,
                () => ToolManager != null ? ToolManager.FindTool<ChartToolSpace>() : null);
            // 文本槽位池归 chart 所有：工具只申请，分配与回收在这里统一调度（规范 §5）
            ToolManager.TextPool = new XChartSlotPool<TMP_Text>(
                new XChartTextSlotFactory((RectTransform)transform));
        }

        private static void PurgeLeftoverChildren(RectTransform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i).gameObject;
                if (Application.isPlaying) Object.Destroy(child);
                else Object.DestroyImmediate(child);
            }
        }

        /// <summary>
        /// 清掉上个域遗留的池根。池根挂在 <b>chart 自己</b>下面（不在 <c>[XChartTools]</c> 里），
        /// 所以复用旧 <c>[XChartTools]</c> 的那套孤儿清理带不走它：域重载后旧池根还在，
        /// 新池再建一个，<b>每次重载攒一个</b>（验收现场报表打到 66 次 <c>POOL-ROOTS-LEAKED</c>，
        /// 他重启 Unity 正好把这件事放大）。
        /// </summary>
        private void PurgeLeftoverPoolRoots()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.name != XChartTextSlotFactory.PoolRootName) continue;
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }
        }

        /// <summary>
        /// 图里的空间工具（<b>唯一的投影权威</b>）。两条注册路径都没有登记它时，
        /// 这里补一个默认的——立方体空间的元素（背景 / 线框 / 散点 / 轴线）都靠它对齐，
        /// 少了它就变成"各算各的投影"。
        /// </summary>
        public ChartToolSpace Space => ToolManager?.FindTool<ChartToolSpace>();

        private void EnsureSpace()
        {
            if (ToolManager == null || ToolManager.FindTool<ChartToolSpace>() != null) return;
            ToolManager.AddTool<ChartToolSpace>(ChartToolSpace.DefaultToolName, -1000);
        }

        /// <summary>
        /// ★ <b>派生类在这里声明"这张图默认带哪些元素"</b>（规范 §22）。
        ///
        /// <para>渲染参数挂在图上（旧版 <c>XPseudo3DScatterChart</c> 的「立方体外观」那组字段）有两个毛病：
        /// 面板里它不在「工具」下，看不见是哪个东西在用；以及换一张图、加第二个背景时那份旋钮不能复用。
        /// 搬进元素表之后，参数随元素走，脚本注册与配置注册是同一条路。</para>
        ///
        /// <para><b>只在这张表"第一次被读到"时调用一次</b>（见 <see cref="m_ElementsSeen"/>），
        /// 而且 <see cref="AddDefaultElement{T}"/> 按类型去重：表里已经有这一类就不再塞第二份。
        /// 于是两种历史形态都照顾到了——表是空的（新图）补出整套默认元素；表里只有轴线/散点
        /// （<b>老场景</b>：立方体那几件当时还是图上的字段、不在表里）只把缺的那几件补上，
        /// 而不是"升级之后背景整块没了"。
        /// 他之后把某个元素删掉，标记已置，<b>不会再自动补回来</b>——那是对着意愿干架。</para>
        /// </summary>
        protected virtual void DeclareDefaultElements() { }

        /// <summary>把一份元素配置加进表（面板上的 Add 走序列化属性，不从这里走）</summary>
        protected T AddElement<T>(T config) where T : XChartElementConfig
        {
            if (config == null || m_Elements.Contains(config)) return config;
            m_Elements.Add(config);
            return config;
        }

        /// <summary>
        /// 补一份<b>默认</b>元素：这一类配置在表里已经存在时就沿用现有那条、不加第二份
        /// （语义与判据见 <see cref="DeclareDefaultElements"/>）。
        /// 真要"两个背景"是主动的事：面板 Add，或 <see cref="AddElement{T}"/>。
        /// </summary>
        protected T AddDefaultElement<T>(T config) where T : XChartElementConfig
        {
            int at = XChartElementTable.IndexOfKind(m_Elements, typeof(T));
            if (at >= 0) return m_Elements[at] as T;
            return AddElement(config);
        }

        /// <summary>
        /// 第一次见到这张表 ⇒ 让派生类把这张图默认该有的元素补齐，并把"见过这张表"记下来。
        /// 编辑器里额外打一次脏：默认元素要能存进场景、能在面板上被编辑，否则下次进来又是一张生表。
        /// </summary>
        private void DeclareMissingElements()
        {
            if (m_ElementsSeen) return;
            m_ElementsSeen = true;

            int before = m_Elements.Count;
            DeclareDefaultElements();
#if UNITY_EDITOR
            if (m_Elements.Count > before && !Application.isPlaying) UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>
        /// ★ <b>脚本注册工具的唯一集中点</b>：派生类在这里登记自己要用的渲染工具。
        /// 只在初始化时调用一次；配置路径（Inspector 元素表）走 <see cref="RebuildElements"/>，
        /// 两条路互不冲突，都会进同一个管理器。
        ///
        /// <para>新的预设请优先走 <see cref="DeclareDefaultElements"/>（元素表里的一条配置）：
        /// 那样面板上看得见、能改、能存。脚本路径留给"这张图必需的托管组件"（例如散点渲染器）。</para>
        /// </summary>
        protected virtual void RegisterTools() { }

        /// <summary>登记一个工具（脚本路径的便捷写法，返回实例便于后续配置）</summary>
        protected T RegisterTool<T>(string toolName, int orderIndex = 0)
            where T : XChartToolBase, new()
            => ToolManager.AddTool<T>(toolName, orderIndex);

        /// <summary>登记一个已经造好的工具实例</summary>
        protected void RegisterTool(XChartToolBase tool, string toolName, int orderIndex = 0)
            => ToolManager?.AddTool(tool, toolName, orderIndex);

        /// <summary>初始化全部完成（两条注册路径都已落地）后的通用钩子</summary>
        protected virtual void OnChartInitialized() { }

        /// <summary>仅运行时（Application.isPlaying）初始化后调用</summary>
        protected virtual void OnRuntimeInitialized() { }

        /// <summary>仅编辑器（编辑期，[ExecuteAlways] 下）初始化后调用</summary>
        protected virtual void OnEditorInitialized() { }

        // ══ 阶段二：每帧 ════════════════════════════════════════

        protected virtual void Update()
        {
            if (ToolManager == null) return;
            // 工具在这里算出"这帧要几个标签"并申请（只置脏）
            ToolManager.UpdateAll();
            if (NeedsRefresh()) Refresh();
        }

        protected virtual void LateUpdate()
        {
            if (ToolManager == null) return;
            // 申请都收齐了才统一发落，然后才轮到渲染：顺序反了就会出现"这帧申请、下帧才有件"
            ToolManager.DispatchTextPools();
            ToolManager.RenderAll();
#if UNITY_EDITOR
            EditorDumpTick();
#endif
        }

        // ══ 阶段三：刷新 ════════════════════════════════════════

        /// <summary>是否需要刷新：数据版本变了，或有人显式请求过</summary>
        public bool NeedsRefresh()
            => m_RefreshRequested || m_ConsumedDataVersion != DataSource.Version;

        /// <summary>请求一次刷新（脏标记）：下一帧走正常刷新流程</summary>
        public void RequestRefresh() => m_RefreshRequested = true;

        /// <summary>正常刷新（版本比对不过就跳过，除非有人请求过）</summary>
        public void Refresh()
        {
            if (!NeedsRefresh()) return;
            m_RefreshRequested = false;
            m_ConsumedDataVersion = DataSource.Version;
            OnChartRefresh(false);
        }

        /// <summary>无条件刷新：编辑期改旋钮、重置之后都走这条</summary>
        public void ForceRefresh()
        {
            m_RefreshRequested = false;
            m_ConsumedDataVersion = DataSource.Version;
            OnChartRefresh(true);
        }

        /// <summary>
        /// 刷新钩子。<paramref name="force"/> 为 true 表示无条件刷新。
        /// 派生类只需要实现这一个，正常刷新与强制刷新的差别由它自己决定。
        /// </summary>
        protected virtual void OnChartRefresh(bool force) { }

        // ══ 阶段四：重置 ════════════════════════════════════════

        /// <summary>渲染层重置：不碰数据，只让渲染重新来过</summary>
        public void ResetRender()
        {
            OnResetRender();
            ForceRefresh();
        }

        /// <summary>数据重置：清空数据与数据缓存，再刷新</summary>
        public void ResetData()
        {
            m_DataModel?.Clear();
            OnResetData();
            ForceRefresh();
        }

        /// <summary>渲染层重置钩子</summary>
        protected virtual void OnResetRender() { }

        /// <summary>数据重置钩子（数据本身已由 <see cref="ResetData"/> 清空）</summary>
        protected virtual void OnResetData() { }

        /// <summary>
        /// <b>回收重建</b>：把自己生成的全部节点拆掉，再走一遍完整初始化流程——chart 的初始化流程
        /// 和每个工具的初始化流程依次重来（拆目录是 chart 的活，不下放给某个工具）。
        /// 脚本重载、配置结构大改、或调试菜单里手动触发时用。
        /// </summary>
        public void RebuildAll()
        {
            Teardown();
            EnsureInitialized();
        }

        /// <summary>
        /// 调试开关：临时显示/隐藏生成节点。<b>只改可见位，不动保存位</b>——
        /// 关掉隐藏是为了让人能选中它们看参数，绝不是让它们被存进资产或场景。
        ///
        /// <para>遍历的是"chart 的整条生成子树"：池根挂在 chart 自己下面（不在 <c>[XChartTools]</c> 里），
        /// 件又在工具的宿主之下（<c>[XChartTools]/工具/GeneratedTexts/TextSlot_n</c>），
        /// 所以<b>按名字认根 + 递归到底</b>，不按层数猜——按层数猜就是"按了没反应"。</para>
        /// </summary>
        public void SetGeneratedVisible(bool visible)
        {
            if (Application.isPlaying) return;   // 运行期是 HideAndDontSave，没有临时显示这件事

            if (ToolsRoot != null) ApplyHideFlagsDeep(ToolsRoot.gameObject, visible);
            var poolRoot = transform.Find(XChartTextSlotFactory.PoolRootName);
            if (poolRoot != null) ApplyHideFlagsDeep(poolRoot.gameObject, visible);
        }

        private static void ApplyHideFlagsDeep(GameObject target, bool visible)
        {
            ApplyHideFlags(target, visible);
            var node = target.transform;
            for (int i = 0; i < node.childCount; i++)
                ApplyHideFlagsDeep(node.GetChild(i).gameObject, visible);
        }

        /// <summary>
        /// 面板按钮的入口：同时把状态写回 <c>m_ShowDebug</c>，免得按钮和字段各说各话
        /// （两个地方记同一件事，总有一个会先烂）。
        /// </summary>
        public void SetDebugVisibility(bool visible)
        {
            m_ShowDebug = visible;
            SetGeneratedVisible(visible);
        }

        /// <summary>
        /// 两个调试开关的实际效果。这两个字段原先<b>没有任何读者</b>——"开了没反应"就是这么来的：
        /// <c>m_ShowDebug</c> 接到"显示生成节点"，<c>m_HideAllTools</c> 接到"隐藏所有工具"。
        /// 每次初始化与每次 Inspector 改值都过一遍，重建后也不会掉。
        /// </summary>
        public void ApplyDebugVerbs()
        {
            SetGeneratedVisible(m_ShowDebug);

            if (ToolManager == null) return;
            var tools = ToolManager.Tools;
            for (int i = 0; i < tools.Count; i++)
            {
                if (tools[i] != null) tools[i].SetVisible(!m_HideAllTools);
            }
        }

        private static void ApplyHideFlags(GameObject target, bool visible)
        {
            if (Application.isPlaying) return;   // 运行期是 HideAndDontSave，没有临时显示这件事
            target.hideFlags = visible
                ? HideFlags.DontSaveInEditor
                : HideFlags.DontSaveInEditor | HideFlags.HideInHierarchy;
        }

        // ── 工具管理（公开 API） ──────────────────────────────────

        public T AddTool<T>(string toolName, int orderIndex = 0)
            where T : XChartToolBase, new()
        {
            EnsureInitialized();
            return ToolManager.AddTool<T>(toolName, orderIndex);
        }

        public bool RemoveTool(string toolName)
        {
            if (ToolManager == null) return false;
            return ToolManager.RemoveToolByName(toolName);
        }

        public void RemoveAllTools() => ToolManager?.RemoveAllTools();

        /// <summary>
        /// 按类型取<b>已加载的元素实例</b>（同类多条时用 <paramref name="toolName"/> 点名，名字见
        /// <see cref="ElementNameFor"/>）。没初始化、没找到都答 null——不抛，因为"图里现在有没有这一类"
        /// 是现场状态，调用方要能问出"没有"这个答案。
        ///
        /// <para>取到之后直接写它的公开旋钮就行：每个 setter 自己置脏（规范 §27），不用补 <c>MarkDirty()</c>。</para>
        /// </summary>
        public T Tool<T>(string toolName = null) where T : XChartToolBase
            => ToolManager != null ? ToolManager.FindTool<T>(toolName) : null;

        // ── 元素（配置路径） ──────────────────────────────────────

        /// <summary>元素配置表（[SerializeReference]，Inspector 里逐项展开自己类型的字段）</summary>
        public IReadOnlyList<XChartElementConfig> Elements => m_Elements;

        /// <summary>当前已加载的元素实例（与配置表同序、只含启用的项）</summary>
        public IReadOnlyList<XChartToolBase> ElementTools => m_ElementTools;

        /// <summary>
        /// 按类型取表里<b>第一份</b>这一类的配置，没有则 null。
        /// "哪一条是这一类"只许有这一个答法（与 <see cref="XChartElementTable.IndexOfKind"/> 同源），
        /// 面板、默认元素去重、外部写配置都读这一条。
        /// </summary>
        public T Element<T>() where T : XChartElementConfig
            => XChartElementTable.FirstOfKind(m_Elements, typeof(T)) as T;

        private readonly List<XChartToolBase> m_ElementTools = new List<XChartToolBase>();

        /// <summary>
        /// 元素在表里的登记名：默认名按类型给，同类加第二个就会撞（现场拍到 <c>Axis, Axis</c>），
        /// 而按名字配对就会把第二条轴的字段灌进第一条。所以同名按<b>表内出现次序</b>补序号——
        /// 它只由位置决定，不入库，重建后同名元素还是同一个名字。
        /// </summary>
        public string ElementNameFor(int index)
        {
            if (index < 0 || index >= m_Elements.Count) return string.Empty;
            var config = m_Elements[index];
            if (config == null) return string.Empty;

            string baseName = config.ElementName;
            for (int earlier = 0; earlier < index; earlier++)
            {
                var other = m_Elements[earlier];
                if (other != null && other.ElementName == baseName) return baseName + " " + (index + 1);
            }
            return baseName;
        }

        /// <summary>
        /// 按配置表重建全部元素：清掉旧的 → 逐个 CreateTool/AddTool/ApplyTo。
        /// 轴线、网格背景、数据图元都走这一条，图表不需要为每个元素各写一段 Sync。
        /// </summary>
        public void RebuildElements()
        {
            // 管理器不在就走完整初始化（它内部会灌一遍元素表），别在这里再建第二次
            if (ToolManager == null)
            {
                EnsureInitialized();
                return;
            }

            for (int i = 0; i < m_ElementTools.Count; i++)
            {
                if (m_ElementTools[i] != null) ToolManager.RemoveTool(m_ElementTools[i]);
            }
            m_ElementTools.Clear();

            for (int i = 0; i < m_Elements.Count; i++)
            {
                var config = m_Elements[i];
                if (config == null || !config.Enabled) continue;

                var tool = config.CreateTool();
                if (tool == null) continue;

                ToolManager.AddTool(tool, ElementNameFor(i), config.OrderIndex, config.DrawOrder);
                config.ApplyTo(tool);
                m_ElementTools.Add(tool);
            }

            // "图里一定有一个空间工具"这条不变量要在**每次重建后**都成立，不只是初始化那一次：
            // 面板上把「空间（基变换）」这一条删掉或停用是合法动作，而投影权威一没，线框/网格/散点
            // 会一起不画（现象是"整张图突然空了"）。补一个默认的就是为了让"删掉它"不等于"图没了"。
            EnsureSpace();

            OnElementsRebuilt();
        }

        /// <summary>
        /// 配置里改了字段：不重建实例，只把配置重灌进已存在的元素（按名字配对）。
        /// 配对不上（结构变了）就退回整体重建。
        /// </summary>
        public void ReapplyElements()
        {
            // 管理器不在就直接重新初始化，不要静默 return——静默 return 正是
            // "改了字段什么反应都没有"的那种死法
            if (ToolManager == null)
            {
                EnsureInitialized();
                return;
            }

            for (int i = 0; i < m_Elements.Count; i++)
            {
                var config = m_Elements[i];
                if (config == null || !config.Enabled) continue;

                var tool = ToolManager.FindTool<XChartToolBase>(ElementNameFor(i));
                if (tool == null)
                {
                    RebuildElements();
                    return;
                }
                config.ApplyTo(tool);
            }
        }

        /// <summary>元素重建完成后的钩子（派生类可在此做自己的收尾）</summary>
        protected virtual void OnElementsRebuilt() { }

        // ══ 阶段五：销毁 ════════════════════════════════════════

        /// <summary>
        /// 整条层级正在被 Unity 递归销毁——<b>Undo 删 GameObject 走的也是这条路</b>。
        ///
        /// <para>这个窗口里对<b>子孙</b>调 <c>DestroyImmediate</c> 会撞进 Unity 自己的销毁遍历，
        /// 实测 SIGSEGV（2026-10-24，他的 Editor.log）：栈是
        /// <c>Object_CUSTOM_DestroyImmediate ← XChartToolBase.Dispose ← XChartToolsManager.RemoveAllTools
        /// ← Teardown ← OnDestroy ← GameObject::WillDestroyGameObject/PreDestroyRecursive
        /// ← ObjectUndo::ApplyUndo</c>；崩溃前 Unity 还连报两次
        /// <c>Child GameObject 'LineChunk0' was not registered into undo system and became dangling</c>。</para>
        ///
        /// <para>生成物（工具根 / 池根 / 分块子节点）全是本对象的子孙 ⇒ 跟着一起没。
        /// 所以这窗口只做记账（清列表、丢引用、从池里注销），一个 <c>Destroy*</c> 都不调。</para>
        /// </summary>
        private bool m_HierarchyBeingDestroyed;

        protected virtual void OnDestroy()
        {
            m_HierarchyBeingDestroyed = true;
            Teardown();
        }

        private void Teardown()
        {
            m_DataModel?.DisposeRuntime();
            m_ElementTools.Clear();
            if (ToolManager != null)
            {
                if (m_HierarchyBeingDestroyed)
                {
                    ToolManager.ReleaseBookkeeping();
                }
                else
                {
                    // 池根挂在 chart 自己下面，[XChartTools] 的清理带不走它：不先拆池，
                    // 每回收重建一次就在场景里漏一个 GeneratedTextPool
                    ToolManager.DestroyTextPool();
                    ToolManager.Dispose();
                }
                ToolManager = null;
            }
            if (ToolsRoot != null)
            {
                if (!m_HierarchyBeingDestroyed)
                {
                    if (Application.isPlaying)
                        Destroy(ToolsRoot.gameObject);
                    else
                        DestroyImmediate(ToolsRoot.gameObject);
                }
                ToolsRoot = null;
            }
            // "回收干净"是判据不是意图：池根要现拆现验，别留一个看不见的节点自称已清。
            // 销毁窗口里这条不适用——整棵子树都在被销毁，没有"漏一个"可言。
            if (!m_HierarchyBeingDestroyed)
            {
                var leftoverPoolRoot = transform.Find(XChartTextSlotFactory.PoolRootName);
                if (leftoverPoolRoot != null)
                {
                    if (Application.isPlaying) Destroy(leftoverPoolRoot.gameObject);
                    else DestroyImmediate(leftoverPoolRoot.gameObject);
                }
            }
            m_ConsumedDataVersion = -1;
            m_RefreshRequested = true;
        }

        // ── 编辑器 ────────────────────────────────────────────────

        /// <summary>
        /// Inspector 里的值变了。派生类要扩展它请写 <c>protected override void OnValidate()</c>
        /// 并记得 <c>base.OnValidate();</c>——Unity 按名字只调<b>最派生</b>的那一个，
        /// 在派生类里另写一个 <c>private void OnValidate()</c> 会把基类这份<b>遮掉</b>，
        /// 于是"改字段不刷新"（先前 XPseudo3DScatterChart 就是这么把自己的刷新弄丢的）。
        /// </summary>
        protected virtual void OnValidate()
        {
#if UNITY_EDITOR
            // Inspector 里改了配置就重灌元素，再无条件刷一次（这样编辑期就能看到实际效果）。
            // 延迟一拍：避开反序列化中途（工具还没建），也避免拖数值框时同步重建。
            if (!isActiveAndEnabled) return;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null || !isActiveAndEnabled) return;
                ApplyDebugVerbs();
                ReapplyElements();
                ForceRefresh();
            };
#endif
            OnChartValidated();
        }

        /// <summary>编辑器里参数变化后的钩子（给派生类把自己那些非元素旋钮推进工具用）</summary>
        protected virtual void OnChartValidated() { }
    }
}