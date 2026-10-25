using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using XericUI.XChart;
using XericUI.XChart.Axis;
using XericUI.XChart.Mesh;
using XericUI.XChart.Space;

namespace XericUI.XChart.Editor
{
    /// <summary>
    /// 图表的层级式面板（实施依据见 <c>Documentation/XChart-RenderFramework.md</c> 的 §0/§4）。
    ///
    /// <para><b>两个板块，顺序固定</b>：「基础」= 基本变换 / 颜色 / 数据 / 调试；「工具」= 图上每个渲染元素。
    /// 工具只呈现，数据归属 chart，所以维度下标、显示范围这些东西不该散落在工具里。</para>
    ///
    /// <para><b>层级怎么来的</b>：不新增元数据——字段上的 <c>[Header]</c> 就是分组标题，
    /// <c>[Tooltip]</c> 是浮上去看的说明。标题里不放括号补充语（标题定下来就不改，解释归 Tooltip）。</para>
    ///
    /// <para><b>层级别由调用方报数</b>：chart 自己和每个元素都只走同一对入口
    /// （<see cref="MakeGroup"/> / <see cref="GroupFor"/>），块与块的父子关系由"挂在谁的 Body 里"决定。
    /// 灌完整棵树后 <see cref="FinalizeHierarchy"/> 现算深度，并把互斥标记只打在<b>叶子块</b>
    /// （下面再无折叠块、真正装字段的那一层）上：展开任意一个叶子块 ⇒ 收起其余叶子块，
    /// 任何时候只开一个详细面板。中间层（基础 / 工具 / 某个元素 / 数据）是导航，不受这条管。
    /// 所有块<b>默认折叠</b>；工具栏「Expand」是互斥的显式例外（要横向比对时一次全开），
    /// 之后点任意叶子块就回到"只开一个"。</para>
    ///
    /// <para><b>两处踩过的坑，这里按机制避开</b>：</para>
    /// <list type="number">
    /// <item>值变化的监听挂在<b>每个字段自己身上</b>，不挂在面板根上。<see cref="Foldout"/> 本身是
    /// <c>INotifyValueChanged&lt;bool&gt;</c>，挂在根上冒泡监听会把"点标题展开"当成一次数据修改，
    /// 于是一边触发重灌/重建、一边把序列化对象 Apply 一次。</item>
    /// <item>展开状态<b>不靠 ChangeEvent 回写</b>：元素挂上面板时 Unity 会补发一次默认值通知
    /// （<c>bool</c> 的默认值是 false），那一下会把所有分组的状态打回折叠。状态改为在<b>重建前</b>
    /// 从现存 Foldout 身上抄一次，控件自己就是唯一真相。</item>
    /// </list>
    /// </summary>
    [CustomEditor(typeof(XChartBase), true)]
    internal sealed class XChartBaseEditor : UnityEditor.Editor
    {
        private string m_ElementSignature;
        private int m_LastPanelRows = -1;
        private int m_LastPanelTableSize = -1;
        private int m_LastMaxDepth = -1;
        private string m_HierarchyVerdict = "NO-BUILD";
        private Label m_HierarchyLabel;
        private float m_NextWitnessWrite;

        private VisualElement m_Root;
        private readonly Dictionary<string, Foldout> m_LiveFoldouts = new Dictionary<string, Foldout>();
        private readonly Dictionary<VisualElement, Dictionary<string, Group>> m_GroupsByHost
            = new Dictionary<VisualElement, Dictionary<string, Group>>();

        /// <summary>本轮流程造出来的每个折叠块，按创建顺序。<b>层级不在这里传</b>——灌完树从树上现算。</summary>
        private readonly List<Group> m_Groups = new List<Group>();

        /// <summary>元素标题行上的 R/D 文本（值变了要就地重写，见 <see cref="RefreshOrderReadouts"/>）</summary>
        private readonly List<OrderReadout> m_OrderReadouts = new List<OrderReadout>();

        /// <summary>互斥显示的字段登记（勾了某个开关，另外几个要消失；见 <see cref="RefreshExclusiveFields"/>）</summary>
        private readonly List<ExclusiveField> m_ExclusiveFields = new List<ExclusiveField>();

        /// <summary>和 <see cref="m_Groups"/> 一一对应的纯数据视图，交给 <see cref="XChartPanelHierarchy"/> 判层级。</summary>
        private readonly List<XChartPanelHierarchy.Node<VisualElement>> m_Nodes
            = new List<XChartPanelHierarchy.Node<VisualElement>>();

        /// <summary>最深块的 key：它们共用一个互斥标记，展开一个就收起其余。</summary>
        private readonly List<string> m_ExclusiveKeys = new List<string>();

        /// <summary>批量开合中（见 <see cref="SetAllExpanded"/>）：这段时间互斥让路。</summary>
        private bool m_BulkFold;

        /// <summary>
        /// 元素表的句柄。<b>每次都现取</b>：`serializedObject.Update()` 之后旧句柄会作废，
        /// 而作废的 SerializedProperty 不报错——它安静地报 <c>arraySize == 0</c>，
        /// 于是"元素行整批不见了"（同一时刻表里其实还有 2~4 条，报表拍的到）。
        /// </summary>
        private SerializedProperty ElementsProp()
        {
            return serializedObject.FindProperty("m_Elements");
        }

        public override VisualElement CreateInspectorGUI()
        {
            m_ElementSignature = ComputeSignature(ElementsProp());

            m_Root = new VisualElement();
            m_Root.Add(BuildToolbar());
            RebuildTree();
            return m_Root;
        }

        private VisualElement BuildToolbar()
        {
            var toolbar = new Toolbar();
            // 这三个按钮的汉字（添加/元素/展/开/折/叠…）不在默认字体的图集里 ⇒ 文本测量要走"新增字形"那条路，
// 于是撞上那个坏掉的动态图集槽（§8.6）⇒ 只有它们抛 MissingReferenceException。
// 改成 ASCII 就不再需要新字形，而这仨本来就很短，英文也装得下。
            toolbar.Add(MakeButton("Add", ShowAddElementMenu));
            toolbar.Add(MakeButton("Expand", () => SetAllExpanded(true)));
            toolbar.Add(MakeButton("Collapse", () => SetAllExpanded(false)));
            return toolbar;
        }

        // ── 树装配 ────────────────────────────────────────────────

        private void RebuildTree()
        {
            if (m_Root == null) return;

            // 重建前把当前展开状态从活的控件上抄下来（唯一真相；不接受事件回写）
            foreach (var pair in m_LiveFoldouts) m_LastExpanded[pair.Key] = pair.Value.value;
            m_LiveFoldouts.Clear();

            serializedObject.Update();

            var body = m_Root.Q("panel-body");
            if (body == null)
            {
                body = new VisualElement { name = "panel-body" };
                m_Root.Add(body);
            }
            body.Clear();
            // 树要重建了：同一父级下的同名合并表也必须清掉，否则第二遍会把字段塞进上一棵树的盒子里
            m_GroupsByHost.Clear();
            m_Groups.Clear();
            m_Nodes.Clear();
            m_OrderReadouts.Clear();
            m_ExclusiveFields.Clear();

            var chart = target as XChartBase;
            body.Add(BuildBasicsBox(chart));
            body.Add(BuildToolsBox(chart));
            FinalizeHierarchy();
            WritePanelWitness();
        }

        private readonly Dictionary<string, bool> m_LastExpanded = new Dictionary<string, bool>();

        /// <summary>基础板块：图表自己的字段（按 [Header] 分组）+ 数据窗口入口 + 调试。</summary>
        private VisualElement BuildBasicsBox(XChartBase chart)
        {
            var basics = MakeGroup("chart-basics", "基础");

            // 数据这件事在面板上只留一个入口：浏览/增删行列/导入导出/每列的统计与范围覆盖
            // 全在数据窗口里（2026-10-25 他明确要求"所有信息整合到这个窗口里"，面板不要再摊一层）。
            if (chart != null)
            {
                basics.Body.Add(MakeButton("打开数据窗口", () => XChartDataWindow.OpenFor(chart)));
                basics.Body.Add(MakeReadonly("数据的增删行列、导入导出、每列统计与范围覆盖都在这个窗口里"));
            }

            AppendHeaderGroups(basics.Body, null, "chart");

            // 同名合并：基类 [Header("调试")] 的两只开关会落进这一块，别再自己 AddField 一遍
            // （验收时问"调试与工具管理是同一份字段、分两个板块的意义何在"就是这么来的）
            var debug = GroupFor(basics.Body, "chart-debug", "调试");
            var buttons = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            buttons.Add(MakeButton("重新生成全部节点", () => { chart?.RebuildAll(); RebuildTree(); }));
            buttons.Add(MakeButton("显示生成节点", () => chart?.SetDebugVisibility(true)));
            buttons.Add(MakeButton("隐藏生成节点", () => chart?.SetDebugVisibility(false)));
            debug.Body.Add(buttons);
            var second = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            second.Add(MakeButton("自述报表 开", () => SetDumpFlag(true)));
            second.Add(MakeButton("自述报表 关", () => SetDumpFlag(false)));
            second.Add(MakeButton("打开报表文件", OpenDumpFile));
            debug.Body.Add(second);
            debug.Body.Add(MakeReadonly(DumpFlagSummary()));
            // 层级读数：互斥标记到底打在几块上、有没有认输（离线探针看不见真控件那一层，所以这里必须自己印）
            m_HierarchyLabel = MakeReadonly(HierarchySummary());
            debug.Body.Add(m_HierarchyLabel);

            return basics.Root;
        }

        /// <summary>工具板块：配置注册的每个元素一个可折叠块；脚本注册的只报数量。</summary>
        private VisualElement BuildToolsBox(XChartBase chart)
        {
            var tools = MakeGroup("chart-tools", "工具");

            var list = ElementsProp();
            int rows = 0;
            if (list != null)
            {
                for (int i = 0; i < list.arraySize; i++)
                {
                    var elementProp = list.GetArrayElementAtIndex(i);
                    var config = elementProp.managedReferenceValue as XChartElementConfig;
                    if (config == null) continue;

                    int index = i;
                    // 标题用登记名（同类第二个会带序号）：两条轴都叫 "Axis" 的话，行和工具对不上号
                    string title = chart != null ? chart.ElementNameFor(index) : config.ElementName;
                    var block = MakeGroup(elementProp.propertyPath, title);
                    block.Root.tooltip = ObjectNames.NicifyVariableName(config.GetType().Name);

                    // **一行装下状态**（他 2026-10-25 给的格式：`▼ √ Cylinder (R1, D4) [↑][↓][-]`）：
                    // 元素块默认折叠着，这些控件放在 Body 里就等于看不见——他报"注册序/绘制序的字段
                    // 我在界面上没看到"正是这个原因（控件在，只是那块是收起来的）。
                    var head = MakeHeaderRow(block);
                    var enabled = elementProp.FindPropertyRelative("m_Enabled");
                    if (enabled != null) head.Add(MakeEnabledToggle(enabled));
                    var order = elementProp.FindPropertyRelative("m_OrderIndex");
                    var drawOrder = elementProp.FindPropertyRelative("m_DrawOrder");
                    // 标题行里是**文本**（他 2026-10-25 第二轮："做成文本，格式就是 R 几 D 几"）：
                    // 这里放过输入框会露出拖拽光标，而且标签把宽度吃掉之后数字根本看不见
                    var orderText = new Label(OrderText(order, drawOrder))
                    {
                        tooltip = "R＝注册序（谁先建、谁先算）　D＝绘制序（谁盖谁；只在同一分组内比较）",
                        style = { marginLeft = 8, flexShrink = 1 },
                    };
                    head.Add(orderText);
                    m_OrderReadouts.Add(new OrderReadout { Order = order, Draw = drawOrder, Text = orderText });
                    head.Add(MakeTinyButton("↑", () => MoveElement(index, -1),
                        "上移一位：它在表里更靠前 ⇒ 绘得更早，会被后面的元素盖住"));
                    head.Add(MakeTinyButton("↓", () => MoveElement(index, 1),
                        "下移一位：它在表里更靠后 ⇒ 绘得更晚，盖住前面的元素"));
                    head.Add(MakeTinyButton("-", () => RemoveElement(index),
                        "删除这个元素。默认元素（空间 / BG Cube Wire / BG Cube Grid）**也能删**——"
                        + "删了就是不要它；预设不会在之后又把它补回来（只在第一次读到这张表时补齐缺的那几件）"));

                    // ★ 序号没有写入口（他 2026-10-25）：注册序由类型定好、绘制序跟元素在表里的位置走（§24.5）。
                    // 位置是唯一权威——再多两个手填的格子，就会出现"位置与数字各说一套"，
                    // 而"上移之后绘制顺序到底变没变"这个问题就没人回答得了。

                    // 这个元素要几个数值维、几条记录，表里现在各有多少——**三个数一起印**（§25.1）。
                    // "加了工具但什么都没显示"最常见的原因就是形状不够，而画面上两者长得一模一样。
                    if (config.RequiredNumericDimensions > 0)
                    {
                        int haveDims = chart != null && chart.DataModel != null
                            ? chart.DataModel.NumericDimensionCount
                            : 0;
                        int haveRecords = chart != null && chart.DataModel != null
                            ? chart.DataModel.RecordCount
                            : 0;
                        int needDims = config.RequiredNumericDimensions;
                        int needRecords = config.RequiredRecordCount;

                        string dimsPart = "需要 " + needDims + " 个数值维，表现在有 " + haveDims + " 个";
                        string recordsPart = needRecords > 0
                            ? "；需要 " + needRecords + " 条记录，表现在有 " + haveRecords + " 条"
                            : string.Empty;
                        bool shortOfShape = haveDims < needDims || haveRecords < needRecords;
                        block.Body.Add(MakeReadonly(dimsPart + recordsPart
                            + (shortOfShape
                                ? "　⇒ 画不出来（报表里 dims=" + haveDims + "/" + needDims
                                  + " records=" + haveRecords + "）"
                                : string.Empty)));
                    }

                    AppendHeaderGroups(block.Body, elementProp, elementProp.propertyPath);

                    // 宿主面：从**当前板上真有的插槽**里挑（外加手填），不让人去猜 `mesh.face.3` 这种字符串
                    if (config is XChartAxisConfig)
                    {
                        AddSlotPicker(block.Body, elementProp.FindPropertyRelative("m_SlotId"), chart);
                    }

                    // 刻度与网格分格的对齐提示：两套口径（nice-step 铺刻度 / GridDivisions 铺分格线）
                    // 不派生，而是把算式和当前数字印出来——他要的是"能对上"，先得知道差在哪
                    if (config is XChartAxisConfig)
                    {
                        var major = elementProp.FindPropertyRelative("m_TargetMajorCount");
                        int ticks = major != null ? major.intValue : 0;
                        int cells = Mathf.Max(ticks - 1, 1);

                        // 按方向给建议：轴沿 S 走，刻度就应该落在"沿 T 的格线"上，而那组格线由 S 方向的分格数决定
                        var direction = elementProp.FindPropertyRelative("m_Direction");
                        string along = direction != null && direction.intValue != 0 ? "T" : "S";
                        // 光说"设成 K-1"还得让人满面板找那两个旋钮——把当前值和位置一并印出来。
                        // 值住在**网格元素**那份配置里（以前是图上的字段，所以旧提示语指着"基础 → 立方体外观"）
                        string gridNow = GridKnobsHint(chart, list);

                        block.Body.Add(MakeReadonly("与网格对齐：主刻度 " + ticks + " 条 = " + cells + " 段" + gridNow));
                        LogDetailOnce("grid-align",
                            "XChart 面板：与网格对齐——主刻度 " + ticks + " 条 = " + cells + " 段，这条轴沿 " + along
                            + " 走，把立方体网格的 " + along + " 方向分格数设成 " + cells + " 就逐线对上"
                            + "（0 表示跟随统一值）；前提是轴铺满整面、且范围能被步长整除。");

                        // 两头对不上最可能的原因就是这条轴只铺了面宽的一部分（默认内缩过）。
                        // 网格线铺的是整面 0..1，轴铺 8%..95% 的话怎么调分格数都对不上。
                        var start = elementProp.FindPropertyRelative("m_StartRatio");
                        var end = elementProp.FindPropertyRelative("m_EndRatio");
                        if (start != null && end != null && (start.floatValue > 0f || end.floatValue < 1f))
                        {
                            var warn = MakeReadonly("这条轴只铺了 " + (start.floatValue * 100f).ToString("0.#")
                                + "% .. " + (end.floatValue * 100f).ToString("0.#") + "%");
                            block.Body.Add(warn);
                            LogDetailOnce("axis-span",
                                "XChart 面板：这条轴只铺了 " + (start.floatValue * 100f).ToString("0.#")
                                + "% .. " + (end.floatValue * 100f).ToString("0.#")
                                + "% 的面宽，两头都对不上格线（网格线铺的是整面 0..1）。按“铺满整面”一步到位。");
                            block.Body.Add(MakeButton("铺满整面", () => SetAxisSpan(index, warn)));
                        }
                    }

                    tools.Body.Add(block.Root);
                    rows++;
                }
            }

            if (chart != null)
            {
                // 脚本注册的工具也要<b>逐个点名</b>（他 2026-10-25："你并没有显示出来这几个工具是什么东西？
                // 既然是工具就要放在工具下面"）。只报数量的话，"图里到底有谁"还得去翻报表或代码。
                var elementTools = chart.ElementTools;
                var live = chart.ToolManager != null ? chart.ToolManager.Tools : null;
                int listed = 0;
                var scriptBox = GroupFor(tools.Body, "chart-tools-script", "脚本注册的工具");
                if (live != null)
                {
                    for (int i = 0; i < live.Count; i++)
                    {
                        var tool = live[i];
                        if (tool == null || IsElementTool(elementTools, tool)) continue;
                        if (++listed == 1)
                            scriptBox.Body.Add(MakeReadonly("这些由派生类 RegisterTools 登记；"
                                + "字段在脚本/各自元素配置里，不在这里改"));
                        scriptBox.Body.Add(MakeReadonly(ToolSummary(tool)));
                    }
                }
                // 一行都没有也要说明为什么：分不清"没有脚本工具"和"管理器还没建"就等于没报
                if (listed == 0)
                    scriptBox.Body.Add(MakeReadonly("脚本注册的工具 0 个"
                        + (chart.ToolManager == null ? "（工具管理器还没建）" : string.Empty)));
            }

            // 证人：面板渲染出来的行数要和表长一起印进同一份报表。
            // "行整批不见了"那一次，表里其实还有 2~4 条——只有两边同印才分得清是数据没了还是读数没了。
            m_LastPanelRows = rows;
            m_LastPanelTableSize = list != null ? list.arraySize : -1;
            return tools.Root;
        }

        /// <summary>这个工具是不是元素表建出来的（按引用比，不按名字：名字可以撞，引用不会）</summary>
        private static bool IsElementTool(System.Collections.Generic.IReadOnlyList<XChartToolBase> elementTools,
            XChartToolBase tool)
        {
            if (elementTools == null) return false;
            for (int i = 0; i < elementTools.Count; i++)
                if (ReferenceEquals(elementTools[i], tool)) return true;
            return false;
        }

        /// <summary>
        /// 一个脚本注册工具的自述行：<b>名字 + 类型 + 分组 + 注册序 + 绘制序</b>。
        /// 名字与序必须一起印：「上移下移」只管元素表，脚本工具的先后由注册顺序决定，
        /// 不印出来就成了"面板上有两排同名的东西，谁是权威看不出来"。
        /// </summary>
        private static string ToolSummary(XChartToolBase tool)
        {
            return tool.ToolName + " · " + tool.GetType().Name
                + "　组=" + tool.Category + "　注册=" + tool.OrderIndex + "　绘制=" + tool.DrawOrder;
        }

        /// <summary>
        /// S / T 分格数现在的<b>住址</b>与<b>现值</b>。
        ///
        /// <para>提示语必须<em>当场找</em>这两个数，不能凭印象写死："这两个旋钮在哪"这件事刚刚搬过家
        /// （图上的「基础 → 立方体外观」→ 工具里的那份网格配置）。写死的提示语不会报错，只会把人引到
        /// 一个已经不存在的块上——比没有提示更坏。</para>
        /// </summary>
        private static string GridKnobsHint(XChartBase chart, SerializedProperty elements)
        {
            if (elements == null) return "（图里没有网格元素 ⇒ 没有格线可对：在 工具 里 Add 一个 BG Cube Grid）";

            for (int i = 0; i < elements.arraySize; i++)
            {
                var prop = elements.GetArrayElementAtIndex(i);
                if (!(prop.managedReferenceValue is XChartMeshBgConfig)) continue;

                var divisionsS = prop.FindPropertyRelative("m_GridDivisionsS");
                var divisionsT = prop.FindPropertyRelative("m_GridDivisionsT");
                if (divisionsS == null || divisionsT == null) return string.Empty;

                // 两块网格时印的是<em>第一块</em>的名字——那才是刻度这条轴实际对上的那一块
                string name = chart != null ? chart.ElementNameFor(i) : "BG Cube Grid";
                return "（现在 S=" + divisionsS.intValue + " T=" + divisionsT.intValue
                    + "，这两个旋钮在 工具 → " + name + " 里，紧挨着「网格分格数」；0＝跟随统一值）";
            }
            return "（图里没有网格元素 ⇒ 没有格线可对：在 工具 里 Add 一个 BG Cube Grid）";
        }

        /// <summary>把 owner 的直接子字段按 [Header] 切成嵌套 box；没有 Header 的进"其它"。</summary>
        private void AppendHeaderGroups(VisualElement host, SerializedProperty owner, string keyPrefix)
        {
            Type type = owner != null
                ? (owner.managedReferenceValue == null ? null : owner.managedReferenceValue.GetType())
                : target.GetType();
            if (type == null) return;

            var bodyContent = new VisualElement();
            string groupTitle = null;

            // 互斥显示的判据在配置自己身上（§25）：面板只负责"照着藏/放"。
            // 只有"元素"这一层才有配置——chart 自己那份（owner == null）是没有的，所以这里**不写**
            // `target as XChartElementConfig`：target 是 UnityEngine.Object，配置是纯托管类，那样写编不过
            var config = owner != null ? owner.managedReferenceValue as XChartElementConfig : null;

            foreach (SerializedProperty prop in DirectChildren(owner))
            {
                if (prop.name == "m_Script" || prop.name == "m_DataModel" || prop.name == "m_Elements") continue;
                // 轴线的宿主面在轴那一块有专用控件（"已发布插槽"下拉 + 手填框），
                // 这里再出一个裸文本框就是同一件事两个入口
                if (prop.name == "m_SlotId" && type == typeof(XChartAxisConfig)) continue;
                // 启用 / 注册序 / 绘制序 归**元素标题那一行**（√ R D）管了（§24.1）：基类那三个字段
                // 没有 [Header]，不跳过去它们就会整组落进"其它"，同一个写入口出现两次
                if (owner != null && (prop.name == "m_Enabled"
                        || prop.name == "m_OrderIndex" || prop.name == "m_DrawOrder")) continue;

                string header = HeaderFor(type, prop.name);
                if (header != null && header != groupTitle)
                {
                    FlushGroupBox(host, bodyContent, groupTitle ?? "其它", keyPrefix);
                    bodyContent = new VisualElement();
                    groupTitle = header;
                }
                var field = MakeField(prop);
                bodyContent.Add(field);
                if (config != null) RegisterExclusiveField(config, prop.name, field);
            }
            FlushGroupBox(host, bodyContent, groupTitle ?? "其它", keyPrefix);
        }

        private void FlushGroupBox(VisualElement host, VisualElement content, string title, string keyPrefix)
        {
            if (content.childCount == 0) return;
            // 同名合并：面板手建的"数据 / 调试"与派生类的 [Header] 同名时落进同一块
            GroupFor(host, keyPrefix + "/" + title, title).Body.Add(content);
        }

        /// <summary>
        /// 一个 box = 边框 + 可折叠标题 + <b>Body 容器</b>。折叠切换的就是 Body 的显示，
        /// 所以所有内容必须加进 <see cref="Group.Body"/>；加到 <see cref="Group.Root"/> 上
        /// 会变成"标题折叠了、内容还在"（上一版的大分类打不开就是这个原因）。
        ///
        /// <para><b>默认折叠</b>：面板一打开只露顶层两行（基础 / 工具），要看的东西一层点进去。
        /// 展开状态从 <see cref="m_LastExpanded"/> 取（没有记录就是关着），当前实例登记进
        /// <see cref="m_LiveFoldouts"/> 供下次重建抄回。</para>
        ///
        /// <para>这里<b>不接受调用方传层级</b>：<see cref="FinalizeHierarchy"/> 在树灌完后从
        /// "这块挂在谁的 Body 里"现算深度，并把互斥标记只打在最深那一层上。</para>
        /// </summary>
        private Group MakeGroup(string key, string title)
        {
            var box = new VisualElement();
            ApplyBoxStyle(box);
            box.style.marginLeft = 2;
            box.style.marginRight = 4;
            box.style.marginTop = 4;
            // 左侧留够内边距：折叠小三角是 Foldout 自带 toggle，默认样式带负缩进，
            // 留白不够它就压在左描边上（就是"box 和折叠按钮冲突"那个现象）
            box.style.paddingLeft = 16;
            box.style.paddingRight = 6;
            box.style.paddingTop = 2;
            box.style.paddingBottom = 3;

            bool expanded;
            if (!m_LastExpanded.TryGetValue(key, out expanded)) expanded = false;

            var header = new Foldout { text = title, value = expanded };
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginLeft = 0;
            header.style.marginTop = 0;
            header.style.marginBottom = 0;
            // key 钉在盒子根上（不是标题上）：算层级时沿 parent 往上走，撞到的第一个带 key 的
            // 祖先就是父块；标题是盒子的孩子、永远不会成为谁的祖先。
            box.userData = key;
            m_LiveFoldouts[key] = header;
            box.Add(header);

            var body = new VisualElement { name = "box-body" };
            body.style.marginLeft = 4;
            body.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
            // 折叠只改显示，不碰序列化对象：点标题不是一次数据修改
            header.RegisterValueChangedCallback(evt => body.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None);
            box.Add(body);

            var group = new Group { Root = box, Body = body, Header = header };
            group.Mark = new XChartPanelHierarchy.Node<VisualElement> { Key = key, Box = box };
            m_Groups.Add(group);
            m_Nodes.Add(group.Mark);
            return group;
        }

        /// <summary>
        /// 把某个块的标题改装进<b>一行横排</b>：折叠箭头与名字仍是最左那一段，右边留给状态控件。
        /// 折叠机件（<see cref="MakeGroup"/> 里那条"开合只改 Body 显示"的回调、层级互斥）都还挂在
        /// 同一个 Foldout 上，搬动位置不改它的身份。
        /// </summary>
        private static VisualElement MakeHeaderRow(Group group)
        {
            var row = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, alignItems = Align.Center },
            };
            int at = group.Root.IndexOf(group.Header);
            group.Root.Remove(group.Header);
            group.Header.style.flexShrink = 0;
            row.Add(group.Header);
            group.Root.Insert(at < 0 ? 0 : at, row);
            return row;
        }

        /// <summary>
        /// 标题行上的"启用"勾选：写的就是 <c>m_Enabled</c>，Body 里不再留第二个入口。
        /// 签名（<see cref="ComputeSignature"/>）里含 Enabled ⇒ 改它走的是 RebuildElements，
        /// 停用之后工具真的会没，而不是"勾了但还在画"。
        /// </summary>
        private Toggle MakeEnabledToggle(SerializedProperty enabled)
        {
            var toggle = new Toggle
            {
                value = enabled.boolValue,
                tooltip = "启用/停用这个元素（停用＝不创建它，配置留在表里）",
                style = { marginLeft = 4, flexShrink = 0 },
            };
            toggle.RegisterValueChangedCallback(evt =>
            {
                serializedObject.Update();
                enabled.boolValue = evt.newValue;
                OnValueChanged();
            });
            return toggle;
        }

        /// <summary>
        /// 标题行上的一段读数：<c>R1 D4</c>。用<b>文本</b>而不是输入框——
        /// 输入框的标签区会露出拖拽光标（"露馅"），而且窄到连数字都看不见（他 2026-10-25 两轮反馈）。
        /// 写入口在 Body 里；这里只显示，值一变就地改文本（<see cref="RefreshOrderReadouts"/>）。
        /// </summary>
        private static string OrderText(SerializedProperty order, SerializedProperty drawOrder)
        {
            return "R" + (order != null ? order.intValue : 0)
                + " D" + (drawOrder != null ? drawOrder.intValue : 0);
        }

        /// <summary>标题行的 R/D 读数：面板树在值变化时**不重建**（会换掉正在拖的输入框），
        /// 所以静态文本要自己去改，否则它就停在旧值上（"改了没反应"的又一种形状）</summary>
        private sealed class OrderReadout
        {
            public SerializedProperty Order;
            public SerializedProperty Draw;
            public Label Text;
        }

        /// <summary>把标题行上的 R/D 文本按序列化对象现值重写一遍（OnValueChanged 末尾调）</summary>
        private void RefreshOrderReadouts()
        {
            for (int i = 0; i < m_OrderReadouts.Count; i++)
            {
                OrderReadout readout = m_OrderReadouts[i];
                if (readout == null || readout.Text == null) continue;
                readout.Text.text = OrderText(readout.Order, readout.Draw);
            }
        }

        /// <summary>互斥显示登记的一条：哪个配置的哪个字段、对应面板上哪个控件</summary>
        private sealed class ExclusiveField
        {
            public XChartElementConfig Config;
            public string FieldName;
            public VisualElement Element;
        }

        /// <summary>
        /// 登记一个字段的"该不该藏"，并立刻按现况定初值。
        /// <b>藏而不是不建</b>：勾掉那个开关时要能立刻回来，而面板树在值变化时是不重建的——
        /// 建好再切显示，才有"勾一下它回来"这种即时手感。
        /// </summary>
        private void RegisterExclusiveField(XChartElementConfig config, string fieldName, VisualElement element)
        {
            m_ExclusiveFields.Add(new ExclusiveField { Config = config, FieldName = fieldName, Element = element });
            element.style.display = config.IsFieldHidden(fieldName) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        /// <summary>按各配置现况重算"该藏的藏起来"（值变化时调；判据在配置自己身上）</summary>
        private void RefreshExclusiveFields()
        {
            for (int i = 0; i < m_ExclusiveFields.Count; i++)
            {
                ExclusiveField item = m_ExclusiveFields[i];
                if (item == null || item.Element == null) continue;
                item.Element.style.display = item.Config != null && item.Config.IsFieldHidden(item.FieldName)
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
            }
        }

        /// <summary>一行里的单个动作键（↑ ↓ -）：一个字符 + 一句 tooltip，中文按钮名留给浮看</summary>
        private static Button MakeTinyButton(string text, Action clicked, string tooltip)
        {
            return new Button(clicked)
            {
                text = text,
                tooltip = tooltip,
                style =
                {
                    width = 22f, minWidth = 22f, flexShrink = 0,
                    marginLeft = 1, marginRight = 0, paddingLeft = 0, paddingRight = 0,
                },
            };
        }

        /// <summary>
        /// 取（没有就建）一个 box，按<b>同一父级下的标题</b>认同一块：派生类自己写的 <c>[Header("数据")]</c>
        /// 与面板手建的"数据"曾经各起一个同名 box（验收时问"分两个板块的意义何在"），现在同名合并进同一块。
        ///
        /// <para><b>合并必须限定在同一父级内</b>：两条轴元素的内部标题是同名的（"值域与刻度"/"外观"/…），
        /// 跨父级按标题合并会把第二条轴的字段全灌进第一条的盒子里，第二条看起来就是空的
        /// （P4 验收现场："只有第一个 axis 有内容，另一个 axis 的内容是空的"，就是这一条）。
        /// <b>本方法自己把 Root 挂到 host 上</b>，调用方不要再 Add 一次。</para>
        /// </summary>
        private Group GroupFor(VisualElement host, string key, string title)
        {
            if (host == null) return MakeGroup(key, title);

            Dictionary<string, Group> siblings;
            if (!m_GroupsByHost.TryGetValue(host, out siblings))
            {
                siblings = new Dictionary<string, Group>();
                m_GroupsByHost[host] = siblings;
            }

            Group existing;
            if (siblings.TryGetValue(title, out existing)) return existing;

            var group = MakeGroup(key, title);
            siblings[title] = group;
            host.Add(group.Root);
            return group;
        }

        /// <summary>一个可折叠块：Root 挂到父 Body 上，内容一律进 Body。</summary>
        private sealed class Group
        {
            public VisualElement Root;
            public VisualElement Body;
            public Foldout Header;

            /// <summary>这块的 key 与层级判定结果（父级/深度/是否最深都由 <see cref="XChartPanelHierarchy"/> 填）。</summary>
            public XChartPanelHierarchy.Node<VisualElement> Mark;
        }

        /// <summary>
        /// 树灌完后统一收口：把这一轮的块交给 <see cref="XChartPanelHierarchy"/> 判层级，
        /// 再把互斥标记<b>只</b>打在最深那一层（叶子块）上。
        ///
        /// <para><b>为什么在这儿算而不是装配时传参</b>：调用方知道自己第几层，但"挂在谁的 Body 里"
        /// 是 <see cref="GroupFor"/> 的合并规则决定的（同名合并、跨父级不合并）。传进来的层级会和
        /// 实际挂接位置分叉，而"最深那一层"必须按真实的树判——所以流程只登记块，层级事后从树上读。</para>
        ///
        /// <para><b>互斥只有一条</b>：整个面板共用一组标记，展开任意一个叶子块就收起其余叶子块，
        /// 于是任何时候最多只有一个"详细面板"开着。中间层（基础 / 工具 / 某个元素 / 数据）不受管，
        /// 它们只是导航，想同时开几个开几个。</para>
        /// </summary>
        private void FinalizeHierarchy()
        {
            m_ExclusiveKeys.Clear();

            XChartPanelHierarchy.Compute(m_Nodes, v => v.parent, v => v.userData as string);

            int deepest = 0;
            bool anyParent = false;
            m_LastMaxDepth = 0;
            foreach (var group in m_Groups)
            {
                if (group.Mark.Depth > m_LastMaxDepth) m_LastMaxDepth = group.Mark.Depth;
                if (group.Mark.ParentKey != null) anyParent = true;
                if (!group.Mark.IsLeaf) continue;

                deepest++;
                m_ExclusiveKeys.Add(group.Mark.Key);
                string self = group.Mark.Key;
                // 只接"开"这一下：开一个就把别的最深块收掉。Body 的显示不归这里管——
                // MakeGroup 里那条回调本来就是唯一的显示开关，两条各管各的。
                group.Header.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue) CollapseOtherDetails(self);
                });
            }

            m_HierarchyVerdict = HierarchyVerdict(m_Groups.Count, deepest, anyParent);
            if (m_HierarchyLabel != null) m_HierarchyLabel.text = HierarchySummary();
        }

        private string HierarchySummary()
        {
            return "层级：块 " + m_Groups.Count + "　最深 " + m_ExclusiveKeys.Count
                + "　深 " + m_LastMaxDepth + "　" + m_HierarchyVerdict;
        }

        /// <summary>
        /// 层级判决行。这两条不是装饰，是"离线探针看不见的这一层"的读数：
        /// 纯数据那侧算得再对，<c>parent</c>/<c>userData</c> 没接上时它收到的就是一串顶层节点。
        /// </summary>
        private static string HierarchyVerdict(int groups, int deepest, bool anyParent)
        {
            if (groups == 0) return "NO-GROUPS";
            if (deepest == 0) return "HIERARCHY-NO-LEAF";
            // 多于一个块却没人有父级 ⇒ 往上爬那一格没爬到东西：互斥会把导航层也一起管了
            if (groups > 1 && !anyParent) return "HIERARCHY-NO-PARENT";
            return "OK";
        }

        /// <summary>收起除 <paramref name="openKey"/> 之外所有开着的最深块。</summary>
        private void CollapseOtherDetails(string openKey)
        {
            if (m_BulkFold) return;

            for (int i = 0; i < m_ExclusiveKeys.Count; i++)
            {
                string key = m_ExclusiveKeys[i];
                if (key == openKey) continue;

                Foldout header;
                if (!m_LiveFoldouts.TryGetValue(key, out header)) continue;
                // 已经关着的别补发事件：Foldout 的 value setter 无条件发 ChangeEvent，
                // 白发给下去只是多跑一遍回调
                if (!header.value) continue;

                header.value = false;
            }
        }

        /// <summary>
        /// 2022.1 的 IStyle 没有 borderWidth/borderColor/borderRadius 这组简写（那是以后才加的），
        /// 只有分边的属性，所以 box 的描边一条条写。
        /// </summary>
        private static void ApplyBoxStyle(VisualElement box)
        {
            var line = new StyleColor(new Color(1f, 1f, 1f, 0.14f));
            box.style.borderLeftWidth = 1;
            box.style.borderRightWidth = 1;
            box.style.borderTopWidth = 1;
            box.style.borderBottomWidth = 1;
            box.style.borderLeftColor = line;
            box.style.borderRightColor = line;
            box.style.borderTopColor = line;
            box.style.borderBottomColor = line;
            box.style.borderTopLeftRadius = 3;
            box.style.borderTopRightRadius = 3;
            box.style.borderBottomLeftRadius = 3;
            box.style.borderBottomRightRadius = 3;
        }

        /// <summary>
        /// 批量开合。<b>这一趟要屏蔽互斥</b>：不屏蔽的话，"全部展开"逐个把最深块置 true，
        /// 每置一个就收掉前面刚开的那个 ⇒ 按钮按下去只剩最后一个开着（等于没展开）。
        /// 所以 Expand 是这条规则的显式例外出口（要横向比对时用它），
        /// 而只要再点任意一个最深块，规则立刻恢复成"只开一个"。
        /// </summary>
        private void SetAllExpanded(bool expanded)
        {
            m_BulkFold = true;
            foreach (var pair in m_LiveFoldouts)
            {
                pair.Value.value = expanded;
                m_LastExpanded[pair.Key] = expanded;
            }
            m_BulkFold = false;
        }

        // ── 字段 ──────────────────────────────────────────────────

        private PropertyField MakeField(SerializedProperty prop, string label = null)
        {
            var field = label == null ? new PropertyField(prop) : new PropertyField(prop, label);
            // 监听挂在这个字段自己身上（只收它内部控件冒泡上来的事件），不挂面板根：
            // 挂根上会把 Foldout 的开合也当成"值变了"
            field.RegisterCallback<ChangeEvent<float>>(evt => OnValueChanged());
            field.RegisterCallback<ChangeEvent<double>>(evt => OnValueChanged());
            field.RegisterCallback<ChangeEvent<int>>(evt => OnValueChanged());
            field.RegisterCallback<ChangeEvent<long>>(evt => OnValueChanged());
            field.RegisterCallback<ChangeEvent<bool>>(evt => OnValueChanged());
            field.RegisterCallback<ChangeEvent<string>>(evt => OnValueChanged());
            field.RegisterCallback<ChangeEvent<Enum>>(evt => OnValueChanged());
            field.RegisterCallback<ChangeEvent<Color>>(evt => OnValueChanged());
            field.RegisterCallback<ChangeEvent<Vector2>>(evt => OnValueChanged());
            field.RegisterCallback<ChangeEvent<Vector3>>(evt => OnValueChanged());
            field.RegisterCallback<ChangeEvent<Vector4>>(evt => OnValueChanged());
            field.RegisterCallback<ChangeEvent<Quaternion>>(evt => OnValueChanged());
            return field;
        }

        /// <summary>下拉里表示"不用清单里的 id，我自己填"的那一项</summary>
        private const string kManualSlot = "（手填）";

        /// <summary>
        /// 宿主面 = "已发布插槽"下拉 + 手填框。列的是**当前板上真有的** id
        /// （由定义网格的那个元素发布，见规范 §11.2），所以不用去猜 <c>mesh.face.3</c> 这种字符串；
        /// 手填保留，因为发布可能晚于面板构建（面板重开一次就到齐）。
        /// </summary>
        private void AddSlotPicker(VisualElement host, SerializedProperty slotProp, XChartBase chart)
        {
            if (slotProp == null) return;

            var available = new List<string>();
            var board = chart != null && chart.ToolManager != null && chart.ToolManager.Context != null
                ? chart.ToolManager.Context.Slots
                : null;
            if (board != null)
            {
                for (int i = 0; i < board.SlotCount; i++)
                {
                    string id;
                    XChartSlotMode mode;
                    if (!board.TryGetSlotAt(i, out id, out mode)) continue;
                    if (!available.Contains(id)) available.Add(id);
                }
            }

            var choices = new List<string> { kManualSlot };
            choices.AddRange(available);
            int current = available.IndexOf(slotProp.stringValue);

            var dropdown = new DropdownField("已发布插槽", choices, current < 0 ? 0 : current + 1);
            dropdown.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue == kManualSlot) return;   // 手填：不动现值
                serializedObject.Update();
                slotProp.stringValue = evt.newValue;
                serializedObject.ApplyModifiedProperties();
                OnValueChanged();
            });

            host.Add(dropdown);
            host.Add(MakeField(slotProp, "宿主面（可手填）"));
            host.Add(MakeReadonly("已发布插槽 " + available.Count + " 个"));
            LogDetailOnce("slots", "XChart 面板：板上已发布的插槽 id —— " + (available.Count == 0
                ? "（一个都没有：定义网格的元素还没跑，或本图没有网格元素）"
                : string.Join(", ", available.ToArray())));
        }

        /// <summary>
        /// 把某条轴铺满整面（起点 0、终点 1）。网格线铺的是整面 0..1，
        /// 轴内缩着就不可能和格线对齐——这是"两头都对不上"最常见的原因，
        /// 所以给一键而不是让人去猜两个比例该填多少。
        /// </summary>
        private void SetAxisSpan(int elementIndex, Label warn)
        {
            serializedObject.Update();
            var table = ElementsProp();
            if (table == null || elementIndex < 0 || elementIndex >= table.arraySize) return;

            var element = table.GetArrayElementAtIndex(elementIndex);
            var start = element.FindPropertyRelative("m_StartRatio");
            var end = element.FindPropertyRelative("m_EndRatio");
            if (start == null || end == null) return;

            start.floatValue = 0f;
            end.floatValue = 1f;
            serializedObject.ApplyModifiedProperties();
            if (warn != null) warn.text = "已铺满整面（0% .. 100%）：刻度现在能落在格线上了";
            OnValueChanged();
        }

        /// <summary>
        /// 值变了：重灌元素 + 无条件刷一次。<b>不重建面板</b>——只有结构真的变了（增删/移动/启停/换类型）
        /// 才重建工具，且面板树保持不动，用户正在拖的输入框才不会半路被换掉。
        /// </summary>
        private void OnValueChanged()
        {
            serializedObject.ApplyModifiedProperties();
            // 面板树不重建（重建会换掉用户正在拖的输入框）⇒ 标题行的 R/D 文本与"互斥该藏谁"
            // 都要就地重算，否则它们停在旧状态上（"改了没反应"的又一种形状）
            RefreshOrderReadouts();
            RefreshExclusiveFields();

            var chart = target as XChartBase;
            if (chart == null) return;

            string signature = ComputeSignature(ElementsProp());
            if (signature != m_ElementSignature)
            {
                m_ElementSignature = signature;
                chart.RebuildElements();
            }
            else
            {
                chart.ReapplyElements();
                chart.ForceRefresh();
            }
            Repaint();
        }

        private static Label MakeReadonly(string text) => new Label(text);

        private static Button MakeButton(string text, Action clicked)
            => new Button(clicked) { text = text };

        // ── 自述报表（与 chart 那边共用同一对文件名）────────────────

        private static void SetDumpFlag(bool on)
        {
            var root = XChartBase.ProjectRoot();
            if (root == null) return;
            var path = Path.Combine(root, XChartBase.DumpFlagName);
            if (on) File.WriteAllText(path, DateTime.Now.ToString("u"));
            else if (File.Exists(path)) File.Delete(path);
            Debug.Log(DumpFlagDetail());   // 完整状态走日志：面板塞不下、也复制不了
        }

        private static void OpenDumpFile()
        {
            var root = XChartBase.ProjectRoot();
            if (root == null) return;
            var path = Path.Combine(root, XChartBase.DumpFileName);
            if (!File.Exists(path))
            {
                Debug.LogWarning("XChart 报表还没生成：先按“自述报表 开”，再让图表跑几帧。");
                return;
            }
            EditorUtility.OpenWithDefaultApp(path);
        }

        /// <summary>
        /// 面板只留一句话，**完整说明进 Console**；每个 key 只打一次——面板会随数据重建，
        /// 不按 key 去重就会刷屏（这也是"说明太长、面板塞不下、又没法复制"的统一出口）。
        /// </summary>
        private static readonly HashSet<string> s_LoggedOnce = new HashSet<string>();

        private static void LogDetailOnce(string key, string detail)
        {
            if (!s_LoggedOnce.Add(key)) return;
            Debug.Log(detail);
        }

        private static string DumpFlagSummary()
        {
            // 面板上只留一句话；**路径、字节数这类长文本走日志**（面板里看不全、也没法复制）
            var root = XChartBase.ProjectRoot();
            if (root == null) return "报表：拿不到工程根";
            return "报表：" + (File.Exists(Path.Combine(root, XChartBase.DumpFlagName)) ? "开" : "关");
        }

        /// <summary>完整状态（含输出路径与文件大小）：抱怨不进面板，直接进 Console 让人能读能复制</summary>
        private static string DumpFlagDetail()
        {
            var root = XChartBase.ProjectRoot();
            if (root == null) return "XChart 报表：拿不到工程根";
            var on = File.Exists(Path.Combine(root, XChartBase.DumpFlagName));
            var file = Path.Combine(root, XChartBase.DumpFileName);
            return "XChart 报表：" + (on ? "开" : "关") + "；输出 " + file
                + (File.Exists(file) ? "（" + new FileInfo(file).Length + " 字节）" : "（还没生成）");
        }

        /// <summary>
        /// 把"面板渲染了几行 / 表里有几条"追加进同一份报表：这两个数不等就说明行不是被数据拿走、
        /// 而是面板自己读不到（验收时"元素行整批不见"正是这一类）。按 0.4 秒节流，别在重绘里刷盘。
        /// </summary>
        private void WritePanelWitness()
        {
            var root = XChartBase.ProjectRoot();
            if (root == null) return;
            if (!File.Exists(Path.Combine(root, XChartBase.DumpFlagName))) return;
            if ((float)EditorApplication.timeSinceStartup < m_NextWitnessWrite) return;
            m_NextWitnessWrite = (float)EditorApplication.timeSinceStartup + 0.4f;

            try
            {
                File.AppendAllText(Path.Combine(root, XChartBase.DumpFileName),
                    "  panel: rows=" + m_LastPanelRows + " table=" + m_LastPanelTableSize
                    + (m_LastPanelRows == m_LastPanelTableSize ? "" : "  ROWS-DISAGREE")
                    + " groups=" + m_Groups.Count + " deepest=" + m_ExclusiveKeys.Count
                    + " depth=" + m_LastMaxDepth + " hierarchy=" + m_HierarchyVerdict + "\n");
            }
            catch (Exception error)
            {
                m_NextWitnessWrite = float.MaxValue;
                Debug.LogWarning("XChart 面板证人写入失败，已停止：" + error.Message);
            }
        }

        /// <summary>owner 的直接子字段；owner 为 null 时取顶层字段。</summary>
        private IEnumerable<SerializedProperty> DirectChildren(SerializedProperty owner)
        {
            var result = new List<SerializedProperty>();
            if (owner != null)
            {
                SerializedProperty it = owner.Copy();
                int depth = it.depth;
                if (!it.Next(true)) return result;
                do
                {
                    if (it.depth <= depth) break;
                    if (it.depth == depth + 1) result.Add(it.Copy());
                }
                while (it.Next(false));
                return result;
            }

            SerializedProperty top = serializedObject.GetIterator();
            bool enterFirst = true;
            while (top.NextVisible(enterFirst))
            {
                enterFirst = false;
                if (top.depth != 0) continue;
                result.Add(top.Copy());
            }
            return result;
        }

        // ── [Header] 反射 ─────────────────────────────────────────

        private static string HeaderFor(Type type, string fieldName)
        {
            FieldInfo field = FindField(type, fieldName);
            if (field == null) return null;
            var attribute = field.GetCustomAttribute<HeaderAttribute>();
            return attribute == null ? null : attribute.header;
        }

        private static FieldInfo FindField(Type type, string name)
        {
            for (Type t = type; t != null; t = t.BaseType)
            {
                var field = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (field != null) return field;
            }
            return null;
        }

        // ── 元素增删移（结构性操作才重建面板） ────────────────────

        private void ShowAddElementMenu()
        {
            if (target as XChartBase == null) return;

            var entries = XChartElementRegistry.GetEntries();
            var menu = new GenericMenu();
            if (entries.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("没有带 [XChartElement] 的配置类型"));
            }
            else
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    menu.AddItem(new GUIContent(entry.Meta.DisplayName), false, () => AddElement(entry.ConfigType));
                }
            }
            menu.DropDown(new Rect(m_Root.worldBound.position, Vector2.zero));
        }

        private void AddElement(Type configType)
        {
            var config = XChartElementRegistry.Create(configType);
            if (config == null) return;

            Undo.RecordObject(target, "添加图表元素");
            serializedObject.Update();
            var list = ElementsProp();
            int index = list.arraySize;
            list.InsertArrayElementAtIndex(index);
            list.GetArrayElementAtIndex(index).managedReferenceValue = config;
            serializedObject.ApplyModifiedProperties();

            AfterStructureChange();
        }

        private void RemoveElement(int index)
        {
            Undo.RecordObject(target, "删除图表元素");
            serializedObject.Update();
            ElementsProp().DeleteArrayElementAtIndex(index);
            serializedObject.ApplyModifiedProperties();
            AfterStructureChange();
        }

        private void MoveElement(int index, int delta)
        {
            serializedObject.Update();
            var list = ElementsProp();
            int to = index + delta;
            if (to < 0 || to >= list.arraySize) return;

            Undo.RecordObject(target, "调整图表元素顺序");
            list.MoveArrayElement(index, to);
            serializedObject.ApplyModifiedProperties();
            AfterStructureChange();
        }

        private void AfterStructureChange()
        {
            // 结构一变就把**绘制序按表内位置重排**（他 2026-10-25 的政策：位置就是层序，§24.5）
            serializedObject.Update();
            RenumberDrawOrder();
            m_ElementSignature = ComputeSignature(ElementsProp());
            var chart = target as XChartBase;
            if (chart != null) chart.RebuildElements();
            RebuildTree();
            Repaint();
        }

        /// <summary>
        /// 把每个元素的**绘制序**重写成它在表里的位置（0,1,2…）：位置更靠后＝绘得更晚＝盖在上面。
        /// 他只调位置（↑↓），不再有第二个手填的绘制序号——两条口径指向不同事实时，
        /// "上移之后绘制顺序变没变"就没人回答得了（§24.5）。
        /// </summary>
        private void RenumberDrawOrder()
        {
            var list = ElementsProp();
            if (list == null) return;

            bool changed = false;
            for (int i = 0; i < list.arraySize; i++)
            {
                var drawOrder = list.GetArrayElementAtIndex(i).FindPropertyRelative("m_DrawOrder");
                if (drawOrder == null || drawOrder.intValue == i) continue;
                drawOrder.intValue = i;
                changed = true;
            }
            if (changed) serializedObject.ApplyModifiedProperties();
        }

        private static string ComputeSignature(SerializedProperty list)
        {
            if (list == null) return string.Empty;
            var builder = new StringBuilder();
            for (int i = 0; i < list.arraySize; i++)
            {
                var config = list.GetArrayElementAtIndex(i).managedReferenceValue as XChartElementConfig;
                if (config == null) { builder.Append("<null>|"); continue; }
                builder.Append(config.GetType().Name).Append(':')
                    .Append(config.Enabled ? '1' : '0').Append(':')
                    .Append(config.OrderIndex).Append(':')
                    .Append(config.ElementName).Append('|');
            }
            return builder.ToString();
        }
    }
}
