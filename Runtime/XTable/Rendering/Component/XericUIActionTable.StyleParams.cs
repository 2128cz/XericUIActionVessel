using TMPro;
using UnityEngine;
using XericLibrary.Runtime.SuperStyleSheet;

namespace XericUI.XTable.Rendering.Component
{
    /// <summary>
    /// 表格样式参数——统一管理所有样式路径与回退值。
    /// 修改此处的常量即可全局调整表格外观，无需在各方法中查找硬编码字符串。
    /// </summary>
    public partial class XericUIActionTable
    {
        #region 样式路径常量

        public const string STYLE_CELL_BG_COLOR           = "/cell/bg/color";
        public const string STYLE_CELL_FG_COLOR           = "/cell/fg/color";
        public const string STYLE_CELL_SELECTION_BG_COLOR = "/cell/selection/bg/color";

        public const string STYLE_CELL_BORDER_TOP_COLOR    = "/cell/border/top/color";
        public const string STYLE_CELL_BORDER_TOP_WIDTH    = "/cell/border/top/width";
        public const string STYLE_CELL_BORDER_BOTTOM_COLOR = "/cell/border/bottom/color";
        public const string STYLE_CELL_BORDER_BOTTOM_WIDTH = "/cell/border/bottom/width";
        public const string STYLE_CELL_BORDER_LEFT_COLOR   = "/cell/border/left/color";
        public const string STYLE_CELL_BORDER_LEFT_WIDTH   = "/cell/border/left/width";
        public const string STYLE_CELL_BORDER_RIGHT_COLOR  = "/cell/border/right/color";
        public const string STYLE_CELL_BORDER_RIGHT_WIDTH  = "/cell/border/right/width";

        public const string STYLE_CELL_FONT_SIZE      = "/cell/font/size";
        public const string STYLE_CELL_FONT_ALIGNMENT = "/cell/font/alignment";
        public const string STYLE_CELL_FONT_RICH_TEXT = "/cell/font/richText";
        public const string STYLE_CELL_FONT_ASSET     = "/cell/font/fontAsset";

        public const string STYLE_CELL_DEFAULT_HEIGHT = "/cell/default/height";
        public const string STYLE_CELL_DEFAULT_WIDTH  = "/cell/default/width";

        #endregion

        #region 回退值（GetStyleColor/GetStyleFloat/GetStyleInt 兜底参数）

        public static readonly Color BG_COLOR_FALLBACK           = new Color(0.18f, 0.18f, 0.18f, 1f);
        public static readonly Color FG_COLOR_FALLBACK           = new Color(0.05f, 0.05f, 0.05f);
        public static readonly Color SELECTION_COLOR_FALLBACK    = new Color(0.2f, 0.5f, 1f, 0.3f);
        public static readonly Color BORDER_COLOR_FALLBACK       = new Color(0.35f, 0.35f, 0.35f, 0.5f);
        public static readonly float BORDER_WIDTH_FALLBACK       = 1f;
        public static readonly int   FONT_SIZE_FALLBACK          = 14;

        #endregion

        #region 动态默认值（EnsureDefaultStyleNamespace 中 AddDynamicStyle 的初始值，匹配 DefaultTableStyles.xsss）

        public static readonly Color DEFAULT_BG_COLOR           = Color.white;
        public static readonly Color DEFAULT_FG_COLOR           = new Color(0.1f, 0.1f, 0.1f);
        public static readonly Color DEFAULT_BORDER_COLOR       = new Color(0.8f, 0.8f, 0.8f);
        public static readonly float DEFAULT_BORDER_WIDTH       = 1f;
        public static readonly Color DEFAULT_SELECTION_COLOR    = new Color(0.2f, 0.5f, 1f, 0.3f);
        public static readonly int   DEFAULT_FONT_SIZE          = 14;
        public const  string         DEFAULT_FONT_ALIGNMENT     = "MiddleCenter";
        public const  bool           DEFAULT_FONT_RICH_TEXT     = true;
        public const  float          DEFAULT_CELL_HEIGHT        = 40f;
        public const  float          DEFAULT_CELL_WIDTH         = 100f;

        #endregion

        #region 运行时硬编码色值

        public static readonly Color RT_SELECTION_COLOR      = new Color(0.18f, 0.42f, 0.82f, 0.25f);
        public static readonly Color RT_MULTI_SELECT_COLOR   = new Color(0.18f, 0.42f, 0.82f, 0.12f);
        public static readonly Color RT_FOCUS_COLOR          = new Color(0.22f, 0.55f, 0.95f, 0.35f);
        public static readonly Color RT_BORDER_COLOR         = new Color(0.35f, 0.35f, 0.35f, 0.5f);
        public const  float         RT_BORDER_WIDTH          = 1f;

        #endregion

        #region 便捷取值方法（封装路径 + 回退值）

        private Color GetCellBgColor(string ns) =>
            GetStyleColor(ns, STYLE_CELL_BG_COLOR, BG_COLOR_FALLBACK);

        private Color GetCellFgColor(string ns) =>
            GetStyleColor(ns, STYLE_CELL_FG_COLOR, FG_COLOR_FALLBACK);

        private Color GetCellSelectionBgColor(string ns) =>
            GetStyleColor(ns, STYLE_CELL_SELECTION_BG_COLOR, SELECTION_COLOR_FALLBACK);

        private Color GetCellBorderColor(string ns) =>
            GetStyleColor(ns, STYLE_CELL_BORDER_BOTTOM_COLOR, BORDER_COLOR_FALLBACK);

        private float GetCellBorderWidth(string ns) =>
            GetStyleFloat(ns, STYLE_CELL_BORDER_BOTTOM_WIDTH, BORDER_WIDTH_FALLBACK);

        private int GetCellFontSize(string ns) =>
            GetStyleInt(ns, STYLE_CELL_FONT_SIZE, FONT_SIZE_FALLBACK);

        private TMP_FontAsset GetCellFontAsset(string ns) =>
            GetStyleFontAsset(ns, STYLE_CELL_FONT_ASSET);

        #endregion
    }
}
