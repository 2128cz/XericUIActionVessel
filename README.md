# Xeric UI Action Vessel

提供一些unity上常见UI容器用法。

## 容器类说明

### 泛型界面容器

使用`ObjectVessel<T>`来声名一个可以呈现在界面上的属性，其中，泛型可以用于在Unity界面上约束目标类型。  

> 这个类不建议进行封装，封装后会失去在Inspector界面上的泛型约束功能。  

### 翻页管理器

提供一个`FlipPageComponentManager`类，其继承于`XericFlipPageIndexVesselBase`。  
实现一种基于翻页逻辑的数据虚拟化结构管理器。  
父级实现了基础翻页逻辑，而子类实现在此基础上的组件交互逻辑，使用时，需要继承于`FlipPageComponentManager`再实现数据层功能。  
比如下面是一个通过翻页管理器控制Xchart.UITable内数据翻页的基础实例：
```
public class MeasuringPointPageManager : FlipPageComponentManager
{
    public UITable Table; // 表格呈现

    // 页面数据对象
    public class PageMeasuringPointInfo : PageComponentInfo
    {
        // 页面数据对象里包含多个内容数据对象，并会由于数据的增加自动增加页面。
    }
    // 内容数据对象
    public class ItemMeasuringPointInfo : ItemComponentInfo
    {
        public string RiskSourceName = "数据内容";
        // 单个数据条目内容的数据在此定义。
    }

    // 当翻页时，处理翻页数据
    public override void FlipPage(int pageIndex, bool forceRefresh = false)
    {
        base.FlipPage(pageIndex, forceRefresh);

        Table.ClearData();
        var page = Currentpage as PageMeasuringPointInfo;
        foreach (var item in page.itemInfos)
        {
            AddTableRowData(page.pageNumber, item as ItemMeasuringPointInfo);
        }
    }
    // 直接添加一个空数据对象到表格中
    [HorizontalGroup("数据"), Button("添加测试项目")]
    private void AddItem()
    {
        AddItem(new ItemMeasuringPointInfo(),
            () => AddPageLast(new PageMeasuringPointInfo()));
    }
    // 定义一下如何将数据处理为表格格式
    public void AddTableRowData(int pageOffset, ItemMeasuringPointInfo info)
    {
        Table.AddRow(
            (pageOffset + Table.data.Count).ToString(),
            info.RiskSourceName);
    }

}
```

## Shader 烘焙（Bake Shader As Image）

把 shader 离屏渲染成贴图，用来在场景或界面里当贴图 / Sprite 使用。

入口：`XericLibrary/Tool/Bake Shader As Image`

### 输入源

- **Shader**：按该 shader 造一张临时材质，属性表里改的就是这张副本，关掉窗口即丢。
- **Material**：复制一份材质再编辑，原资产不会被写脏；要落盘按「写回原材质」，否则按「丢弃改动」回到来源参数。

两者只能有一个，切换时另一个会被清空。属性表按 shader 声明的类型铺行（Range 滑块、Float / Int、`[Toggle]` 关键字、`[Enum]`、Color、Vector、贴图），每次刷新都会把值原样写回材质并同步关键字状态。

### 输出

尺寸预设或自定义、MSAA、HDR、sRGB 存储、翻转 Y、Pass 下标、背景色。导出支持 PNG / JPG / TGA / EXR，以及直接生成 Sprite 资产。

### 面片与 UV

Quad 网格里每一格对应一段 UV 区域，格号（UV X / Y）就是这段区域原点的整数部分；加行加列会按贴边那一格往外推算编号，「恢复 1×1」回到单片。也可以切到外部网格，把模型的某一层按平面投影烘出来。

- 「框住面片」把整个网格铺满输出，「选中格铺满」只框当前选中的那一格；
- 输出比例与面片比例不一致时按**内容比例**拉伸铺满，不留白边——一格就是一张图；
- 预览图上会叠分格线与每格 UV 编号，点选格子时右侧的 UV X / Y 跟着换。

### 实时预览与测试视图

工具栏「实时预览」打开后按编辑器心跳重画（聚焦 60fps、失焦 20fps）；关掉时参数改动只记成「待刷新」，写在「重新渲染」按钮上。

「测试视图」提供四种不依赖材质内容的探针图，用同一套面片 / 相机 / pass 重画：

- **UV**：红 = U、绿 = V，左下角纯黑即这一格的 UV 原点；
- **梯度**：灰度尺，用来量这一格占了画面多大；
- **棋盘**：方块变长条说明横竖缩放不等，变平行四边形说明斜切或透视；
- **纯色**：内置 `Unlit/Color` 画一张纯色面片，画得出来就说明管线是通的，问题在被测 shader 自己。

重复次数大于 1 时，UV 视图里一格仍是一条连续递增的尺子，整数 UV 处保留一条分界线，方便数这一格跨了几个 UV 单位。

### 全局参数

引擎在相机流程里才会写入的全局量（光照方向、环境光、反射探针等）在离屏路径里默认缺失。可在「全局参数」里扫描 shader 实际用到的条目，勾选后在渲染前注入、渲染后立即还原。

### 已知限制

- 属性表不列 `[HideInInspector]` 与 `[PerRendererData]` 属性，后者由渲染器逐实例提供，离屏渲染没有对应实例。
- 离屏渲染不跑引擎的相机流程，依赖 `_CameraDepthTexture`、屏幕空间后处理等相机内部数据的 shader 可能烘不出预期结果。
