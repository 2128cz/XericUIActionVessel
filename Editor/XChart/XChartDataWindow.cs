using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace XericUI.XChart.Editor
{
    /// <summary>
    /// 数据窗口：浏览 / 导入 / 导出 / 复制粘贴图表数据（UI Toolkit + 行虚拟化）。
    ///
    /// <para><b>虚拟化落在 UI 层</b>：数据本身仍是 <see cref="XChartDataModel"/> 里那张内存记录表
    /// （没有分页、没有懒加载）。这里的虚拟化指"只有滚到可见范围的那几行才被绑定"——
    /// 窗口顶部会自报"已绑定次数 / 总行数"，一万条数据下这个次数应该停在几十，而不是一万。</para>
    ///
    /// <para><b>格式只有一份</b>：导出、导入、复制、粘贴全都走 <see cref="XChartDataText"/>，
    /// 所以"复制出来的文本"和"导出的文件"逐字节同源（规范 §3）。</para>
    /// </summary>
    public sealed class XChartDataWindow : EditorWindow
    {
        private XChartBase m_Chart;
        private ObjectField m_ChartField;
        private VisualElement m_TablePane;
        private ScrollView m_RangePane;
        private VisualElement m_RangeRows;
        private ToolbarToggle m_TableTab;
        private ToolbarToggle m_RangeTab;
        private VisualElement m_Header;
        private ListView m_List;
        private Label m_Stats;
        private TextField m_CapacityField;
        private Label m_BindReadout;
        private Label m_Message;
        private int m_BindCount;
        private int m_LastBoundIndex = -1;
        private int m_DimensionCount = -1;

        /// <summary>每一列对应的值下标（-1＝这一列不占值格）；表头与格子共用，规则来自 <see cref="XChartColumnMath"/></summary>
        private int[] m_ColumnValueSlots = new int[0];

        /// <summary>上面那组的签名：换一张同列数但种类不同的图时，旧行里的格子必须重造</summary>
        private string m_ColumnValueSignature = string.Empty;

        /// <summary>范围页是在哪一份值格分布上建的：对不上就整页重建（一列一行不能指错列）</summary>
        private string m_RangeSignature = string.Empty;
        private bool m_Applying;
        private bool m_ShowRangeTab;

        /// <summary>维度范围页里每个维度一行控件（重建只在列数变化时发生，改数只刷读数）</summary>
        private readonly List<DimensionRow> m_DimensionRows = new List<DimensionRow>();

        // 列宽：表头与行里的格子共用同一组常量，否则表头和内容会错位
        private const float kIndexWidth = 60f;
        private const float kTimeWidth = 110f;
        private const float kCellWidth = 100f;
        private const float kAddColumnWidth = 26f;
        private const float kDeleteWidth = 26f;
        private const float kOverrideFieldWidth = 90f;

        /// <summary>表页的操作帮助：只作 tooltip，不占界面（他 2026-10-25："描述性信息太多、语法不简洁"）</summary>
        private const string kListHint =
            "时间与数值列可直接改（回车或点开别处提交，改不动会说明原因并回填原值）；标签列只读。"
            + "末行「＋ 添加一行」加记录；行末「-」删这一行；表头「＋」加一列、「×」删一列"
            + "（删列连同整列的值一起删，动手前整表已复制到剪贴板）。"
            + "整表改法走「导出到剪贴板 → 改文本 → 从剪贴板替换」，导出的文件与剪贴板是同一份格式。";

        /// <summary>范围页的操作帮助：同样只作 tooltip</summary>
        private const string kRangeHint =
            "一列两行：标题是名字与值下标 [n]，第二行是这一列的真实统计；两端各一个开关带一个格子。"
            + "勾上才生效，没勾的那端跟着数据跑；钉住的那端会盖住自动值域。";

        [MenuItem("Tools/XChart/数据窗口")]
        private static void OpenFromMenu()
        {
            var window = GetWindow<XChartDataWindow>("XChart 数据");
            window.TryAdoptSelection();
            window.RebuildList();
        }

        /// <summary>供面板按钮调用："打开这张图的数据窗口"</summary>
        public static void OpenFor(XChartBase chart)
        {
            var window = GetWindow<XChartDataWindow>("XChart 数据");
            window.m_Chart = chart;
            window.BindChartField();
            window.RebuildList();
            window.Repaint();
        }

        private void TryAdoptSelection()
        {
            if (m_Chart != null) return;
            m_Chart = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponent<XChartBase>()
                : null;
        }

        private void OnEnable()
        {
            BuildUI();
        }

        private void OnFocus()
        {
            // 别处（面板/脚本）改了数据以后回到这个窗口要看到新的
            RebuildList();
        }

        private void BuildUI()
        {
            rootVisualElement.Clear();
            m_DimensionRows.Clear();

            // ── 顶部：动作（导出 / 导入 / 清空 / 测试 / 强制刷新）────────────────
            // 工具栏控件一律用 Toolbar* 那一族（ToolbarMenu / ToolbarButton / ToolbarToggle），
            // 它们自带工具栏的尺寸与配色，和窗口里其它按钮混用会显得是两种界面。
            // "图表"这个引用字段不在这里——它属于"我现在看的是哪张图"，归底部状态栏右侧。
            var toolbar = new Toolbar();
            toolbar.Add(MakeMenu("导出",
                ("到剪贴板", (Action)ExportToClipboard),
                ("到文件…", (Action)ExportToFile)));
            toolbar.Add(MakeMenu("导入",
                ("从剪贴板替换", () => ImportFromClipboard(true)),
                ("从剪贴板追加", () => ImportFromClipboard(false)),
                ("从文件替换…", () => ImportFromFile(true)),
                ("从文件追加…", () => ImportFromFile(false))));
            toolbar.Add(MakeMenu("清空", ("清空全部记录", (Action)ClearData)));
            toolbar.Add(MakeMenu("测试",
                ("螺旋 32（一圈正弦/余弦）", AppendTestSpiral),
                ("螺旋矢量场 32（7 维：坐标 + 色相/饱和/明度/不透明）", AppendTestVectorField),
                ("平方和金字塔 30（1+4+9+16）", AppendTestPyramid)));
            toolbar.Add(new ToolbarButton(ForceRefreshNow) { text = "强制刷新", tooltip = "改完数据直接重画一次图，不用回界面乱点" });

            var bindToggle = new ToolbarToggle { text = "绑定读数", value = false };
            bindToggle.RegisterValueChangedCallback(evt => m_BindReadout.style.display =
                evt.newValue ? DisplayStyle.Flex : DisplayStyle.None);
            toolbar.Add(bindToggle);
            rootVisualElement.Add(toolbar);

            m_BindReadout = new Label { style = { display = DisplayStyle.None } };
            rootVisualElement.Add(m_BindReadout);

            // ── 中部：两页（数据表 / 维度范围），同一时刻只显示一页 ──────────────
            var panes = new VisualElement { style = { flexGrow = 1 } };

            m_TablePane = new VisualElement { style = { flexGrow = 1 } };
            // 表头：<c>#</c>（记录下标，只读）/ 时间 / 各维度（就地改名 + 删列）/ ＋加一列 / 标签 / 删位
            m_Header = new VisualElement { style = { flexDirection = FlexDirection.Row, flexShrink = 0 } };
            m_TablePane.Add(m_Header);

            m_List = new ListView
            {
                fixedItemHeight = 20,
                selectionType = SelectionType.Single,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
            };
            m_List.style.flexGrow = 1;
            m_List.makeItem = MakeRow;
            m_List.bindItem = BindRow;
            m_List.tooltip = kListHint;
            m_TablePane.Add(m_List);

            m_RangePane = new ScrollView { style = { flexGrow = 1, display = DisplayStyle.None } };
            // 滚动内容一律 flexShrink=0：窗口矮了要能滚，而不是把最上面那一行压扁成"两行叠着画"
            // （他 2026-10-25 现场：第一列的统计行与开关行重叠，后面几列却正常——就是被挤掉高度的那一行）
            var rangeBody = new VisualElement { style = { flexShrink = 0 } };
            m_RangeRows = new VisualElement { style = { flexShrink = 0 }, tooltip = kRangeHint };
            rangeBody.Add(m_RangeRows);
            m_RangePane.Add(rangeBody);
            // 滚动容器自己的内容也要不许收缩：收缩会发生在"这一整页"上，压扁的就是最上面那一行
            m_RangePane.contentContainer.style.flexShrink = 0;
            panes.Add(m_TablePane);
            panes.Add(m_RangePane);
            rootVisualElement.Add(panes);

            // ── 标签条（在窗口下面，不在上面：表是主视图，切下去才看到范围页）──────
            var tabs = new Toolbar();
            m_TableTab = new ToolbarToggle { text = "数据表", value = true };
            m_RangeTab = new ToolbarToggle { text = "维度范围", value = false };
            m_TableTab.RegisterValueChangedCallback(evt => SelectTab(evt.newValue ? 0 : 1));
            m_RangeTab.RegisterValueChangedCallback(evt => SelectTab(evt.newValue ? 1 : 0));
            tabs.Add(m_TableTab);
            tabs.Add(m_RangeTab);
            rootVisualElement.Add(tabs);

            // ── 底部状态栏：计数 + 缓冲上限（就地可改）+ 图表引用（右侧）──────────
            var status = new Toolbar();
            m_Stats = new Label();
            status.Add(m_Stats);
            status.Add(new ToolbarSpacer { style = { flexGrow = 1 } });
            status.Add(new Label("缓冲"));
            m_CapacityField = new TextField { style = { width = 70 }, tooltip =
                "数据模型的缓冲上限：写入超过它才从最旧的记录开始丢；改小不会立刻裁剪，改大也找不回已经丢掉的" };
            HookCommit(m_CapacityField, CommitCapacity);   // 与单元格同一套提交时机（回车 / 失焦）
            status.Add(m_CapacityField);
            status.Add(new ToolbarSpacer { style = { width = 10f } });
            m_ChartField = new ObjectField("图表")
            {
                objectType = typeof(XChartBase),
                allowSceneObjects = true,
                style = { width = 240, flexShrink = 0 },
                tooltip = "这个窗表现在作用在哪张图上（改它等于换一个表）",
            };
            m_ChartField.RegisterValueChangedCallback(evt =>
            {
                m_Chart = evt.newValue as XChartBase;
                RebuildColumns();
                RebuildList();
            });
            status.Add(m_ChartField);
            rootVisualElement.Add(status);

            m_Message = new Label();
            rootVisualElement.Add(m_Message);

            BindChartField();
            RebuildColumns();
            RebuildList();
            SelectTab(m_ShowRangeTab ? 1 : 0);
        }

        /// <summary>
        /// 切页：两个 <see cref="ToolbarToggle"/> 当一个互斥标签条用（点哪个看哪个，
        /// 点当前这个就翻到另一页——两页的标签条不需要第三个状态）。
        /// </summary>
        private void SelectTab(int index)
        {
            bool range = index == 1;
            m_ShowRangeTab = range;
            m_TableTab.SetValueWithoutNotify(!range);
            m_RangeTab.SetValueWithoutNotify(range);
            m_TablePane.style.display = range ? DisplayStyle.None : DisplayStyle.Flex;
            m_RangePane.style.display = range ? DisplayStyle.Flex : DisplayStyle.None;
            if (range) RefreshDimensionRows();
        }

        /// <summary>
        /// 算出"每一列对应的值下标"与它的签名。<b>表头、行里的格子、范围页都读这一份</b>，
        /// 于是"这一列有没有值"这件事全窗口只有一个答案（口径来自 <see cref="XChartColumnMath"/>）。
        /// 签名而不是个数：两张表可以同样 3 列但"哪一列有值格"完全不同（换图时旧格子必须重造）。
        /// </summary>
        private void ComputeColumnSlots(XChartDataModel model)
        {
            int dimensions = model != null ? model.Dimensions.Count : 0;
            m_ColumnValueSlots = new int[dimensions];
            for (int i = 0; i < dimensions; i++)
                m_ColumnValueSlots[i] = XChartColumnMath.ValueSlot(model.Dimensions, i);

            var signature = new System.Text.StringBuilder();
            for (int i = 0; i < dimensions; i++) signature.Append(m_ColumnValueSlots[i]).Append('|');
            m_ColumnValueSignature = signature.ToString();
        }

        /// <summary>工具栏上的一个下拉：条目按顺序排，点哪个跑哪个</summary>
        private static ToolbarMenu MakeMenu(string text, params (string label, Action action)[] items)
        {
            var menu = new ToolbarMenu { text = text };
            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i];
                menu.menu.AppendAction(item.label, _ => item.action());
            }
            return menu;
        }

        private void BindChartField()
        {
            if (m_ChartField != null) m_ChartField.SetValueWithoutNotify(m_Chart);
        }

        /// <summary>
        /// 表头：<c>#</c>（记录下标，只读）/ 时间 / 每个维度（就地改名）/ ＋（加一列）/ 标签。
        /// 列宽与行里的格子共用同一组常量，否则表头和内容会错位。
        /// </summary>
        private void RebuildColumns()
        {
            if (m_Header == null) return;
            m_Header.Clear();

            // 每一列对应的**值下标**先算好（表头、格子、范围页都读这一份）
            var model = m_Chart != null ? m_Chart.DataModel : null;
            ComputeColumnSlots(model);
            int dimensions = model != null ? model.Dimensions.Count : 0;
            m_DimensionCount = dimensions;

            m_Header.Add(new Label("#")
            {
                style = { width = kIndexWidth },
                tooltip = "记录下标：0 = 最早，n-1 = 最新。所有工具都按这个顺序取数"
                    + "（圆柱就是按它两两分组：0-1、2-3…）。它不是数据，不可改。",
            });
            m_Header.Add(new Label("时间") { style = { width = kTimeWidth } });

            for (int i = 0; i < dimensions; i++)
            {
                int index = i;
                int valueSlot = m_ColumnValueSlots[i];
                var cell = new VisualElement { style = { flexDirection = FlexDirection.Row, width = kCellWidth } };

                var name = new TextField { style = { flexGrow = 1 } };
                name.SetValueWithoutNotify(ColumnName(model, index));
                name.tooltip = "列标题＝这一维的显示名；改它只改显示名，维度序号不动"
                    + (valueSlot < 0 ? "（这一列不占值格：种类不是 Number）" : string.Empty);
                name.RegisterCallback<KeyDownEvent>(evt =>
                {
                    if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter) return;
                    RenameColumn(index, name.value);
                    evt.StopPropagation();
                });
                name.RegisterCallback<FocusOutEvent>(_ => RenameColumn(index, name.value));
                cell.Add(name);

                cell.Add(new ToolbarButton(() => DeleteColumn(index))
                {
                    text = "×",
                    style = { width = 20 },
                    tooltip = "删掉这一列（连同它每一行的值）：后面的列号整体前移，"
                        + "绑在这些列上的工具要重新对一遍序号",
                });
                m_Header.Add(cell);
            }

            m_Header.Add(new ToolbarButton(AddColumn)
            {
                text = "+",
                style = { width = kAddColumnWidth },
                tooltip = "加一列（新增一个数值维，默认名 D<n>，随后可在这里改名）",
            });
            m_Header.Add(new Label("标签") { style = { flexGrow = 1 }, tooltip = "记录自带的标签（只读）" });
            // 行末"删"那一格的位置：表头留同样的宽度，否则标签列和删列对不齐
            m_Header.Add(new Label { style = { width = kDeleteWidth } });
        }

        private static string ColumnName(XChartDataModel model, int index)
        {
            if (model == null || index < 0 || index >= model.Dimensions.Count) return "-";
            var definition = model.Dimensions[index];
            if (definition == null) return "D" + index;
            return string.IsNullOrEmpty(definition.DisplayName) ? "D" + index : definition.DisplayName;
        }

        /// <summary>改一维的显示名（只动显示名，序号/键都不动）</summary>
        private void RenameColumn(int index, string displayName)
        {
            var model = m_Chart != null ? m_Chart.DataModel : null;
            if (model == null) return;

            if (!model.TryRenameDimension(index, displayName))
            {
                SetMessage("第 " + index + " 列改不了名（不能是空名，或下标越界）");
                RebuildColumns();   // 回填现况，别让控件显示一个没生效的名字
                return;
            }

            EditorUtility.SetDirty(m_Chart);
            m_Chart.ForceRefresh();
            RebuildColumns();
            SetMessage("第 " + index + " 列改名为「" + displayName.Trim() + "」");
        }

        /// <summary>加一列＝新增一个数值维；老记录的这一维会补 0（SetDimensions 会重排每行的值数组）</summary>
        private void AddColumn()
        {
            var model = m_Chart != null ? m_Chart.DataModel : null;
            if (model == null) { SetMessage("先指定一个图表"); return; }

            var dimensions = new List<XChartDimensionDefinition>(model.Dimensions);
            string key = "d" + dimensions.Count;
            dimensions.Add(new XChartDimensionDefinition(key, "D" + dimensions.Count, XChartDimensionKind.Number));
            model.SetDimensions(dimensions);
            EditorUtility.SetDirty(m_Chart);
            m_Chart.ForceRefresh();

            RebuildColumns();
            RebuildList();
            SetMessage("已加一列（" + key + "）：老记录这一维补 0");
        }

        /// <summary>
        /// 删一列＝删掉一个维度定义，连同每一行的那一格。
        /// <b>破坏性动作要看得见代价</b>：先把整表留在剪贴板（和"清空"同一个安全网），再要一次确认——
        /// 删掉的列不能从"改大缓冲上限"那种操作里找回来。
        /// </summary>
        private void DeleteColumn(int index)
        {
            var model = m_Chart != null ? m_Chart.DataModel : null;
            if (model == null) { SetMessage("先指定一个图表"); return; }
            if (index < 0 || index >= model.Dimensions.Count)
            {
                SetMessage("第 " + index + " 列已经不在表里了（先刷新一次）");
                RebuildColumns();
                return;
            }

            string name = ColumnName(model, index);
            EditorGUIUtility.systemCopyBuffer = XChartDataText.Export(model);
            if (!EditorUtility.DisplayDialog("删除第 " + index + " 列？",
                    "「" + name + "」连同它在 " + model.RecordCount + " 行里的值一起删掉。\n"
                    + "后面的列号整体前移：绑在这些列上的工具（轴的维度、圆柱的 X/Y/Z）要重新对一遍。\n"
                    + "整表已先复制到剪贴板，误删可以导回来。", "删除", "取消"))
            {
                SetMessage("已取消删列");
                return;
            }

            string reason;
            if (!model.TryRemoveDimension(index, out reason))
            {
                SetMessage("删不掉第 " + index + " 列：" + reason);
                return;
            }

            EditorUtility.SetDirty(m_Chart);
            m_Chart.ForceRefresh();
            RebuildColumns();
            RebuildList();
            SetMessage("已删掉第 " + index + " 列（" + name + "）：现在 " + model.Dimensions.Count
                + " 列，每行 " + model.NumericDimensionCount + " 个值");
        }

        /// <summary>强制重画一次图（改完数据不用回界面乱点）</summary>
        private void ForceRefreshNow()
        {
            if (!EnsureChart()) return;
            m_Chart.ForceRefresh();
            RebuildList();
            SetMessage("已强制刷新");
        }

        private void AppendTestSpiral() => AppendTest("螺旋", XChartDataGenerators.AppendSpiral, 32);

        private void AppendTestVectorField()
            => AppendTest("螺旋矢量场", XChartDataGenerators.AppendSpiralVectorField, 32, false);

        private void AppendTestPyramid() => AppendTest("平方和金字塔", XChartDataGenerators.AppendSquarePyramid, 4);

        /// <param name="needsDimensions">生成器是否要求表里**已经**有数值维。
        /// 矢量场那份自己会把列补出来（"点一下就能看到东西"才是测试数据的用处），所以传 false。</param>
        private void AppendTest(string label, Func<XChartDataModel, int, int> generator, int argument,
            bool needsDimensions = true)
        {
            if (!EnsureChart()) return;

            var model = m_Chart.DataModel;
            int dimensions = model.NumericDimensionCount;
            if (needsDimensions && dimensions <= 0)
            {
                SetMessage("这个数据模型没有数值维，先加一列");
                return;
            }

            int before = model.RecordCount;
            int accepted = generator(model, argument);
            m_Chart.ForceRefresh();
            RebuildList();
            SetMessage("已生成" + label + "：" + accepted + " 条（行数 " + before + " → " + model.RecordCount
                + "，时间戳接在最后一条之后）");
        }

        /// <summary>末行那个按钮：加一条记录（值全 0，时间戳 = 上一条 +1）</summary>
        private void AddRecord()
        {
            var model = m_Chart != null ? m_Chart.DataModel : null;
            if (model == null) { SetMessage("先指定一个图表"); return; }
            if (model.NumericDimensionCount <= 0) { SetMessage("这个数据模型没有数值维，先加一列"); return; }

            var values = new float[model.NumericDimensionCount];
            if (!XChartDataGenerators.TryAppend(model, values))
            {
                SetMessage("加不了这一行（时间戳必须非递减，或值不是有限数）");
                return;
            }

            EditorUtility.SetDirty(m_Chart);
            m_Chart.ForceRefresh();
            RebuildList();
            SetMessage("已添加一行（时间戳 = 上一条 +1，值全 0，可直接在行里改）");
        }

        private VisualElement MakeRow()
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };

            // 格子装在自己的容器里：末行的"＋ 添加一行"要和格子互斥显示，两种布局不能挤在同一层。
            // 格子的引用统一装在 RowWidgets 上（cells.userData）：按位置数格子会随列数错位，
            // 上一版就是这么数的，加一列之后"标签"那一格会去吃数值格。
            var cells = new VisualElement { name = "row-cells", style = { flexDirection = FlexDirection.Row, flexGrow = 1 } };
            var widgets = new RowWidgets();
            cells.userData = widgets;

            widgets.Index = new Label { style = { width = kIndexWidth } };
            cells.Add(widgets.Index);

            // 单元格就地编辑：时间与数值列是 TextField（回车或失焦提交），标签列只读。
            // userData 记"这一格是哪个槽"：-1＝时间列，>=0＝数值维下标。
            // 时间列的格子没有值下标：CommitCell 用"是不是 widgets.Time 这一格"来分派，
            // 不靠给 userData 塞 -1 这种两个含义撞在一个数上的写法（-1 已经被"不占值格"占了）
            widgets.Time = new TextField { style = { width = kTimeWidth } };
            HookCommit(widgets.Time, () => CommitCell(widgets.Time));
            cells.Add(widgets.Time);

            int dimensions = Mathf.Max(m_DimensionCount, 0);
            widgets.Values = new TextField[dimensions];
            widgets.Slots = m_ColumnValueSignature;
            for (int i = 0; i < dimensions; i++)
            {
                int valueSlot = i < m_ColumnValueSlots.Length ? m_ColumnValueSlots[i] : -1;
                var cell = new TextField { userData = valueSlot, style = { width = kCellWidth } };
                if (valueSlot < 0)
                {
                    // 非数值列：没有对应的值格。留一个禁用的格子而不是让人填进去再被拒——
                    // 他填过却存不进去，比一眼看出"这一格不归我改"更坏。
                    cell.SetEnabled(false);
                    cell.tooltip = "这一列的种类不是 Number ⇒ 它不占值格（值数组里没有它的位置）";
                }
                else HookCommit(cell, () => CommitCell(cell));
                widgets.Values[i] = cell;
                cells.Add(cell);
            }

            widgets.Labels = new Label { style = { flexGrow = 1 } };
            cells.Add(widgets.Labels);

            widgets.Delete = new ToolbarButton(() => DeleteRecord(widgets))
            {
                text = "-",   // 一个符号就够（他 2026-10-25："删除按钮应该改为 -，而不是一个'删'"）
                style = { width = kDeleteWidth },
                tooltip = "删掉这一行＝删掉这一条记录（后面的行号整体前移；记录表本来就按时间排，删一条不会打乱顺序）",
            };
            cells.Add(widgets.Delete);

            row.Add(cells);

            var add = new ToolbarButton(AddRecord)
            {
                name = "row-add",
                text = "＋ 添加一行",
                style = { display = DisplayStyle.None },
            };
            row.Add(add);
            return row;
        }

        /// <summary>
        /// 拿到这条行控件的引用集；<b>列数和当前不一致就整条重造</b>——
        /// 列表回收的是行控件本身，回收来的旧行还带着上一轮的格子数，直接用会少画一列。
        /// </summary>
        private RowWidgets EnsureWidgets(VisualElement element)
        {
            var existing = element.Q("row-cells");
            var widgets = existing != null ? existing.userData as RowWidgets : null;
            int want = Mathf.Max(m_DimensionCount, 0);
            // 列数一样但"哪一列有值格"变了（换了一张同列数、种类不同的图）：格子的可编辑性与
            // 值下标都记在格子上，只看列数会把这一列当成能改的，于是往错的列写数
            if (widgets != null && widgets.Values != null && widgets.Values.Length == want
                && widgets.Slots == m_ColumnValueSignature) return widgets;

            element.Clear();
            var fresh = MakeRow();
            for (int i = fresh.childCount - 1; i >= 0; i--)
            {
                var child = fresh[i];
                fresh.Remove(child);
                element.Insert(0, child);
            }

            var cells = element.Q("row-cells");
            return cells != null ? cells.userData as RowWidgets : null;
        }

        private void HookCommit(TextField field, Action commit)
        {
            field.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter) return;
                commit();
                evt.StopPropagation();
            });
            field.RegisterCallback<FocusOutEvent>(_ => commit());
        }

        private void BindRow(VisualElement element, int index)
        {
            m_BindCount++;
            m_LastBoundIndex = index;
            // 绑定期间控件会失焦/改值：这一段不许被当成用户编辑（否则回填会被写回数据）
            m_Applying = true;

            var model = m_Chart != null ? m_Chart.DataModel : null;
            int total = model != null ? model.RecordCount : 0;

            var widgets = EnsureWidgets(element);
            var add = element.Q("row-add");
            bool isAddRow = model != null && index >= total;

            var cells = element.Q("row-cells");
            if (cells != null) cells.style.display = isAddRow ? DisplayStyle.None : DisplayStyle.Flex;
            if (add != null) add.style.display = isAddRow ? DisplayStyle.Flex : DisplayStyle.None;

            if (widgets != null) widgets.RecordIndex = isAddRow || model == null ? -1 : index;
            if (widgets != null && !isAddRow) BindRowCore(widgets, index);

            m_Applying = false;
            UpdateStats();
        }

        /// <summary>
        /// 提交一格：解析失败/越界/时间倒序都<b>明确报因并把该行按模型现况回填</b>——
        /// 控件显示与数据不一致比改不动更坏。
        /// </summary>
        private void CommitCell(TextField field)
        {
            if (m_Applying || m_Chart == null) return;

            var cells = field.parent;
            var widgets = cells != null ? cells.userData as RowWidgets : null;
            if (widgets == null || widgets.RecordIndex < 0) return;
            int recordIndex = widgets.RecordIndex;
            bool isTime = ReferenceEquals(field, widgets.Time);
            int slot = field.userData is int value ? value : -1;
            if (!isTime && slot < 0) return;   // 非数值列的格子是禁用的，压根不该走到这里

            var model = m_Chart.DataModel;
            if (model == null || recordIndex < 0 || recordIndex >= model.RecordCount) return;

            double parsed;
            if (!double.TryParse(field.value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out parsed))
            {
                SetMessage("\"" + field.value + "\" 不是数字，已回填原值");
                RefreshRow(widgets, recordIndex);
                return;
            }

            string reason = null;
            bool ok = isTime
                ? model.TrySetTime(recordIndex, parsed, out reason)
                : model.TrySetValue(recordIndex, slot, parsed);

            if (!ok)
            {
                SetMessage("第 " + recordIndex + " 行改不动：" + (reason ?? "数值维下标越界，或值不是有限数"));
                RefreshRow(widgets, recordIndex);
                return;
            }

            m_Chart.ForceRefresh();
            SetMessage("第 " + recordIndex + " 行已改（" + (isTime ? "时间戳" : "第 " + slot + " 个数值维") + "）");
            RefreshDimensionRows();
        }

        private void RefreshRow(RowWidgets widgets, int recordIndex)
        {
            m_Applying = true;
            BindRowCore(widgets, recordIndex);
            m_Applying = false;
        }

        /// <summary>只按模型现况重填一行的<b>格子</b>（不计数、不动统计：回填用）</summary>
        private void BindRowCore(RowWidgets widgets, int index)
        {
            var model = m_Chart != null ? m_Chart.DataModel : null;
            if (model == null || widgets == null) return;
            if (index < 0 || index >= model.RecordCount) return;
            widgets.RecordIndex = index;

            widgets.Index.text = "#" + index;
            widgets.Time.SetValueWithoutNotify(model.GetTimeAt(index).ToString("0.###"));

            for (int d = 0; d < widgets.Values.Length; d++)
            {
                if (widgets.Values[d] == null) continue;
                // 格子的下标是**表里的列号**，值数组的下标是**第几个数值列**：中间夹一个非数值列时
                // 两者就差开了，所以这里问的是行控件建好时记下的那一对（widgets.Slots）。
                int slot = d < m_ColumnValueSlots.Length ? m_ColumnValueSlots[d] : -1;
                string text = slot >= 0 ? model.GetValueAt(index, slot).ToString("0.####") : "-";
                widgets.Values[d].SetValueWithoutNotify(text);
            }

            var record = model.Records[index];
            widgets.Labels.text = record.Labels.Count > 0 ? string.Join(", ", record.Labels) : string.Empty;
        }

        /// <summary>删一行＝删一条记录。删完立刻重排行号（列表多/少一行都要跟着改）</summary>
        private void DeleteRecord(RowWidgets widgets)
        {
            var model = m_Chart != null ? m_Chart.DataModel : null;
            if (model == null) { SetMessage("先指定一个图表"); return; }

            int index = widgets != null ? widgets.RecordIndex : -1;
            string reason;
            if (!model.TryRemoveRecord(index, out reason))
            {
                SetMessage("删不掉第 " + index + " 行：" + reason);
                RebuildList();   // 行号是从上一次绑定抄来的：先让它跟现况对齐
                return;
            }

            EditorUtility.SetDirty(m_Chart);
            m_Chart.ForceRefresh();
            RebuildList();
            SetMessage("已删掉第 " + index + " 行：现在 " + model.RecordCount + " 行"
                + "（后面每行的行号 −1；时间顺序不会因此变乱）");
        }

        private void UpdateStats()
        {
            var model = m_Chart != null ? m_Chart.DataModel : null;
            int total = model != null ? model.RecordCount : 0;
            int dimensions = model != null ? model.Dimensions.Count : 0;
            int capacity = model != null ? model.Capacity : 0;

            if (m_Stats != null)
            {
                m_Stats.text = "总行 " + total + "　维度 " + dimensions
                    + "　已丢弃 " + (model != null ? model.DroppedCount : 0);
            }

            // 缓冲上限的读数就在状态栏里（可改）：别处再放一份就会两处各说各话
            if (m_CapacityField != null && !m_Applying)
                m_CapacityField.SetValueWithoutNotify(capacity.ToString());

            if (m_BindReadout != null)
            {
                m_BindReadout.text = "已绑定次数 " + m_BindCount + "　最近绑定行 #" + m_LastBoundIndex
                    + "（虚拟化：这个次数应远小于总行数）";
            }
        }

        /// <summary>改缓冲上限（状态栏里的那个格子）</summary>
        private void CommitCapacity()
        {
            if (m_Applying || m_Chart == null) return;
            var model = m_Chart.DataModel;
            if (model == null) { UpdateStats(); return; }

            int parsed;
            if (!int.TryParse(m_CapacityField.value, out parsed) || parsed < 1)
            {
                UpdateStats();   // 回填现况
                SetMessage("缓冲上限要是个 ≥1 的整数，已回填原值");
                return;
            }

            if (parsed == model.Capacity) return;
            model.Capacity = parsed;
            EditorUtility.SetDirty(m_Chart);
            UpdateStats();
            SetMessage("缓冲上限 = " + model.Capacity + "（只影响之后的写入：到顶才从最旧的开始丢；"
                + "改小不会立刻裁剪，改大也找不回已经丢掉的）");
        }

        private void RebuildList()
        {
            if (m_List == null) return;

            var model = m_Chart != null ? m_Chart.DataModel : null;
            int dimensions = model != null ? model.Dimensions.Count : 0;
            string previousSlots = m_ColumnValueSignature;
            ComputeColumnSlots(model);
            if (dimensions != m_DimensionCount || m_ColumnValueSignature != previousSlots)
            {
                m_DimensionCount = dimensions;
                m_List.makeItem = MakeRow;   // 列数变了：下一轮的可见行按新列数建
                RebuildColumns();            // 表头与格子共用列宽，必须一起重建
            }

            // 范围页一列一行：判"行数和列数/值格分布对不对得上"而不是判"列数变没变"——
            // 第一次打开（列数从来没变过）和换一张同列数但种类不同的图都走得到这里，
            // 只盯"变化"的话这两处的范围页会是空的或指错列，而表看着完全正常。
            if (m_DimensionRows.Count != dimensions || m_RangeSignature != m_ColumnValueSignature)
                RebuildDimensionRows();

            // 行数 = 记录数 + 1：最后那一行专门放"＋ 添加一行"
            var items = new List<int>();
            int total = model != null ? model.RecordCount : 0;
            for (int i = 0; i <= total; i++) items.Add(i);
            m_List.itemsSource = items;
            m_List.Rebuild();

            m_BindCount = 0;
            m_LastBoundIndex = -1;
            UpdateStats();
            RefreshDimensionRows();   // 统计与生效范围是数据的函数：行数变了它们也变
            SetMessage("已加载 " + total + " 行");
        }

        private void ExportToClipboard()
        {
            if (!EnsureChart()) return;
            EditorGUIUtility.systemCopyBuffer = XChartDataText.Export(m_Chart.DataModel);
            SetMessage("已复制到剪贴板：" + m_Chart.DataModel.RecordCount + " 条记录");
        }

        private void ImportFromClipboard(bool replace)
        {
            if (!EnsureChart()) return;
            ApplyText(EditorGUIUtility.systemCopyBuffer, replace, "剪贴板");
        }

        private void ExportToFile()
        {
            if (!EnsureChart()) return;
            string path = EditorUtility.SaveFilePanel("导出图表数据", string.Empty, "xchart-data.txt", "txt");
            if (string.IsNullOrEmpty(path)) return;

            File.WriteAllText(path, XChartDataText.Export(m_Chart.DataModel));
            SetMessage("已导出到 " + path);
        }

        private void ImportFromFile(bool replace)
        {
            if (!EnsureChart()) return;
            string path = EditorUtility.OpenFilePanel("导入图表数据", string.Empty, "txt");
            if (string.IsNullOrEmpty(path)) return;

            ApplyText(File.ReadAllText(path), replace, path);
        }

        private void ApplyText(string text, bool replace, string source)
        {
            if (string.IsNullOrEmpty(text))
            {
                SetMessage(source + " 里没有文本（先复制一份导出的数据？）");
                return;
            }

            if (replace)
            {
                // 替换是破坏性的：先把原文留一份在剪贴板，误操作还能捞回来
                EditorGUIUtility.systemCopyBuffer = XChartDataText.Export(m_Chart.DataModel);
                bool confirmed = EditorUtility.DisplayDialog("替换全部数据？",
                    "这会用 " + source + " 里的内容替换掉当前全部记录。\n"
                    + "当前数据已先复制到剪贴板（误操作可粘贴回来）。", "替换", "取消");
                if (!confirmed)
                {
                    SetMessage("已取消替换");
                    return;
                }
            }

            string error;
            if (!XChartDataText.TryImport(text, m_Chart.DataModel, replace, out error))
            {
                SetMessage("导入失败（没有改动任何数据）：" + error);
                return;
            }

            m_Chart.ForceRefresh();
            RebuildList();
            SetMessage("导入成功（" + (replace ? "替换" : "追加") + "）：现在 " + m_Chart.DataModel.RecordCount + " 条");
        }

        private void ClearData()
        {
            if (!EnsureChart()) return;
            if (m_Chart.DataModel.RecordCount == 0)
            {
                SetMessage("本来就没有数据");
                return;
            }

            EditorGUIUtility.systemCopyBuffer = XChartDataText.Export(m_Chart.DataModel);
            if (!EditorUtility.DisplayDialog("清空数据？", "清空 " + m_Chart.DataModel.RecordCount
                + " 条记录。\n当前数据已先复制到剪贴板。", "清空", "取消"))
            {
                SetMessage("已取消清空");
                return;
            }

            m_Chart.ResetData();
            m_Chart.ForceRefresh();
            RebuildList();
            SetMessage("已清空");
        }

        // ── 维度范围页：一列一行（统计只读 + 两端各自可钉）──────────────────────

        /// <summary>
        /// 建范围页的控件。<b>只在列数变化时重建</b>：格子/勾选框每敲一下都会发 change，
        /// 重建会把正在输入的控件换掉（面板那边踩过同一次坑）。
        /// </summary>
        private void RebuildDimensionRows()
        {
            if (m_RangeRows == null) return;
            m_RangeRows.Clear();
            m_DimensionRows.Clear();

            var model = m_Chart != null ? m_Chart.DataModel : null;
            int dimensions = model != null ? model.Dimensions.Count : 0;
            if (dimensions == 0)
            {
                m_RangeRows.Add(new Label("这个数据模型还没有列：回到「数据表」页，点表头的＋加一列")
                {
                    style = { marginLeft = 6, marginTop = 4 },
                });
                return;
            }

            for (int i = 0; i < dimensions; i++) m_RangeRows.Add(MakeDimensionRowBox(model, i));
            m_RangeSignature = m_ColumnValueSignature;
            RefreshDimensionRows();
        }

        private VisualElement MakeDimensionRowBox(XChartDataModel model, int index)
        {
            // Index＝表里的列号（标题、删列用它），Slot＝值数组里的那一格（统计、范围、覆盖用它）
            var row = new DimensionRow
            {
                Index = index,
                Slot = model != null ? XChartColumnMath.ValueSlot(model.Dimensions, index) : -1,
            };

            var box = new VisualElement
            {
                style =
                {
                    flexShrink = 0,
                    marginLeft = 4, marginRight = 4, marginTop = 4, marginBottom = 4,
                    paddingLeft = 6, paddingRight = 6, paddingTop = 4, paddingBottom = 4,
                    borderTopWidth = 1, borderBottomWidth = 1, borderLeftWidth = 1, borderRightWidth = 1,
                    borderTopColor = new StyleColor(new Color(0.32f, 0.32f, 0.32f, 1f)),
                    borderBottomColor = new StyleColor(new Color(0.32f, 0.32f, 0.32f, 1f)),
                    borderLeftColor = new StyleColor(new Color(0.32f, 0.32f, 0.32f, 1f)),
                    borderRightColor = new StyleColor(new Color(0.32f, 0.32f, 0.32f, 1f)),
                },
            };

            // 三行各自 flexShrink=0 + 不许换行：一行装不下就宁可截断，也不许和邻行叠在一起
            var head = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, flexShrink = 0 },
            };
            row.Title = new Label("维度 " + index) { style = { width = 120f, flexShrink = 0 } };
            row.Effective = new Label { style = { flexGrow = 1, whiteSpace = WhiteSpace.NoWrap } };
            head.Add(row.Title);
            head.Add(row.Effective);
            box.Add(head);

            row.Stats = new Label { style = { whiteSpace = WhiteSpace.NoWrap, flexShrink = 0 } };
            box.Add(row.Stats);

            var edit = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, flexShrink = 0 },
            };
            row.PinMin = new Toggle("钉住最小") { tooltip = "勾上：这一维的最小端就用格子里的数，不再跟着数据跑" };
            row.Min = new TextField
            {
                style = { width = kOverrideFieldWidth },
                tooltip = "最小端的覆盖值（默认 0）。没勾时这个数留着但不参与解析；勾上立刻生效",
            };
            row.PinMax = new Toggle("钉住最大") { tooltip = "勾上：这一维的最大端就用格子里的数，不再跟着数据跑" };
            row.Max = new TextField
            {
                style = { width = kOverrideFieldWidth },
                tooltip = "最大端的覆盖值（默认 0）。没勾时这个数留着但不参与解析；勾上立刻生效",
            };
            HookCommit(row.Min, () => CommitOverride(row));
            HookCommit(row.Max, () => CommitOverride(row));
            row.PinMin.RegisterValueChangedCallback(_ => CommitOverride(row));
            row.PinMax.RegisterValueChangedCallback(_ => CommitOverride(row));
            edit.Add(row.PinMin);
            edit.Add(row.Min);
            edit.Add(row.PinMax);
            edit.Add(row.Max);
            box.Add(edit);

            m_DimensionRows.Add(row);
            return box;
        }

        /// <summary>
        /// 按模型现况刷一遍范围页的读数。
        /// <b>只在范围页真的露出来时算</b>：统计要扫全表（中位数还要排一次），
        /// 在「数据表」页敲格子时算它，等于每敲一下都为一页看不见的东西扫一遍数据。
        /// </summary>
        private void RefreshDimensionRows()
        {
            if (!m_ShowRangeTab || m_DimensionRows.Count == 0) return;

            var model = m_Chart != null ? m_Chart.DataModel : null;
            const XChartAggregateMask mask = XChartAggregateMask.Count | XChartAggregateMask.Range
                | XChartAggregateMask.Mean | XChartAggregateMask.Median | XChartAggregateMask.Rms;

            m_Applying = true;   // 回填不许被当成用户编辑（与表里同一套护栏）
            for (int i = 0; i < m_DimensionRows.Count; i++)
            {
                DimensionRow row = m_DimensionRows[i];
                string column = model != null ? ColumnName(model, row.Index) : "已经不在这张表里了";

                if (model == null || row.Slot < 0)
                {
                    // 表里的一行可以是 Label/Time：它有自己的键与显示名，但值数组里没有它的格子，
                    // 所以它既没有可统计的样本，也没有可以钉住的范围（工具根本不会按数值读它）。
                    row.Title.text = column + " [-]";
                    row.Stats.text = "不占值格";
                    row.Effective.text = "种类不是 Number，工具不按数值读它";
                    SetOverrideControlsEnabled(row, false);
                    continue;
                }
                SetOverrideControlsEnabled(row, true);

                XChartDimensionDefinition definition;
                if (!model.TryGetDimension(row.Slot, out definition) || definition == null)
                {
                    row.Title.text = "已经不在这张表里了";
                    row.Stats.text = "-";
                    row.Effective.text = "-";
                    continue;
                }

                // [n] 就是值下标（行的**位置**已经是表里的列号）：两个号在全数值表上相同，
                // 夹一个非数值列才错开，所以只印一个真正被工具使用的那个数
                row.Title.text = column + " [" + row.Slot + "]";

                XChartAggregates aggregates = default;
                bool computed = model.TryGetAggregates(row.Slot, mask, out aggregates);
                row.Stats.text = computed ? StatsSummary(aggregates) : "统计：这一列还没有可统计的数值";

                var over = definition.DomainOverride;
                row.PinMin.SetValueWithoutNotify(over.HasMin);
                row.PinMax.SetValueWithoutNotify(over.HasMax);
                row.Min.SetValueWithoutNotify(definition.ManualRange.x.ToString("0.####"));
                row.Max.SetValueWithoutNotify(definition.ManualRange.y.ToString("0.####"));

                double min, max;
                bool drawable = XChartDomainMath.TryResolve(model, row.Slot, out min, out max);
                // 哪一端被钉住由下面那两个勾直接看得见，这里不再复述一遍（同一件事只说一次）
                row.Effective.text = drawable
                    ? "生效 " + Number(min) + " .. " + Number(max)
                    : "没有生效范围（没钉、没数据）";
            }
            m_Applying = false;
        }

        private static void SetOverrideControlsEnabled(DimensionRow row, bool enabled)
        {
            row.PinMin.SetEnabled(enabled);
            row.Min.SetEnabled(enabled);
            row.PinMax.SetEnabled(enabled);
            row.Max.SetEnabled(enabled);
        }

        /// <summary>
        /// 提交一行的覆盖：两个勾 + 两个数一起交（改任一个都交一次全套，
        /// 于是"勾了但格子还没填"这种半状态不会留在模型里）。
        /// </summary>
        private void CommitOverride(DimensionRow row)
        {
            if (m_Applying || m_Chart == null) return;
            var model = m_Chart.DataModel;
            if (model == null) return;
            if (row.Slot < 0) return;   // 不占值的列：控件本来就是禁用的，走到这里也只当没发生

            double min, max;
            if (!TryFinite(row.Min.value, out min))
            {
                SetMessage(ColumnName(model, row.Index) + " [" + row.Slot + "] 最小端「" + row.Min.value + "」不是有限数字，已回填");
                RefreshDimensionRows();
                return;
            }
            if (!TryFinite(row.Max.value, out max))
            {
                SetMessage(ColumnName(model, row.Index) + " [" + row.Slot + "] 最大端「" + row.Max.value + "」不是有限数字，已回填");
                RefreshDimensionRows();
                return;
            }

            if (!model.TrySetDomainOverride(row.Slot, row.PinMin.value, min, row.PinMax.value, max))
            {
                SetMessage(ColumnName(model, row.Index) + " [" + row.Slot + "] 写不进去：已经不在这张表里了");
                RebuildColumns();
                RebuildDimensionRows();
                return;
            }

            EditorUtility.SetDirty(m_Chart);
            m_Chart.ForceRefresh();
            RefreshDimensionRows();
            // 回执报的是**结果**（这一维现在按哪个范围画），不是"我改了什么"
            double appliedMin, appliedMax;
            SetMessage(ColumnName(model, row.Index) + " [" + row.Slot + "] "
                + (XChartDomainMath.TryResolve(model, row.Slot, out appliedMin, out appliedMax)
                    ? "生效 " + Number(appliedMin) + " .. " + Number(appliedMax)
                    : "没有生效范围"));
        }

        /// <summary>覆盖值的门口检查：非有限数一律不收（它会变成一个"画不出东西"的范围）</summary>
        private static bool TryFinite(string text, out double value)
        {
            if (!double.TryParse(text, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out value)) return false;
            if (double.IsNaN(value) || double.IsInfinity(value)) { value = 0.0; return false; }
            return true;
        }

        /// <summary>统计那一行：只列数，不写"统计："这类前缀（界面串要短，他 2026-10-25）</summary>
        private static string StatsSummary(XChartAggregates aggregates)
        {
            if (aggregates.Count == 0) return "样本 0";
            return "样本 " + aggregates.Count + "　最小 " + Number(aggregates.Minimum)
                + "　最大 " + Number(aggregates.Maximum) + "　平均 " + Number(aggregates.Mean)
                + "　中位 " + Number(aggregates.Median) + "　RMS " + Number(aggregates.Rms);
        }

        private static string Number(double value)
        {
            if (double.IsNaN(value)) return "—";
            if (double.IsInfinity(value)) return value > 0 ? "+∞" : "-∞";
            return value.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
        }

        private bool EnsureChart()
        {
            if (m_Chart != null) return true;
            SetMessage("先指定一个图表（底部状态栏右侧的“图表”字段，或选中场景里的图表再打开窗口）");
            return false;
        }

        private void SetMessage(string text)
        {
            if (m_Message != null) m_Message.text = text;
        }

        /// <summary>
        /// 一条表行的控件引用。<b>按引用取而不是按下标取</b>：行的格子数随列数变，
        /// 用 `cells[childCount - 1]` 那种算法在加一列之后会把"标签"格当成数值格去填。
        /// </summary>
        private sealed class RowWidgets
        {
            public int RecordIndex = -1;
            public Label Index;
            public TextField Time;
            public TextField[] Values;

            /// <summary>建这一行时"哪一列有值格"的签名；对不上就整条重造（格子是禁用的还是可编辑的都记在格子上）</summary>
            public string Slots;
            public Label Labels;
            public ToolbarButton Delete;
        }

        /// <summary>范围页里一列的那一行控件</summary>
        private sealed class DimensionRow
        {
            public int Index;

            /// <summary>这一列在值数组里的那一格；-1＝这一列不占值（种类不是 Number），没有统计也没有范围可钉</summary>
            public int Slot = -1;
            public Label Title;
            public Label Effective;
            public Label Stats;
            public Toggle PinMin;
            public TextField Min;
            public Toggle PinMax;
            public TextField Max;
        }
    }
}