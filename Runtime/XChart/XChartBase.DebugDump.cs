#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using XericUI.XChart.Pools;
using XericUI.XChart.Space;

namespace XericUI.XChart
{
    /// <summary>
    /// 编辑器期的自述报表（规范 §6 的调试入口，整份文件包在 <c>#if UNITY_EDITOR</c> 里，
    /// 运行时构建看不到它——作用域纪律见 §3）。
    ///
    /// <para>开关是工程根下一个文件 <c>xchart.dump.on</c>：它存在时每 0.4 秒把这张图的关键内部状态
    /// 追加进 <c>xchart.dump.txt</c>；删掉就不再写。LateUpdate 里只按节流做一次 <c>File.Exists</c>，
    /// 不在绘制路径上反复做 IO。</para>
    ///
    /// <para>为什么走文件而不是 Console：面板（元素行）与渲染（图形）同时出问题时，需要同一个时刻
    /// 两边的读数，而 Console 会滚掉、也看不见"这一帧到底有没有算出范围"。</para>
    /// </summary>
    public abstract partial class XChartBase
    {
        private const float k_DumpInterval = 0.4f;
        private const int k_DumpCap = 300;

        /// <summary>开关文件名（面板的"自述报表 开/关"按钮和这边必须同一份口径）</summary>
        public const string DumpFlagName = "xchart.dump.on";
        /// <summary>输出文件名（追加）</summary>
        public const string DumpFileName = "xchart.dump.txt";

        /// <summary>工程根（报表开关与输出都放这里）；拿不到就返回 null。</summary>
        public static string ProjectRoot()
        {
            try
            {
                return Directory.GetParent(Application.dataPath).FullName;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private float m_NextDumpCheck;
        private int m_DumpCount;

        private void EditorDumpTick()
        {
            if (Time.realtimeSinceStartup < m_NextDumpCheck) return;
            m_NextDumpCheck = Time.realtimeSinceStartup + k_DumpInterval;

            var root = ProjectRoot();
            if (root == null) return;

            var flag = Path.Combine(root, DumpFlagName);
            if (!File.Exists(flag)) return;
            if (m_DumpCount >= k_DumpCap) return;

            m_DumpCount++;
            try
            {
                var sb = new StringBuilder();
                BuildDump(sb);
                File.AppendAllText(Path.Combine(root, DumpFileName), sb.ToString());
            }
            catch (Exception error)
            {
                // 报表自己炸了就说清楚，并停止再来（别每帧抛）
                m_NextDumpCheck = float.MaxValue;
                Debug.LogWarning("XChart 调试报表写入失败，已停止：" + error.Message);
            }
        }

        private void BuildDump(StringBuilder sb)
        {
            sb.Append("==== dump #").Append(m_DumpCount)
              .Append(" ").Append(DateTime.Now.ToString("HH:mm:ss.fff"))
              .Append(" chart=\"").Append(name).Append("\" id=").Append(GetInstanceID())
              .Append(" type=").Append(GetType().Name).Append('\n');

            sb.Append("  chart: activeSelf=").Append(gameObject.activeSelf)
              .Append(" activeInHierarchy=").Append(gameObject.activeInHierarchy)
              .Append(" hideFlags=").Append(hideFlags)
              .Append(" enabled=").Append(enabled).Append('\n');

            var rect = transform as RectTransform;
            if (rect == null) sb.Append("  rect: NOT-A-RECTTRANSFORM\n");
            else
            {
                sb.Append("  rect: size=").Append(rect.rect.width.ToString("0.#")).Append('x')
                  .Append(rect.rect.height.ToString("0.#"))
                  .Append(" anchored=").Append(rect.anchoredPosition.x.ToString("0.#")).Append(',')
                  .Append(rect.anchoredPosition.y.ToString("0.#"))
                  .Append(" lossy=").Append(rect.lossyScale.x.ToString("0.###")).Append('\n');
                sb.Append("  ancestry: ").Append(Chain(rect)).Append('\n');
            }

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) sb.Append("  canvas: NO-CANVAS（这条子树里任何 Graphic 都不可能出图）\n");
            else
            {
                sb.Append("  canvas: \"").Append(canvas.name).Append("\" mode=").Append(canvas.renderMode)
                  .Append(" scale=").Append(canvas.scaleFactor.ToString("0.###"))
                  .Append(" rootChildCount=").Append(canvas.transform.childCount).Append('\n');
            }

            var tools = ToolsRoot;
            if (tools == null) sb.Append("  toolsRoot: NULL\n");
            else
            {
                var toolsRect = tools as RectTransform;
                sb.Append("  toolsRoot: children=").Append(tools.childCount)
                  .Append(" active=").Append(tools.gameObject.activeSelf)
                  .Append(" sibling=").Append(toolsRect != null ? toolsRect.GetSiblingIndex() : -1)
                  .Append(" hideFlags=").Append(tools.gameObject.hideFlags).Append('\n');
            }

            var space = Space;
            if (space == null) sb.Append("  space: NULL（工具会全部判空跳过，画面就是空的）\n");
            else
            {
                var offset = space.Offset;
                sb.Append("  space: version=").Append(space.ProjectionVersion)
                  .Append(" halfEdge=").Append(space.HalfEdge.ToString("0.###"))
                  .Append(" sizeMode=").Append(space.SizeMode)
                  .Append(" cubeToPixel=").Append(space.CubeToPixel.ToString("0.###"))
                  .Append(" offset=").Append(offset.x.ToString("0.#")).Append(',').Append(offset.y.ToString("0.#"))
                  .Append(" euler=").Append(space.Rotation.eulerAngles.x.ToString("0.#")).Append(',')
                  .Append(space.Rotation.eulerAngles.y.ToString("0.#")).Append(',')
                  .Append(space.Rotation.eulerAngles.z.ToString("0.#")).Append('\n');
            }

            sb.Append("  elements: count=").Append(Elements.Count).Append('\n');
            for (int i = 0; i < Elements.Count; i++)
            {
                var config = Elements[i];
                if (config == null)
                {
                    sb.Append("    [").Append(i).Append("] NULL-CONFIG\n");
                    continue;
                }
                sb.Append("    [").Append(i).Append("] ").Append(config.GetType().Name)
                  .Append(" enabled=").Append(config.Enabled)
                  .Append(" name=\"").Append(config.ElementName).Append("\"")
                  .Append(" order=").Append(config.OrderIndex).Append('\n');
            }

            var pool = ToolManager != null ? ToolManager.TextPool : null;
            if (pool == null) sb.Append("  pool: NULL（申请会被静默丢掉，标签永远不出现）\n");
            else
            {
                sb.Append("  pool: owners=").Append(pool.OwnerCount)
                  .Append(" dispatches=").Append(pool.DispatchCount)
                  .Append(" acquire=").Append(pool.AcquireCount)
                  .Append(" reuse=").Append(pool.ReuseCount)
                  .Append(" release=").Append(pool.ReleaseCount)
                  .Append(" dropped=").Append(pool.DroppedCount)
                  .Append(" idle=").Append(pool.IsIdle).Append('\n');
            }

            if (ToolManager == null) { sb.Append("  tools: manager NULL\n"); return; }

            // 池根是"回收干净"的判据对象：RebuildAll 之后它要么不在，要么只剩空闲件，不该越重建越多
            var poolRoot = transform.Find(XChartTextSlotFactory.PoolRootName);
            sb.Append("  poolRoot: ").Append(poolRoot == null ? "absent"
                : "present children=" + poolRoot.childCount + " active=" + poolRoot.gameObject.activeSelf).Append('\n');
            int poolRoots = 0;
            for (int i = 0; i < transform.childCount; i++)
                if (transform.GetChild(i).name == XChartTextSlotFactory.PoolRootName) poolRoots++;
            if (poolRoots > 1) sb.Append("  POOL-ROOTS-LEAKED: ").Append(poolRoots).Append(" 个池根\n");

            // 显隐开关的判据读数：隐藏位是"要不要在 Hierarchy 里露脸"，与保存位是两件事，
            // 所以这里既印隐藏位分布，也印有没有人把 DontSave 弄丢（那才会写进资产）
            int hiddenNodes = 0, visibleNodes = 0, noDontSave = 0;
            CountHideFlags(ToolsRoot != null ? ToolsRoot.gameObject : null, ref hiddenNodes, ref visibleNodes, ref noDontSave);
            CountHideFlags(poolRoot != null ? poolRoot.gameObject : null, ref hiddenNodes, ref visibleNodes, ref noDontSave);
            sb.Append("  hideFlags: hidden=").Append(hiddenNodes)
              .Append(" shown=").Append(visibleNodes)
              .Append(" missing-DontSave=").Append(noDontSave).Append('\n');

            sb.Append("  tools: count=").Append(ToolManager.ToolCount).Append('\n');
            var list = ToolManager.Tools;
            for (int i = 0; i < list.Count; i++)
            {
                var tool = list[i];
                if (tool == null) { sb.Append("    [").Append(i).Append("] NULL-TOOL\n"); continue; }

                sb.Append("    [").Append(i).Append("] \"").Append(tool.ToolName).Append("\" ");
                tool.AppendDebugLine(sb);
                sb.Append('\n');

                var root = tool.RootTransform;
                if (root == null) sb.Append("        rootRect: NULL\n");
                else
                {
                    sb.Append("        rootRect: size=").Append(root.rect.width.ToString("0.#")).Append('x')
                      .Append(root.rect.height.ToString("0.#"))
                      .Append(" sibling=").Append(root.GetSiblingIndex())
                      .Append(" parent=\"").Append(root.parent != null ? root.parent.name : "NULL").Append("\"")
                      .Append(" children=").Append(root.childCount).Append('\n');
                }

                string failure = ToolManager.LastFailureOf(tool.ToolName);
                if (!string.IsNullOrEmpty(failure)) sb.Append("        LAST-FAILURE: ").Append(failure).Append('\n');
            }

            var dispatchFailure = ToolManager.LastDispatchFailure;
            if (!string.IsNullOrEmpty(dispatchFailure)) sb.Append("  pool-dispatch-failure: ").Append(dispatchFailure).Append('\n');
        }

        private static void CountHideFlags(GameObject node, ref int hidden, ref int shown, ref int noDontSave)
        {
            if (node == null) return;
            if ((node.hideFlags & HideFlags.HideInHierarchy) != 0) hidden++;
            else shown++;
            if ((node.hideFlags & HideFlags.DontSaveInEditor) == 0) noDontSave++;
            var t = node.transform;
            for (int i = 0; i < t.childCount; i++)
                CountHideFlags(t.GetChild(i).gameObject, ref hidden, ref shown, ref noDontSave);
        }

        private static string Chain(RectTransform node)
        {
            var sb = new StringBuilder();
            var current = node;
            for (int depth = 0; current != null && depth < 6; depth++)
            {
                if (depth > 0) sb.Append(" < ");
                sb.Append(current.name);
                if (current.GetComponent<Canvas>() != null) { sb.Append("[CANVAS]"); break; }
                current = current.parent as RectTransform;
            }
            return sb.ToString();
        }
    }
}
#endif
