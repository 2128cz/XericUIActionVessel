using System;
using System.Collections.Generic;
using UnityEngine;

namespace XericUI.XChart
{
    /// <summary>
    /// 元素注册标记：凡是带它的 <see cref="XChartElementConfig"/> 派生类，都会被编辑器的
    /// "添加元素"菜单列出来（<paramref name="id"/> 是稳定标识，改名不改 id）。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class XChartElementAttribute : Attribute
    {
        /// <summary>稳定标识（序列化无关，供菜单/查找用）</summary>
        public string Id { get; }

        /// <summary>Inspector 菜单里显示的名字</summary>
        public string DisplayName { get; }

        /// <summary>菜单排序</summary>
        public int MenuOrder { get; }

        public XChartElementAttribute(string id, string displayName, int menuOrder = 0)
        {
            Id = id;
            DisplayName = string.IsNullOrEmpty(displayName) ? id : displayName;
            MenuOrder = menuOrder;
        }
    }

    /// <summary>
    /// 元素配置基类——<b>元素的作者数据</b>。
    ///
    /// <para><b>它同时是三样东西</b>：</para>
    /// <list type="number">
    /// <item>图表的序列化内容：由图表用 <c>[SerializeReference]</c> 列表持有，所以每个元素
    /// 自带自己的配置，图表组件不必再为每个元素声明一堆字段并逐个 Sync。</item>
    /// <item>Inspector 上的界面：Unity 对 <c>[SerializeReference]</c> 的支持本身就是"接口协议"——
    /// 列表里每个元素按实际类型展开、显示它自己的字段，派生类不需要写自定义 Editor。</item>
    /// <item>元素的工厂与装配点：<see cref="CreateTool"/> 造元素实例，<see cref="ApplyTo"/>
    /// 把配置灌进去。轴线、网格背景、以后任何东西都走这一条路。</item>
    /// </list>
    ///
    /// <para><b>方向是单向的</b>：配置是作者数据，元素是运行时实例。所以主流程只走
    /// 配置 -> 元素（<see cref="ApplyTo"/>）；<see cref="CaptureFrom"/> 只给编辑器做
    /// "把当前元素状态收回配置"的可选操作，运行时不需要。</para>
    /// </summary>
    [Serializable]
    public abstract class XChartElementConfig
    {
        [SerializeField] private bool m_Enabled = true;

        /// <summary>
        /// 注册序：谁先建、谁先更新（§11.4 的"注册 order"）。<b>它不再是绘制序</b>——
        /// 想让某个元素"先算但后画"（比如轴线的线画在点上面）用 <see cref="DrawOrder"/>。
        /// </summary>
        [SerializeField] private int m_OrderIndex;

        /// <summary>绘制序（画家序）：小者先画、大者盖在上面；只在同一分组内比较（分组见 XChartToolCategory）</summary>
        [SerializeField] private int m_DrawOrder;

        /// <summary>是否参与加载（关掉后元素不创建，但配置留在图表上）</summary>
        public bool Enabled
        {
            get => m_Enabled;
            set => m_Enabled = value;
        }

        /// <summary>注册序（越小越先建、越先更新；与绘制序分开）</summary>
        public int OrderIndex
        {
            get => m_OrderIndex;
            set => m_OrderIndex = value;
        }

        /// <summary>绘制序（越小越先画；同分组内比较）</summary>
        public int DrawOrder
        {
            get => m_DrawOrder;
            set => m_DrawOrder = value;
        }

        /// <summary>元素在 ToolsRoot 下的名字，默认取类型名</summary>
        public virtual string ElementName => GetType().Name;

        /// <summary>
        /// 造一个元素实例。基类拿不到具体类型，所以由每个配置自己 new 出对应的元素；
        /// 这里不接触 ToolsManager，也不做初始化（初始化由图表按统一流程完成）。
        /// </summary>
        public abstract XChartToolBase CreateTool();

        /// <summary>
        /// 把配置灌进元素。调用时机：元素已 <c>AddTool</c> 完成（子节点/渲染器都已建好）。
        /// </summary>
        public abstract void ApplyTo(XChartToolBase tool);

        /// <summary>从元素回读当前状态（编辑器里的"拾取"，可选实现）</summary>
        public virtual void CaptureFrom(XChartToolBase tool) { }

        /// <summary>
        /// 这个元素要<b>几个数值维</b>才画得出来（0＝无所谓，比如几何是常量的元素）。
        ///
        /// <para>面板拿它跟表里现有的数值维数对一下印出来：向量箭头要 6 维，而表里只有 x/y/z 三列时它
        /// <b>一条都不画</b>——那时画面上什么都没有，报表里也只是几个 0，人只能猜
        /// （他 2026-10-25："我把它添加进来了，之后它并没有显示出来，我现在是有默认数据在里面的"）。
        /// 需求是多少、现有是多少，两个数一起印才分得清"没数据"和"数据形状不对"。</para>
        /// </summary>
        public virtual int RequiredNumericDimensions => 0;

        /// <summary>
        /// 互斥显示：<paramref name="fieldName"/> 这个字段在当前配置下要不要从面板上<b>藏掉</b>。
        ///
        /// <para>他 2026-10-25 要的用法："我勾选'连续绘制'，配置里就失去'相对还是绝对'那个选项；
        /// 取消勾选，那个选项又回来"。判据写在配置自己身上（它读的是自己的字段），
        /// 面板只负责照着藏——不在面板里给某个元素写特例，也不做两套开关防止互相矛盾。</para>
        /// </summary>
        public virtual bool IsFieldHidden(string fieldName) => false;
    }

    /// <summary>
    /// 元素表的<b>纯算</b>部分（离线可验，不碰 Unity 对象）。
    /// </summary>
    public static class XChartElementTable
    {
        /// <summary>
        /// 表里第一份<b>这一类</b>（含派生）配置的下标，没有则 -1。
        ///
        /// <para><b>为什么单独拎出来</b>：默认元素按类型去重就靠它。图表初始化会跑很多遍
        /// （域重载、预制体开合、进 Play），<c>DeclareDefaultElements</c> 每遍都被问一次；
        /// 不去重的结果是"每重载一次多一个背景"——同一批格线画两遍、边越描越粗，
        /// 而他看到的只是"图好像越来越糊"。这类"多出来的那一份"必须在能算的地方算清，
        /// 而不是留在 MonoBehaviour 里等现场撞见。</para>
        /// </summary>
        public static int IndexOfKind(IReadOnlyList<XChartElementConfig> elements, Type kind)
        {
            if (elements == null || kind == null) return -1;
            for (int i = 0; i < elements.Count; i++)
            {
                // 空槽（被清掉的那一条）跳过，不算命中：它既不是这一类，也不该把这一类挡在外面
                var element = elements[i];
                if (element != null && kind.IsInstanceOfType(element)) return i;
            }
            return -1;
        }

        /// <summary>表里已经有这一类了吗（<see cref="IndexOfKind"/> 的是/否写法）</summary>
        public static bool HasKind(IReadOnlyList<XChartElementConfig> elements, Type kind)
            => IndexOfKind(elements, kind) >= 0;

        /// <summary>
        /// 这一类配置的<b>第一条</b>，没有则 null。
        /// "哪一条是这一类"只许有这一个答法：判重用下标、脚本入口用实例，两条查找各写一遍
        /// 迟早答得不同（本轮拆掉的正是"两处存同一件事"这一族）。
        /// </summary>
        public static XChartElementConfig FirstOfKind(IReadOnlyList<XChartElementConfig> elements, Type kind)
        {
            int at = IndexOfKind(elements, kind);
            return at >= 0 ? elements[at] : null;
        }
    }
}