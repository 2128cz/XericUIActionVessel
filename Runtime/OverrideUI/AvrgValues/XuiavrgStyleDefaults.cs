using UnityEngine;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// Xuiavrg 样式默认值注入
    /// 
    /// 在应用启动时通过 StyleManager.AddDynamicStyle() 将 xuiavrg 各组件的
    /// 默认样式参数注入到 "default" 命名空间下。用户可在 .xsss 文件中覆写。
    /// 
    /// 触发时机：XuiavrgInspectorManager.OnEnable() 调用 EnsureDefaults()
    /// </summary>
    public static class XuiavrgStyleDefaults
    {
        private static bool _defaultsInjected;
        private const string NS = "default";

        /// <summary>确保 "default" 命名空间下有所有的 xuiavrg 默认样式值（幂等）</summary>
        public static void EnsureDefaults()
        {
            if (_defaultsInjected) return;
            _defaultsInjected = true;

            // ===== 基类通用 =====
            AddIfMissing(NS, "xuiavrg/base/stepValue",       1f);
            AddIfMissing(NS, "xuiavrg/base/readOnlyPrefix",  "[只读] ");
            AddIfMissing(NS, "xuiavrg/base/warningColor",    Color.yellow);

            // ===== Slider =====
            AddIfMissing(NS, "xuiavrg/slider/defaultStep",   0.1f);
            AddIfMissing(NS, "xuiavrg/slider/minStep",       0.001f);
            AddIfMissing(NS, "xuiavrg/slider/maxStep",       1000f);

            // ===== InputField =====
            AddIfMissing(NS, "xuiavrg/input/stepValue",      1f);

            // ===== Toggle =====
            AddIfMissing(NS, "xuiavrg/toggle/onColor",       Color.green);
            AddIfMissing(NS, "xuiavrg/toggle/offColor",      new Color(0.5f, 0.5f, 0.5f, 1f));

            // ===== Vector =====
            AddIfMissing(NS, "xuiavrg/vector/layoutDirection", "Horizontal");

            XericLibrary.Runtime.SuperStyleSheet.StyleManager.ForceRefresh();
        }

        private static void AddIfMissing(string ns, string path, object value)
        {
            if (XericLibrary.Runtime.SuperStyleSheet.StyleManager.HasStyle(ns, path)) return;
            XericLibrary.Runtime.SuperStyleSheet.StyleManager.AddDynamicStyle(ns, path, value);
        }
    }
}
