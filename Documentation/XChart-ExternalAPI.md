# XChart 外部接入 API（快速上手）

> 这份文档只管一件事：**从外部（脚本／驱动／编辑器代码）操作一张 XChart 图**。
> 框架内部的规则与判据在 `XChart-RenderFramework.md`（下面引用的 § 都是它的章节号）。
> 适用于 2026-10-26 之后的代码（P20 自动置脏 / P21 数据折进盒子 / P22-P24 箭头画法与方向来源）。

## 0. 先记住三条入口

| 要改的东西 | 从哪儿进 | 什么时候生效 |
| --- | --- | --- |
| **数据**（记录、维、显示范围） | `chart.DataModel`（或 `chart.DataSource` 只读） | 版本自增，下一帧自己刷 |
| **元素的参数**（半径、颜色、宽度、方向来源…） | `chart.Tool<ChartToolCylinder>()` 等，**写公开属性即可** | setter 自己置脏，下一帧重算几何 |
| **元素的结构与作者数据**（有哪个元素、启用没、顺序、配置里的字段） | `chart.Elements` / `chart.Element<T>()` → 改完 `ReapplyElements()` 或 `RebuildElements()` | 那一次调用之后 |

一句必须记住的取舍（§27.3）：**配置是作者数据，工具是它的投影。**
从外部写进**工具**的值会活到下一次 `ReapplyElements()` / `RebuildElements()`
（面板动任一字段、增删/停用元素、脚本重载、进 Play、`ResetRender()` 都走那条），
那一次会把配置整份重灌进工具 ⇒ 外部的写被静默覆盖。**只要这一帧对，写工具；要活得过重建，写配置。**

## 1. 五分钟跑通一次数据驱动

```csharp
using XericUI.XChart;
using XericUI.XChart.Mesh;
using XericUI.XChart.Pseudo3D;

public void Drive()
{
    var chart = FindObjectOfType<XPseudo3DScatterChart>();   // 或你自己的派生图

    // ① 数据：先把表立起来，再逐条塞
    chart.DataModel.SetDimensions(new System.Collections.Generic.List<XChartDimensionDefinition>
    {
        new XChartDimensionDefinition("x", "X", XChartDimensionKind.Number),
        new XChartDimensionDefinition("y", "Y", XChartDimensionKind.Number),
        new XChartDimensionDefinition("z", "Z", XChartDimensionKind.Number),
    });
    chart.DataModel.TryAddRecord(0.0, new float[] { -100f, 0f, 50f });
    chart.DataModel.TryAddRecord(1.0, new float[] { -93.5f, 10.1f, 49.0f });

    // ② 元素参数：拿到实例，直接写属性——不用再叫 MarkDirty()
    var cylinder = chart.Tool<ChartToolCylinder>();
    if (cylinder != null) { cylinder.Radius = 0.4f; cylinder.ShowFill = false; }

    // ③ 空间：整张图的姿态（改它＝所有元素跟着重算投影）
    chart.Space.HalfEdge = 260f;
}
```

`Tool<T>()` / `Element<T>()` 都**答 null 而不抛**（`chart.ToolManager` 还没建时也一样）——
"图里现在有没有这一类"是现场状态，调用方要问得出"没有"这个答案。

## 2. 有哪些图可以挂

| 组件 | 是什么 | 备注 |
| --- | --- | --- |
| `XPseudo3DScatterChart` | 伪 3D 立方体图（散点 + 线框 + 网格 + 轴 + 圆柱 + 箭头） | 默认元素在 `DeclareDefaultElements()` 里声明（§22） |
| `XMeshPreviewChart` | 网格／矢量场预览（自带一份示例数据与三条轴） | 它的示例数据是"一条记录 6 维"形状，配箭头见 §5 |
| `XRadarChart` | 雷达图（另一条数据模型：`XChartRadarDataModel`） | 2D，不走立方体空间 |
| `XLineChart` | 时序折线 | 目前不注册元素工具（在办清单上） |
| 自己的派生类 | 继承 `XChartBase`，覆写 `OnChartRefresh` / `DeclareDefaultElements` | 元素表是唯一的内容来源 |

## 3. 入口 A：数据

`chart.DataModel`（`XChartDataModel`）是唯一的作者数据；下面这些**全部自带脏标记与版本自增**：

| API | 语义 | 失败时 |
| --- | --- | --- |
| `SetDimensions(list)` | 换整张表的列（会按新列重排已有记录的值） | — |
| `TryAddRecord(time, values, labels?)` | 追加一条（时间戳必须**非递减**） | `false`，原因在数据窗口/报表里看得见 |
| `TrySetValue(recordIndex, numericDim, value)` | 改一个数 | 越界／非有限 ⇒ `false` |
| `TrySetTime(recordIndex, time, out reason)` | 改时间戳（会破坏单调性时拒） | `false` + `reason` |
| `TrySetLabel` / `TryRemoveRecord` / `TryRemoveDimension` | 改标签、删行、删列 | `false` + `reason` |
| `TrySetDomainOverride(dim, overrideMin, min, overrideMax, max)` | 钉住某一维的显示范围（**分侧**可钉） | 越界列 ⇒ `false` |
| `Clear()` / `Capacity`（环形上限）/ `DroppedCount` | 清空、容量、被丢了多少条 | — |
| `Version` / `RecordCount` / `NumericDimensionCount` / `Snapshot` | 读数（工具靠 `Version` 判要不要重算） | — |

只读侧统一走 `IXChartDataSource`（`GetValueAt(record, dim)` 等），元素工具只认它——不要伸手去摸 `Records`。

## 4. 入口 B：元素的参数（setter 自己置脏）

每个元素工具的公开属性都是这个形状（P20 立的规矩，`dirty_gate` 数着：**133 个旋钮，133 个设了就生效**）：

```csharp
public float Radius { get => m_Radius; set { m_Radius = value; MarkDirty(); } }
```

- 所以外部**只管赋值**，不需要（也不该重复）调 `MarkDirty()`。`MarkDirty()` 还在，是给"改了但没走属性"的场景兜底用的。
- 取实例：`chart.Tool<T>()`；同类多条按登记名点：`chart.Tool<XChartToolBase>("Axis 2")`（名字就是 `chart.ElementNameFor(i)`、也就是面板元素行上那个）。
- 每个工具都实现了 `MarkDirty()`（基类是 **abstract**，不给静默空实现，§27.4）。

常用的几个：

| 工具 | 关键属性 |
| --- | --- |
| `ChartToolCylinder` | `Start/End/Radius/SegmentCount/StartNormal/EndNormal` · `DriveByData/DimensionX/Y/Z/GroupSize/MaxCylinders` · `ShowOutline/OutlineColor/OutlineThickness` · `ShowFill/FillColor` · `ShowCapCross/CrossColor/CrossThickness` · `ShowStartRing/ShowEndRing` · `GeneratrixCount/GeneratrixColor` · `Transform` |
| `ChartToolVectorArrows` | 见 §5 |
| `ChartToolMeshBg`（`BG Cube Grid`） | `FillFaces/FillColor/FillCenterColor` · `GridFaces/GridColor/GridThickness/GridDivisions/GridDivisionsS/T` · `GridDimensionS/T`（格线绑到哪一维） · `ShowFaceOutline/OnlyVisibleFaces/OutlineColor` |
| `ChartToolCubeBg`（`BG Cube Wire`） | `FrontEdgeColor/BackEdgeColor/EdgeThickness/ShowBackEdges` |
| `ChartToolScatterPoints` | `AutoFromData/DimensionX/Y/Z` · 外观 10 个（`PointSize/PointType/PointSizeMode/PointColor/CenterColor/BorderColor/BorderThickness/SideCount/ChamferSize/PointAngle`，走 `SyncShapeParams`） · `EnableColorMapping/ColorMapMin/ColorMapMax/ColorMapDimension` |
| `ChartToolAxis` | `SlotId`（**宿主**：板上现在发 `mesh.face.<0..5>` 与 `mesh.face.<f>.side.<0..3>` 共 30 个可当选；边号 0 底·1 左·2 顶·3 右，见规范 §31。下拉里不会再出现 `…grid.S/T`，那是「对齐格线」的输入） · `AlignToGrid/Direction/StartRatio/EndRatio/CrossRatio` · 刻度（`Dimension/Scale/AutoStep/ManualStep/TargetMajorCount/MinorTickCount`） · 线与标签（`LineColor/LineThickness/Major…/Minor…/LabelSource/LabelFormat/CustomLabels/Label…`） |

## 5. 矢量场（箭头）：方向从哪来、头贴在哪、尺寸怎么算

`ChartToolVectorArrows` 是"线 + 库的原生标记"，**没有填充几何**（§29）。四档方向来源决定支数和维数需求（§30）：

| `DirectionSource` | 起点 | 方向／第二量 | 支数 | 需要的数值维 |
| --- | --- | --- | --- | --- |
| `NextRecordAsVector`（旧 Absolute） | 第 i 条 0~2 | 第 i+1 条 0~2 | N−1 | 3 |
| `NextRecordAsPoint`（旧 Relative，**首尾相接**） | 第 i 条 0~2 | 第 i+1 条 0~2（作差） | N−1 | 3 |
| `ThisRecordCarriesVector`（**一条记录一个箭头**） | 第 i 条 0~2 | **同一条**的 3~5（原始矢量） | N | 6 |
| `AlternatingRecords`（**分段**：偶点奇向） | 偶数条 0~2 | 奇数条 0~2（原始矢量） | N÷2 | 3 |

```csharp
var arrows = chart.Tool<ChartToolVectorArrows>();
arrows.CoordinateDimension = 0;                                  // 坐标从第几维开始
arrows.DirectionSource = XChartArrowDirectionSource.AlternatingRecords;
arrows.DirectionScale = 0.25f;                                   // 每"数据单位"给多少立方体单位
arrows.MaxArrowCount = 512;
arrows.MarkerAnchor = 1f;                                        // 0＝尾部落点、0.5＝中心、1＝头尖落点
arrows.HeadLength = 10f; arrows.HeadWidth = 7f; arrows.ShaftWidth = 2f;
arrows.Continuous = false;                                       // true＝把所有点连成一条贝塞尔路径
arrows.HeadPosition = 1f;                                        // 连续绘制时头在路径上的位置（0~1）
arrows.PathSegments = 12;                                        // 每节细分几段
```

三条会直接影响画面的规矩：

- **坐标会按各维显示范围折进盒子**（±1＝盒子边界，§28）。你的原始值量级是 −100..50 没关系，会自动铺满盒子；
  某一维跨度为 0（一列同一个数）时**整个元素不画**，报表印 `dom=-`。
- **方向不折**（`ThisRecordCarriesVector` / `AlternatingRecords` 那两档）：方向是矢量，按位置范围折一次就把"朝哪"变成"在哪"。
- **头的尺寸吃投影**：平行于屏幕＝全额，指向屏幕里会缩到接近没有（§30.3）。这不是 bug，是"箭头是 3D 矢量"的正确表现。

颜色（可选，全在数值维上，§25.5）：`UseColorDimensions = true` 后紧跟坐标的 3~4 维依次是
**色相(0..256 色环，256 与 0 同色) / 饱和度(0..1) / 明度(0..1) / (不透明度 0..1)**；
起点由 `ColorDimension` 给——自带方向那档要设成 **6**，否则撞上方向列，工具会退回单色并印 `colors=overlap`。

## 6. 空间与视图（动它＝整张图跟着走）

```csharp
chart.Space.Rotation = someQuaternion;      // 唯一的旋转权威量（四元数）
chart.Space.HalfEdge = 260f;                // 半边长（像素）；立方体单位 → 像素的比例就在这里
chart.Space.SizeMode = XChartCubeSizeMode.HalfEdgeLength;
chart.Space.CenterOffset = new Vector2(0, 40f);
```

预设图上另有便捷口：`chart.CubeHalfSize`、`chart.Rotation`、`chart.ApplyViewPreset(XChartCubeViewPreset.Isometric)`、
`chart.CubeSizeMode`（它们写进的是元素表里那份 `XChartSpaceConfig`，所以能活过重建，§7）。

## 7. 入口 C：结构与配置（要活得过重建就改这里）

| 想做的事 | 怎么做 |
| --- | --- |
| 看有什么元素 | `chart.Elements`（只读表）、`chart.ElementNameFor(i)`、`chart.ElementTools` |
| 拿某一类的配置 | `chart.Element<XChartArrowConfig>()` |
| 让改动生效 | 只改字段值：`chart.ReapplyElements()`；增删/换类型/启停：`chart.RebuildElements()` |
| 面板加元素 | 图表 Inspector →「工具」→ Add；代码里派生图在 `DeclareDefaultElements()` 里 `AddElement` / `AddDefaultElement` |
| 启停一个元素 | `config.Enabled = true/false` 然后 `RebuildElements()`（停用＝不创建工具，配置仍留着） |
| 画面上谁盖谁 | **位置就是绘制序**（面板上 ↑↓）；`DrawOrder` 由位置算，`OrderIndex`（注册序）由类型定，**都不给你手填**（§24.5） |

> 配置类（`XChart*Config`）的字段目前仍是 `private [SerializeField]`：面板走 `SerializedProperty` 所以够用，
> **代码里直接写配置还没有公开属性**——这条在办（P22 之后的下一步）。眼下要"既立刻生效又活得过重建"，
> 就先写工具、再去面板动一下任一字段让它落盘，或把值存到你自己的脚本里在重建后重推一次。

## 8. 自查：改完没看见东西，按这张表读

| 读数（报表或面板） | 意思 |
| --- | --- |
| `dims=3/6` | 表里的数值维**不够**这个元素（面板上元素块也印同一对） ⇒ 加列或关掉颜色维 |
| `records=1`（而 `arrows=0`） | 记录不够凑一支（相邻两条那两档要 2 条） |
| `dom=-` | 有一维折不进盒子（该列跨度为 0 或不是有限数）⇒ 整元素不画 |
| `skipped=N` | N 个点读不出来（非有限值），少画了几支 |
| `path=none` | 曲线渲染器没建起来 ⇒ 箭头什么都画不出（箭头的唯一绘制出口就是它） |
| `colors=overlap` | 颜色维起点撞上坐标／方向列 ⇒ 实际画的是单色 |
| `arrows=31 arrowFaces=…`（旧版本）/ `markers=` | 提交了多少：与"看得见"是两件事，先跟上面几项对齐 |

开关报表：图表 Inspector 底部「自述报表 开/关」（= 工程根放一个 `xchart.dump.on`，每 0.4 秒追加一次，删掉就停）。
**编辑期要有重绘帧才会跑**（`[ExecuteAlways]`）：编辑器静止时外部写要等一帧——滚一下 Scene 视图即可。

## 9. 会咬人的九条

1. **外部写工具的值会被配置重灌覆盖**（§0 那条，最容易中的一次）。
2. **改配置字段名＝场景里的值静默丢**；只是移动类型/换 `see cref` 不丢。给配置加字段时优先加，不要改名。
3. **元素靠类型去重**：`AddDefaultElement<T>` 同类型只补一份；真要两份（两条轴）就 `AddElement` 并让名字不同名（面板会自动加序号，配对按 `ElementNameFor`）。
4. **"图里一定有一个空间工具"是不变量**：删掉/停用空间元素会被补回默认（删掉它≠图没了），要拿回旋钮就重新 Add 一条。
5. **箭头/圆柱的数据驱动模式要"几条记录凑一个图元"**：`GroupSize`（圆柱，默认 2）与箭头的四档来源都在说这件事——支数＝记录数的函数，不是记录数本身。
6. **65535 顶点上限**：填充/线网格按预算自动分块（`chunks=`/`fillMaxVerts=` 读数）；自己加图元时不要绕开预算。
7. **`UICurveRenderer.LiveVertexCount` 不是本实例的数**（报表里 `curveVerts=` 是"库里一共活着多少顶点"，P23 待办）；要判自己那份，读 `curves=`/`markers=`。
8. **雷达图与伪 3D 图是两套空间**：`XRadarChart` 用自己的 `XChartRadarDataModel`（2D 极坐标），不吃立方体的 `TryResolveDomain` 那套。
9. **别在元素自己的对象上做手脚**：工具与它生成的节点是运行时的（`HideAndDontSave`），重建一次就换一批——把状态存在你自己的组件或配置里。

## 10. 端到端：一个"掘进姿态"箭头

需求：每帧一条姿态（位置 + 朝向）。两种写法都能用，区别只在数据形状：

```csharp
// 写法 A：一条记录自带方向 —— 32 帧 = 32 支箭头（要 6 个数值维）
model.SetDimensions(new System.Collections.Generic.List<XChartDimensionDefinition>
{
    new XChartDimensionDefinition("x",  "X",  XChartDimensionKind.Number),
    new XChartDimensionDefinition("y",  "Y",  XChartDimensionKind.Number),
    new XChartDimensionDefinition("z",  "Z",  XChartDimensionKind.Number),
    new XChartDimensionDefinition("dx", "DX", XChartDimensionKind.Number),
    new XChartDimensionDefinition("dy", "DY", XChartDimensionKind.Number),
    new XChartDimensionDefinition("dz", "DZ", XChartDimensionKind.Number),
});
model.TryAddRecord(t, new[] { px, py, pz, heading.x, heading.y, heading.z });
arrows.DirectionSource = XChartArrowDirectionSource.ThisRecordCarriesVector;
arrows.DirectionScale = 4f;                        // 一支箭头画多长（立方体单位）
arrows.MarkerAnchor = 1f;                          // 头尖钉在那个姿态点上

// 写法 B：偶点奇向的分段 —— 每两支记录一支箭头（32 点 = 16 支）
model.TryAddRecord(t, pose);      model.TryAddRecord(t, poseDirection);
arrows.DirectionSource = XChartArrowDirectionSource.AlternatingRecords;

// 写法 C：只要轨迹 —— 连续绘制把每条记录连成一条路径，末端一个头
arrows.Continuous = true;  arrows.HeadPosition = 1f;
```

写法 A/B 的方向都是**原始矢量**（不折进盒子、只做长度倍率）；写法 C 是"一串点"，所以那里没有"相对/绝对"这一项（面板会自动藏）。
