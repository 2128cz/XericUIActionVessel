using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using XericUI.XChart.Pools;

namespace XericUI.XChart
{
    /// <summary>
    /// 图表工具管理器。
    ///
    /// 负责管理所有图表渲染工具的注册、生命周期编排、执行顺序控制。
    /// 挂在 XChartBase 上，作为工具的中央调度器。
    /// </summary>
    public class XChartToolsManager
    {
        // ── 工具列表 ──────────────────────────────────────────────

        private List<XChartToolBase> m_Tools = new List<XChartToolBase>(8);

        /// <summary>
        /// 更新用的调度序（分组 → 注册序）。<b>与 <see cref="m_Tools"/> 分开</b>：
        /// 那个是"注册顺序"，这个是"谁先算"。绘制不走这个表——画家序由兄弟序决定（见 <see cref="ApplyOrder"/>）。
        /// </summary>
        private readonly List<XChartToolBase> m_Dispatch = new List<XChartToolBase>(8);

        private XChartToolCategory[] m_SortCategories = new XChartToolCategory[0];
        private int[] m_SortOrders = new int[0];
        private int[] m_SortRegistrations = new int[0];
        private int[] m_SortIndex = new int[0];
        private bool m_OrderApplied;

        /// <summary>只读工具列表（用于外部遍历）</summary>
        public IReadOnlyList<XChartToolBase> Tools => m_Tools;

        /// <summary>当前工具数量</summary>
        public int ToolCount => m_Tools.Count;

        // ── 管理节点 ──────────────────────────────────────────────

        /// <summary>所有工具的根父节点（由 XChartBase 指定）</summary>
        public Transform ToolsRoot { get; set; }

        /// <summary>
        /// 渲染上下文（由 chart 建好塞进来）。元素要的空间、数据、显示范围都只从这一个门拿，
        /// 不再自己 FindTool 找邻居——查找路径有第二条，就会长出第二套投影。
        /// </summary>
        public IXChartRenderContext Context { get; set; }

        /// <summary>
        /// 文本槽位池（由 chart 建好塞进来）。<b>工具不自己 new 标签、也不自己维护实例列表</b>：
        /// 申请走 <see cref="RequestTextSlots"/>，发落在 <see cref="DispatchTextPools"/>（规范 §5）。
        /// </summary>
        public XChartSlotPool<TMP_Text> TextPool { get; set; }

        internal void RequestTextSlots(XChartToolBase owner, RectTransform host, int count)
            => TextPool?.Request(owner, host, count);

        internal void ForgetTextOwner(XChartToolBase owner) => TextPool?.Forget(owner);

        /// <summary>拆池（chart 回收重建 / 销毁时调用）：件由工厂硬销毁，管理器这边把引用清干净</summary>
        public void DestroyTextPool()
        {
            if (TextPool == null) return;
            TextPool.Dispose();
            TextPool = null;
        }

        /// <summary>
        /// 一次池调度：先释放、顺位复用、池兜底、盈余归还（chart 在渲染前调用）。
        /// <b>这里必须隔离</b>：它跑在 RenderAll 之前，抛出去就等于整张图这一帧不画
        /// （"加了一个元素后所有元素都不显示"的老形状会因此复活）。
        /// </summary>
        public void DispatchTextPools()
        {
            if (TextPool == null) return;
            try
            {
                TextPool.Dispatch();
            }
            catch (System.Exception error)
            {
                m_DispatchFailure = error.GetType().Name + ": " + error.Message;
                if (m_ReportedFailures.Add("Dispatch|" + error.Message))
                    Debug.LogWarning("XChart 文本槽位调度抛异常，本轮跳过（其余渲染照常）：\n" + error);
            }
        }

        // ── 版本控制（防溢出） ────────────────────────────────────

        private int m_Version;

        // ── 添加工具 ──────────────────────────────────────────────

        /// <summary>
        /// 添加一个图表工具（泛型版：自己 new）。
        /// </summary>
        public T AddTool<T>(string toolName, int orderIndex = 0)
            where T : XChartToolBase, new()
        {
            var tool = new T();
            AddTool(tool, toolName, orderIndex);
            return tool;
        }

        /// <summary>
        /// 添加一个已经造好的图表工具实例。
        /// 元素框架走这一条：由 <see cref="XChartElementConfig.CreateTool"/> 决定造什么，
        /// 管理器只负责建同名子 GameObject、初始化、按 OrderIndex 插入。
        /// </summary>
        public void AddTool(XChartToolBase tool, string toolName, int orderIndex = 0, int drawOrder = 0)
        {
            if (tool == null)
                throw new ArgumentNullException(nameof(tool));
            if (ToolsRoot == null)
                throw new InvalidOperationException("ToolsRoot 未设置，无法添加工具");

            if (string.IsNullOrEmpty(toolName))
                toolName = tool.GetType().Name;

            var flags = Application.isPlaying
                ? HideFlags.HideAndDontSave
                : HideFlags.DontSaveInEditor | HideFlags.HideInHierarchy;

            var rootObject = new GameObject(toolName, typeof(RectTransform));
            var rootTransform = rootObject.GetComponent<RectTransform>();
            rootTransform.SetParent(ToolsRoot, false);
            rootTransform.anchorMin = Vector2.zero;
            rootTransform.anchorMax = Vector2.one;
            rootTransform.offsetMin = Vector2.zero;
            rootTransform.offsetMax = Vector2.zero;
            rootObject.hideFlags = flags;

            tool.InternalInitialize(this, toolName, rootObject, orderIndex);
            tool.DrawOrder = drawOrder;

            // 按注册序（OrderIndex）插入：它就是"谁先建、谁先更新"
            int insertIdx = m_Tools.Count;
            for (int i = 0; i < m_Tools.Count; i++)
            {
                if (m_Tools[i].OrderIndex > orderIndex)
                {
                    insertIdx = i;
                    break;
                }
            }
            m_Tools.Insert(insertIdx, tool);

            // 兄弟序与更新序都由 ApplyOrder 统一落（不再在这里按注册序设兄弟序——
            // 那正是"注册序被迫等于绘制序"的老毛病）
            ApplyOrder();
        }

        /// <summary>
        /// 把"谁先跑、谁先画"一次落清（§11.4）：
        /// <list type="bullet">
        /// <item>更新序 = 分组（定义→背景→前景）→ 注册序：定义网格的工具先算范围/发插槽，别的当帧就能取；</item>
        /// <item>绘制序（= 兄弟序）= 分组 → <see cref="XChartToolBase.DrawOrder"/>：所以"轴先算、但画在点上面"表达得出来。</item>
        /// </list>
        /// 注册/删除工具时自动调一次，不必手工维护。
        /// </summary>
        public void ApplyOrder()
        {
            int count = m_Tools.Count;
            if (m_SortCategories.Length < count)
            {
                m_SortCategories = new XChartToolCategory[count];
                m_SortOrders = new int[count];
                m_SortRegistrations = new int[count];
                m_SortIndex = new int[count];
            }

            for (int i = 0; i < count; i++)
            {
                m_SortCategories[i] = m_Tools[i].Category;
                m_SortOrders[i] = m_Tools[i].OrderIndex;
                m_SortRegistrations[i] = i;
            }

            // 更新序：分组 → 注册序
            XChartToolOrder.Sort(count, m_SortCategories, m_SortOrders, m_SortRegistrations, m_SortIndex);
            m_Dispatch.Clear();
            for (int i = 0; i < count; i++) m_Dispatch.Add(m_Tools[m_SortIndex[i]]);

            // 绘制序（兄弟序）：分组 → 绘制序
            for (int i = 0; i < count; i++) m_SortOrders[i] = m_Tools[i].DrawOrder;
            XChartToolOrder.Sort(count, m_SortCategories, m_SortOrders, m_SortRegistrations, m_SortIndex);
            for (int i = 0; i < count; i++)
            {
                var tool = m_Tools[m_SortIndex[i]];
                if (tool != null && tool.RootTransform != null) tool.RootTransform.SetSiblingIndex(i);
            }

            m_OrderApplied = true;
            m_Version++;
        }

        // ── 移除工具 ──────────────────────────────────────────────

        public void RemoveTool(XChartToolBase tool) => RemoveTool(tool, true);

        public void RemoveTool(XChartToolBase tool, bool destroyRoot)
        {
            if (tool == null || !m_Tools.Contains(tool)) return;
            tool.Dispose(destroyRoot);
            m_Tools.Remove(tool);
            m_Version++;
            // destroyRoot=false ＝"整条层级正在被销毁"（见 XChartBase.Teardown）：那个窗口里
            // 不许再动兄弟序（会走进 Unity 的销毁遍历，与 DestroyImmediate 同族的重入）
            if (destroyRoot) ApplyOrder();
        }

        public bool RemoveToolByName(string toolName)
        {
            for (int i = m_Tools.Count - 1; i >= 0; i--)
            {
                if (m_Tools[i].ToolName == toolName)
                {
                    RemoveTool(m_Tools[i]);
                    return true;
                }
            }
            return false;
        }

        public void RemoveAllTools() => RemoveAllTools(true);

        /// <summary>
        /// <paramref name="destroyRoots"/>=false：整条层级正在被 Unity 销毁（见 XChartBase.Teardown）。
        /// 工具的根都是那一层的子孙 ⇒ 跟着一起没；那个窗口里再调 DestroyImmediate 会撞进
        /// Unity 的销毁遍历（实测 SIGSEGV）。
        /// </summary>
        public void RemoveAllTools(bool destroyRoots)
        {
            for (int i = m_Tools.Count - 1; i >= 0; i--)
                m_Tools[i].Dispose(destroyRoots);
            m_Tools.Clear();
            m_Version++;
            if (destroyRoots) ApplyOrder();
        }

        /// <summary>
        /// 只记清楚账、一个对象都不拆：给"整条层级正在被销毁"那个窗口用。
        /// 池根同样是 chart 的子孙，所以这里只丢引用，不 Dispose 一遍；调度表也一并清掉——
        /// <b>那个窗口里不许动兄弟序</b>（那与 <c>DestroyImmediate</c> 是同族的重入）。
        /// </summary>
        public void ReleaseBookkeeping()
        {
            RemoveAllTools(false);
            TextPool = null;
            m_Dispatch.Clear();
            m_OrderApplied = false;
        }

        // ── 查找工具 ──────────────────────────────────────────────

        public T FindTool<T>(string toolName = null) where T : XChartToolBase
        {
            for (int i = 0; i < m_Tools.Count; i++)
            {
                if (m_Tools[i] is T tool)
                {
                    if (toolName == null || tool.ToolName == toolName)
                        return tool;
                }
            }
            return null;
        }

        // ── 生命周期驱动 ──────────────────────────────────────────

        /// <summary>
        /// 已经报过异常的工具：<b>隔离但要出声</b>。一个工具炸掉不该让后面的工具全不画
        /// （"加了个圆柱，所有元素都不显示"就是这么来的），但悄悄吞掉更难查，
        /// 所以每种"工具 + 异常"组合只报一次。
        /// </summary>
        private readonly HashSet<string> m_ReportedFailures = new HashSet<string>();

        /// <summary>
        /// 每个工具最近一次炸在哪：日志滚过去就没了，但调试报表要能点名"这个工具这一帧没画是因为抛了"。
        /// </summary>
        private readonly Dictionary<string, string> m_LastFailures = new Dictionary<string, string>();

        public string LastFailureOf(string toolName)
        {
            string text;
            return m_LastFailures.TryGetValue(toolName, out text) ? text : null;
        }

        /// <summary>调度阶段的异常见证（报表用；null 表示没炸过）</summary>
        public string LastDispatchFailure => m_DispatchFailure;
        private string m_DispatchFailure;

        private void ReportFailure(XChartToolBase tool, string phase, System.Exception error)
        {
            string name = tool != null ? tool.ToolName : "<null>";
            m_LastFailures[name] = phase + ": " + error.Message;
            if (!m_ReportedFailures.Add(phase + '|' + name + '|' + error.GetType().Name + '|' + error.Message)) return;
            Debug.LogWarning("XChart 工具 \"" + name + "\" 在 " + phase + " 中抛异常，已跳过它，其余工具继续：\n" + error);
        }

        public void UpdateAll()
        {
            // 按调度序跑（分组 → 注册序）：定义几何/插槽的工具先算，轴线和数据图元当帧就能取到。
            // 列表为空（还没排过）就退回注册序，免得早期调用什么都跑不到。
            var order = m_OrderApplied ? (IReadOnlyList<XChartToolBase>)m_Dispatch : m_Tools;
            for (int i = 0; i < order.Count; i++)
            {
                var tool = order[i];
                if (tool == null || !tool.IsEnabled || !tool.IsInitialized) continue;

                int snap = m_Version;
                try
                {
                    tool.OnUpdate();
                }
                catch (System.Exception error)
                {
                    ReportFailure(tool, "OnUpdate", error);
                }
                if (snap != m_Version && (i >= order.Count || order[i] != tool))
                    i--;
            }
        }

        public void RenderAll()
        {
            for (int i = 0; i < m_Tools.Count; i++)
            {
                var tool = m_Tools[i];
                if (!tool.IsEnabled || !tool.IsInitialized) continue;

                int snap = m_Version;
                try
                {
                    tool.OnRender();
                }
                catch (System.Exception error)
                {
                    ReportFailure(tool, "OnRender", error);
                }
                if (snap != m_Version && (i >= m_Tools.Count || m_Tools[i] != tool))
                    i--;
            }
        }

        // ── 清理 ──────────────────────────────────────────────────

        public void Dispose() => Dispose(true);

        /// <summary>
        /// <paramref name="destroyRoots"/>=false 时不拆任何对象（见 <see cref="RemoveAllTools(bool)"/>）：
        /// 那是 Undo/删除驱动的整层销毁窗口。
        /// </summary>
        public void Dispose(bool destroyRoots)
        {
            RemoveAllTools(destroyRoots);
            m_Tools.Clear();
        }
    }
}
