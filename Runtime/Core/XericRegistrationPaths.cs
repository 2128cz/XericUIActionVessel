namespace XericUI.Core
{
    /// <summary>
    /// 注册路径总表：本插件里所有菜单 / 组件注册路径的<b>唯一出处</b>。
    ///
    /// <para><b>为什么要有它</b>：路径以前散落在各文件的字面量里（<c>"Xeric UI Vessel/…"</c>、
    /// <c>"Xeric/XChart/…"</c>、<c>"XericLibrary/…"</c> 三套风格并存），改名要满地找。
    /// 现在注册时只用 族常量 + 自己的名字。</para>
    ///
    /// <para><b>两套根只差一个空格</b>（<see cref="MenuBarRoot"/> / <see cref="ContextRoot"/>）：</para>
    /// <list type="bullet">
    ///   <item>顶部菜单栏（<c>[MenuItem]</c>）走 <c>XericLibrary/Action Vessel/…</c>，
    ///         品牌根 <b>不带空格</b>；族常量名后面加 <c>Bar</c>（<see cref="ChartBar"/> 等），
    ///         只有真挂了菜单项的族才有。</item>
    ///   <item>Hierarchy（<c>GameObject/…</c>）、Project（<c>CreateAssetMenu</c> 的 menuName）、
    ///         Add Component（<c>AddComponentMenu</c>）三个右键上下文面共用
    ///         <c>Xeric Library/Action Vessel/…</c>，即不带后缀的族常量本身。</item>
    /// </list>
    ///
    /// <para><b>用法（属性参数必须是编译期常量，所以这里全是 const，靠拼接成串）</b></para>
    /// <code>
    ///   // 组件菜单（Add Component 下，Unity 自己加 Component/ 前缀）
    ///   [AddComponentMenu(XericRegistrationPaths.Chart + "/Mesh/Cube Mesh Background Tool")]
    ///
    ///   // Project 右键建资产（menuName 是相对 Assets/Create 的路径）
    ///   [CreateAssetMenu(menuName = XericRegistrationPaths.Table + "/Table Config")]
    ///
    ///   // 顶部菜单栏
    ///   [MenuItem(XericRegistrationPaths.TableBar + "/Table Editor Override Layer")]
    ///
    ///   // GameObject &gt; … 创建菜单（Hierarchy 右键）
    ///   [MenuItem(XericRegistrationPaths.ChartCreate + "/Scatter Chart", false, 12)]
    ///
    ///   // 组件上下文菜单（CONTEXT/&lt;组件类型&gt;/… 的前缀是绑定键，不能加根）
    ///   [MenuItem(XericRegistrationPaths.ContextPrefix + "Image/Replace To/Xeric Image", true)]
    /// </code>
    ///
    /// <para><b>新加产品时</b>：先用 <see cref="ContextRoot"/> + 你的族名拼一个族常量放在这里
    /// （只有需要在顶部菜单栏挂项的族才另配一个 <c>*Bar</c>），再去调用点用"族常量 + 产品名"。
    /// 不要在任何别的地方写死路径字面量。</para>
    /// </summary>
    public static class XericRegistrationPaths
    {
        /// <summary> 顶部菜单栏根：品牌根不带空格 </summary>
        public const string MenuBarRoot = "XericLibrary/Action Vessel";

        /// <summary> Hierarchy / Project / Add Component 共用根：品牌根带空格 </summary>
        public const string ContextRoot = "Xeric Library/Action Vessel";

        // ── 上下文侧族常量（AddComponentMenu 与 CreateAssetMenu 用；GameObject 创建菜单也从这里长） ──

        /// <summary>图表族（Chart / Line / Radar / Mesh / Pseudo3D …）</summary>
        public const string Chart = ContextRoot + "/Chart";
        /// <summary>表格族</summary>
        public const string Table = ContextRoot + "/Table";
        /// <summary>通用 UI 组件族（Image / Button / Toggle / Style* / Values …）</summary>
        public const string UI = ContextRoot + "/UI";
        /// <summary>布局族</summary>
        public const string Layout = ContextRoot + "/Layout";
        /// <summary>窗口（Form）族</summary>
        public const string Form = ContextRoot + "/Form";
        /// <summary>翻页族</summary>
        public const string FlipPage = ContextRoot + "/FlipPage";
        /// <summary>锚点窗口族</summary>
        public const string AnchorWindow = ContextRoot + "/AnchorWindow";

        // ── 菜单栏侧（只给真挂了 [MenuItem] 的族） ────────────────

        /// <summary> 图表族的工具栏入口（数据窗口等） </summary>
        public const string ChartBar = MenuBarRoot + "/Chart";
        /// <summary> 表格族的工具栏入口（编辑器覆盖层开关） </summary>
        public const string TableBar = MenuBarRoot + "/Table";
        /// <summary> 通用 UI 族的工具栏入口（场景变形手柄开关） </summary>
        public const string UIBar = MenuBarRoot + "/UI";

        // ── Hierarchy 创建菜单 ────────────────────────────────────

        /// <summary>GameObject 菜单前缀：创建类菜单必须从这里开始</summary>
        public const string CreatePrefix = "GameObject/";

        /// <summary>GameObject 菜单下的族路径</summary>
        public const string ChartCreate = CreatePrefix + Chart;
        public const string TableCreate = CreatePrefix + Table;
        public const string UICreate = CreatePrefix + UI;

        /// <summary>组件上下文菜单前缀（后接 组件类型/叶子，例如 "Image/Replace To/Xeric Image"）</summary>
        public const string ContextPrefix = "CONTEXT/";
    }
}
