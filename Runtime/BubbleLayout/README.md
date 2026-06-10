# BubbleLayout 气泡布局系统 — 操作手册

---

## 1. 架构总览

```
                        ┌─────────────────────────────┐
                        │     BubbleLayoutGroup        │
                        │  (宿主, 继承 LayoutGroup)     │
                        │                             │
                        │  扫描 GameObject 上的插件      │
                        │  管理 BubbleItemData 字典      │
                        │  调度管线执行顺序              │
                        └──────────┬──────────────────┘
                                   │
        ┌──────────────────────────┼──────────────────────────┐
        ▼                          ▼                          ▼
┌───────────────┐    ┌───────────────────────┐    ┌───────────────────────┐
│ 动画插件列表    │    │   物理积分 (内置)       │    │ 约束插件列表            │
│ IBubbleAnim-  │    │  BubblePhysicsSolver  │    │ IBubbleConstraint-    │
│ ationPlugin[] │    │  .Integrate()         │    │ Plugin[]              │
│               │    │                       │    │                       │
│ 施加力/速度    │    │ F→a→v→position        │    │ 限制位置/排列布局        │
└───────────────┘    └───────────────────────┘    └───────────┬───────────┘
                                                             │
                                                    ┌────────▼────────┐
                                                    │  气泡碰撞排挤     │
                                                    │  (内置, 四叉树)  │
                                                    └────────┬────────┘
                                                             ▼
                                                    ┌────────────────┐
                                                    │ ApplyPositions │
                                                    │  → RectTransform│
                                                    └────────────────┘
```

**每帧管线顺序（不可改变）：**

```
SyncItemList → 动画插件(按Order) → 物理积分 → 约束插件(按Order) → 碰撞排挤 → ApplyPositions
```

---

## 2. 核心数据类型：BubbleItemData

[`BubbleItemData.cs`](Runtime/BubbleLayout/BubbleItemData.cs) — 每个子 UI 元素的完整物理状态，插件通过它读写数据。

| 字段 | 类型 | 说明 |
|---|---|---|
| `rectTransform` | `RectTransform` | 关联的 UI 变换组件 |
| `Key` | `string` | 唯一标识 `{name}_{instanceID}` |
| `position` | `Vector2` | 当前动画位置 |
| `size` | `Vector2` | UI 尺寸 |
| `rotation` | `float` | 旋转角度(度) |
| `velocity` / `prevVelocity` | `Vector2` | 当前 / 上一帧速度 |
| `force` / `prevForce` | `Vector2` | 当前 / 上一帧力 |
| `momentum` | `Vector2` | 动量 |
| `angularMomentum` | `float` | 角动量 |
| `mass` | `float` | 质量(默认=1) |

> **关键约定**：每帧物理积分后会清零 `force`。插件需要在每帧重新施加力。

---

## 3. 如何编写插件

### 3.1 动画插件 (IBubbleAnimationPlugin)

动画插件在物理积分**之前**执行，用于施加力/速度/动量。

```csharp
using System.Collections.Generic;
using UnityEngine;
using XericUI.BubbleLayout;

[AddComponentMenu("Xeric UI Vessel/Layout/My Animation Plugin", 60)]
public class MyAnimationPlugin : MonoBehaviour, IBubbleAnimationPlugin
{
    // ── 必须实现：Enabled 和 Order ──
    [SerializeField] private bool m_Enabled = true;
    [SerializeField] private int m_Order = 5;
    public bool Enabled => m_Enabled;
    public int Order => m_Order;

    // ── 可自定义的参数 ──
    [SerializeField] private float m_WindStrength = 10f;

    // ── 核心方法：每帧调用，在此施加力 ──
    public void ProcessAnimation(List<BubbleItemData> items, float deltaTime)
    {
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];

            // 示例：施加一个向右的风力
            item.force += new Vector2(m_WindStrength, 0f);

            // 示例：根据速度施加速度阻尼
            // item.velocity *= 0.95f;
        }
    }
}
```

**要点：**
- 直接挂载到 `BubbleLayoutGroup` 所在 GameObject 上即可被自动发现
- `items` 是**所有**气泡元素的列表，遍历它来施加力
- 力通过 `+=` 累加（多个插件可同时施加力）
- `Order` 越小越先执行，建议范围 1~99

---

### 3.2 约束插件 (IBubbleConstraintPlugin)

约束插件在物理积分**之后**、碰撞排挤**之前**执行，用于排列布局或限制位置。

```csharp
using System.Collections.Generic;
using UnityEngine;
using XericUI.BubbleLayout;

[AddComponentMenu("Xeric UI Vessel/Layout/My Constraint Plugin", 61)]
public class MyConstraintPlugin : MonoBehaviour, IBubbleConstraintPlugin
{
    [SerializeField] private bool m_Enabled = true;
    [SerializeField] private int m_Order = 20;
    public bool Enabled => m_Enabled;
    public int Order => m_Order;

    [SerializeField] private float m_BoundaryRadius = 300f;
    [SerializeField] private float m_AttractionForce = 50f;

    public void ProcessConstraint(List<BubbleItemData> items, Vector2 layoutCenter)
    {
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];

            // 示例1：限制在圆形边界内
            Vector2 toCenter = layoutCenter - item.position;
            float dist = toCenter.magnitude;
            if (dist > m_BoundaryRadius)
            {
                Vector2 clampedPos = layoutCenter + toCenter.normalized * m_BoundaryRadius;
                // 方式A：直接修正位置（硬约束）
                item.position = clampedPos;
            }

            // 示例2：通过弹簧力吸附到目标位置（软约束，推荐）
            Vector2 targetPos = layoutCenter; // 目标位置
            Vector2 springForce = (targetPos - item.position) * m_AttractionForce;
            item.force += springForce;
        }
    }
}
```

**要点：**
- 可以直接修改 `item.position`（硬约束，即时生效）
- 也可以施加 `item.force`（软约束，下帧物理积分生效，有平滑动画过渡）
- `layoutCenter` 是 BubbleLayoutGroup 的 Rect 中心点
- 约束阶段在碰撞排挤之前，所以碰撞排挤会修正可能的重叠

---

### 3.3 IBubbleLayoutPlugin 接口参考

```csharp
public interface IBubbleLayoutPlugin
{
    bool Enabled { get; }  // Inspector 中可关闭
    int  Order    { get; }  // 优先级，值越大越靠后
}

public interface IBubbleAnimationPlugin : IBubbleLayoutPlugin
{
    void ProcessAnimation(List<BubbleItemData> items, float deltaTime);
}

public interface IBubbleConstraintPlugin : IBubbleLayoutPlugin
{
    void ProcessConstraint(List<BubbleItemData> items, Vector2 layoutCenter);
}
```

---

## 4. 内置插件一览

| 插件 | Order 建议 | 功能 |
|---|---|---|
| `VerticalLayoutConstraint` | 10 | 竖排排列，支持拉链模式(一左一右交替) |
| `HorizontalLayoutConstraint` | 10 | 横排排列，支持垂直对齐 |
| `CircularLayoutConstraint` | 10 | 环绕排列，支持自动/固定角度 |
| `BubbleCollisionConstraint` | 1000 | 独立碰撞排挤插件(可选，布局组件已内置) |

---

## 5. 如何着手了解源码

**推荐阅读顺序：**

1. **`IBubbleLayoutPlugin.cs`** — 接口定义，了解插件契约（~45行）
2. **`BubbleItemData.cs`** — 数据模型，了解插件操作的数据对象（~100行）
3. **`BubbleLayoutGroup.cs`** — 核心宿主，从 `RunLayoutPipeline()` 方法入手看管线调度（~700行）
4. **`BubblePhysicsSolver.cs`** — 物理积分器，`Integrate()` 方法（~100行）
5. **`QuadTree.cs`** — 四叉树空间索引，`Clear/Rebuild/Retrieve` 方法（~450行）
6. 任意内置插件（`VerticalLayoutConstraint.cs` 等）— 作为插件编写参考

**关键断点位置：**

| 方法 | 文件 | 作用 |
|---|---|---|
| `RunLayoutPipeline()` | `BubbleLayoutGroup.cs:223` | **管线入口**，从这里开始调试 |
| `SyncItemList()` | `BubbleLayoutGroup.cs:329` | 子元素增删改同步 |
| `RunAnimationPhase()` | `BubbleLayoutGroup.cs:457` | 动画插件调度 |
| `BubblePhysicsSolver.Integrate()` | `BubblePhysicsSolver.cs:18` | 力→速度→位置 |
| `RunConstraintPhase()` | `BubbleLayoutGroup.cs:470` | 约束插件调度 |
| `RunBubbleCollision()` | `BubbleLayoutGroup.cs:488` | 四叉树碰撞排挤 |

**定制流程参考：**

- 想修改**碰撞算法** → 看 `RunBubbleCollision()` + `QuadTree.cs`
- 想修改**物理模型** → 看 `BubblePhysicsSolver.Integrate()`
- 想修改**子元素管理** → 看 `SyncItemList()` + `RefreshChildList()`
- 想修改**插件发现机制** → 看 `RefreshPlugins()`
- 想添加**新的布局排列** → 参考 `VerticalLayoutConstraint.cs`，实现 `IBubbleConstraintPlugin`

---

## 6. BubbleLayoutGroup 可调参数

| 参数 | 默认值 | 说明 |
|---|---|---|
| `Enable Plugins` | true | 全局插件开关 |
| `Physics Damping` | 0.9 | 速度阻尼 (0=无阻尼, 1=完全停止) |
| `Physics Mass` | 1 | 默认质量 |
| `Mass Scale With Area` | false | 开启后质量 = 基础质量 × UI面积 |
| `Physics Delta Time` | 0 | 0=自动, >0=固定步长 |
| `Max Iterations` | 10 | 碰撞排挤最大迭代次数 |
| `Stiffness` | 1 | 排斥刚度 |
| `Min Separation` | 2 | 最小分离距离(像素) |
| `Iteration Damping` | 0.85 | 每轮迭代刚度递减系数 |
| `Layout Ignore Inactive` | true | 忽略未激活的子对象 |
