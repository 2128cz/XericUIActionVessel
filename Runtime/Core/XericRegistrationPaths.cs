namespace XericUI.Core
{
    /// <summary>
    /// 注册路径总表：本插件里所有菜单 / 组件注册路径的<b>唯一出处</b>。
    ///
    /// <para><b>为什么要有它</b>：路径以前散落在各文件的字面量里（<c>"Xeric UI Vessel/…"</c>、
    /// <c>"Xeric/XChart/…"</c>、<c>"XericLibrary/…"</c> 三套风格并存），改名要满地找。
    /// 现在统一成"<b>{根}/{产品族}/{产品名}</b>"，注册时只用 族常量 + 自己的名字。</para>
    ///
    /// <para><b>用法（属性参数必须是编译期常量，所以这里全是 const，靠拼接成串）</b></para>
    /// <code>
    ///   // 组件菜单（Component 菜单下，不需要 "Component/" 前缀）
    ///   [AddComponentMenu(XericRegistrationPaths.Chart + "/Mesh/Cube Mesh Background Tool")]
    ///
    ///   // 菜单栏
    ///   [MenuItem(XericRegistrationPaths.Chart + "/重新投影")]
    ///
    ///   // GameObject > … 创建菜单
    ///   [MenuItem(XericRegistrationPaths.ChartCreate + "/Scatter Chart", false, 12)]
    ///
    ///   // 组件上下文菜单（CONTEXT/&lt;组件类型&gt;/… 的前缀是绑定键，不能加根）
    ///   [MenuItem(XericRegistrationPaths.ContextPrefix + "Image/Replace To/Xeric Image", true)]
    /// </code>
    ///
    /// <para><b>新加产品时</b>：先用 <see cref="Root"/> + 你自己的族名拼一个族常量放在这里，
    /// 再去调用点用"族常量 + 产品名"。不要在任何别的地方写死路径字面量。</para>
    /// </summary>
    public static class XericRegistrationPaths
    {
        /// <summary>总根：所有注册路径的公共前缀</summary>
        public const string Root = "Xeric Library";

        // ── 产品族（菜单栏与组件菜单共用同一套前缀） ──────────────

        /// <summary>图表族（Chart / Line / Radar / Mesh / Pseudo3D …）</summary>
        public const string Chart = Root + "/Chart";
        /// <summary>表格族</summary>
        public const string Table = Root + "/Table";
        /// <summary>通用 UI 组件族（Image / Button / Toggle / Style* / Values …）</summary>
        public const string UI = Root + "/UI";
        /// <summary>布局族</summary>
        public const string Layout = Root + "/Layout";
        /// <summary>窗口（Form）族</summary>
        public const string Form = Root + "/Form";
        /// <summary>翻页族</summary>
        public const string FlipPage = Root + "/FlipPage";
        /// <summary>锚点窗口族</summary>
        public const string AnchorWindow = Root + "/AnchorWindow";

        // ── 其它菜单前缀 ──────────────────────────────────────────

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