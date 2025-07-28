# Xeric UI Action Vessel

提供一些unity上常见UI容器用法。

## 容器类说明

### 泛型界面容器

使用`ObjectVessel<T>`来声名一个可以呈现在界面上的属性，其中，泛型可以用于在Unity界面上约束目标类型。  

> 这个类不建议进行封装，封装后会失去在Inspector界面上的泛型约束功能。  

### 翻页管理器

提供一个`FlipPageComponentManager`类，其继承于`XericFlipPageIndexVesselBase`。  
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