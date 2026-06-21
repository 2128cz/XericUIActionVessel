using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 界面属性编组
    /// 
    /// 将一种 C# 数据类型与一个 UI 预制体绑定，并归属于一个主题。
    /// 类似 Newtonsoft.Json 的 Converter 概念 — 针对特定 Type，指定特定的 UI 模板。
    /// 
    /// 同一类型可以有多个不同 uiName/主题的重载（如 "简单输入框" vs "带标签的输入框"）。
    /// Manager 根据当前主题名 + 字段 Tag 自动匹配对应的编组。
    /// 
    /// 全局注册表以 themeName 为键分组管理，OnEnable/OnDisable 自动注册/注销。
    /// </summary>
    [Serializable]
    [CreateAssetMenu(fileName = "Xeric UI avr Property group", menuName = "Xeric Library/UI Action Vessel/Reflection UI Gen/UI avr Property Group", order = 10)]
    public class XuiavrgPropertyGroupScriptableObject : ScriptableObject
    {
        #region 全局注册表

        /// <summary>
        /// 全局预制体组字典，以主题名称为键分组
        /// </summary>
        internal static Dictionary<string, List<XuiavrgPropertyGroupScriptableObject>> GlobalPrefabDict
            = new Dictionary<string, List<XuiavrgPropertyGroupScriptableObject>>();

        /// <summary>
        /// 根据主题名获取该主题下所有编组
        /// </summary>
        public static List<XuiavrgPropertyGroupScriptableObject> GetGroupsByTheme(string themeName)
        {
            if (string.IsNullOrEmpty(themeName))
                themeName = DefaultThemeName;

            GlobalPrefabDict.TryGetValue(themeName, out var list);
            return list ?? new List<XuiavrgPropertyGroupScriptableObject>();
        }

        /// <summary>
        /// 根据主题名 + 标记查找编组
        /// </summary>
        public static XuiavrgPropertyGroupScriptableObject FindGroup(string themeName, params XuiavrgFieldTag[] tags)
        {
            var groups = GetGroupsByTheme(themeName);
            return groups.FirstOrDefault(g => tags.All(t => g.supportedTags.Contains(t)));
        }

        /// <summary>
        /// 根据主题名 + UI 名称查找编组
        /// </summary>
        public static XuiavrgPropertyGroupScriptableObject FindGroupByUiName(string themeName, string uiName)
        {
            var groups = GetGroupsByTheme(themeName);
            return groups.FirstOrDefault(g => g.uiName == uiName);
        }

        /// <summary>
        /// 根据主题名 + 绑定类型查找所有匹配的编组
        /// </summary>
        public static List<XuiavrgPropertyGroupScriptableObject> FindGroupsByType(string themeName, string valueTypeName)
        {
            var groups = GetGroupsByTheme(themeName);
            return groups.Where(g => g.valueTypeName == valueTypeName).ToList();
        }

        /// <summary>
        /// 默认主题名称
        /// </summary>
        public const string DefaultThemeName = "Default";

        #endregion

        #region 属性字段

        /// <summary>
        /// 主题名称。同一主题下的 UI 组件共享统一风格。
        /// </summary>
        [Tooltip("主题名称。同一主题下的 UI 组件共享统一风格。Manager 根据其 currentTheme 查找对应编组")]
        public string themeName = DefaultThemeName;

        /// <summary>
        /// UI 个体名称。同一类型可有多个不同名称的重载。
        /// 在数据字段上通过 XuiavrgFieldPrefabOverride 按此名称指定。
        /// </summary>
        [Tooltip("UI 个体名称。用于 XuiavrgFieldPrefabOverride 按名指定，同一类型可有多个重载")]
        public string uiName = "Default";

        /// <summary>
        /// 此编组绑定的 C# 数据类型全名（如 System.Single、System.String）
        /// </summary>
        [Tooltip("绑定的 C# 数据类型全名，如 System.Single、System.Boolean")]
        public string valueTypeName = "System.Single";

        /// <summary>
        /// 解析后的类型缓存
        /// </summary>
        public Type ValueType
        {
            get
            {
                if (_valueType != null) return _valueType;
                if (!string.IsNullOrEmpty(valueTypeName))
                    _valueType = Type.GetType(valueTypeName);
                return _valueType;
            }
        }
        private Type _valueType;

        /// <summary>
        /// 所有程序集中类型名的缓存 — 简单名列表（如 "Boolean"、"Int32"、"String"）
        /// 与 _allTypeFullNames 下标一一对应
        /// </summary>
        private static List<string> _allTypeSimpleNames;

        /// <summary>
        /// 所有程序集中类型名的缓存 — 全名列表（如 "System.Boolean"、"System.Int32"）
        /// 与 _allTypeSimpleNames 下标一一对应
        /// </summary>
        private static List<string> _allTypeFullNames;

        /// <summary>
        /// 初始化类型名缓存 — 反射扫描所有已加载程序集中的类型
        /// </summary>
        private static void EnsureTypeCacheBuilt()
        {
            if (_allTypeFullNames != null) return;

            _allTypeSimpleNames = new List<string>();
            _allTypeFullNames = new List<string>();

            var seen = new HashSet<string>();

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in asm.GetTypes())
                    {
                        var fullName = type.FullName;
                        if (string.IsNullOrEmpty(fullName)) continue;
                        // 跳过泛型参数位置和编译器生成的类型
                        if (fullName.Contains("`") || fullName.Contains("<")) continue;

                        if (!seen.Add(fullName)) continue;

                        _allTypeSimpleNames.Add(type.Name);
                        _allTypeFullNames.Add(fullName);
                    }
                }
                catch
                {
                    // 某些程序集可能无法反射（如动态程序集），忽略
                }
            }
        }

        /// <summary>
        /// 尝试将用户输入的类型名纠正为标准全名。
        /// 通过 LevenshteinDistance 在所有已知类型中找到最匹配的名称。
        /// 如 "boolean" → "System.Boolean", "single" → "System.Single"。
        /// 如果已经是标准全名或无法匹配则返回原值（不做修改）。
        /// </summary>
        /// <param name="userInput">用户输入的类型名</param>
        /// <returns>纠正后的标准类型全名，无法识别时返回原值</returns>
        public static string NormalizeTypeName(string userInput)
        {
            if (string.IsNullOrWhiteSpace(userInput))
                return userInput;

            userInput = userInput.Trim();

            // 已经是标准全名（包含 "."），直接视为 OK，不做修改
            if (userInput.Contains('.'))
                return userInput;

            // 初始化类型缓存
            EnsureTypeCacheBuilt();

            // 通过 Levenshtein 距离在所有类型简单名中找到最匹配的
            var bestMatch = XericLibrary.Runtime.MacroLibrary.MacroMath.LevenshteinDistance(
                _allTypeSimpleNames, userInput,
                out int dis, out int index);

            // 阈值：距离超过输入长度 2 倍 + 2，认为不匹配，返回原值
            if (bestMatch == null || dis > userInput.Length * 2 + 2)
                return userInput;

            // 返回对应的全名
            return _allTypeFullNames[index];
        }

        /// <summary>
        /// 尝试将此实例的 valueTypeName 纠正为标准全名
        /// </summary>
        public void NormalizeValueTypeName()
        {
            valueTypeName = NormalizeTypeName(valueTypeName);
            _valueType = null; // 清空缓存，下次访问时重新解析
        }

        /// <summary>
        /// 对应的 UI 预制体
        /// </summary>
        [Tooltip("拖入 UI 预制体，系统会自动扫描其中的 IXuiavrgFieldTagProvider 组件")]
        public GameObject prefab;

        /// <summary>
        /// 此编组支持的 UI 功能标记集合
        /// </summary>
        [Tooltip("此编组包含的 UI 功能标记。Manager 根据字段 Tag 匹配对应编组")]
        public List<XuiavrgFieldTag> supportedTags = new List<XuiavrgFieldTag>();

        #endregion

        #region 组件自动识别（运行时 + Editor 共用）

        /// <summary>
        /// 从预制体中自动扫描到的 IXuiavrgFieldTagProvider 组件列表（非序列化，仅供 Editor 展示）
        /// </summary>
        [NonSerialized]
        public List<Component> detectedComponents = new List<Component>();

        /// <summary>
        /// 重新扫描预制体中的标记组件
        /// </summary>
        public void ScanPrefabComponents()
        {
            detectedComponents.Clear();
            if (prefab == null) return;

            var components = prefab.GetComponentsInChildren<Component>(true);
            foreach (var comp in components)
            {
                if (comp is IXuiavrgFieldTagProvider tagProvider && tagProvider.EntryTag != XuiavrgFieldTag.None)
                {
                    detectedComponents.Add(comp);
                }
            }

            // 自动同步 supportedTags
            HashSet<XuiavrgFieldTag> autoTags = new HashSet<XuiavrgFieldTag>();
            foreach (var comp in detectedComponents)
            {
                if (comp is IXuiavrgFieldTagProvider tp && tp.EntryTag != XuiavrgFieldTag.None)
                    autoTags.Add(tp.EntryTag);
            }

            foreach (var tag in autoTags)
            {
                if (!supportedTags.Contains(tag))
                    supportedTags.Add(tag);
            }
        }

        #endregion

        #region 生命周期

        void OnEnable()
        {
            RegisterToGlobalDict();
        }

        void OnDisable()
        {
            UnregisterFromGlobalDict();
        }

        private void RegisterToGlobalDict()
        {
            string key = string.IsNullOrEmpty(themeName) ? DefaultThemeName : themeName;
            if (!GlobalPrefabDict.TryGetValue(key, out var list))
            {
                list = new List<XuiavrgPropertyGroupScriptableObject>();
                GlobalPrefabDict[key] = list;
            }
            if (!list.Contains(this))
                list.Add(this);
        }

        private void UnregisterFromGlobalDict()
        {
            string key = string.IsNullOrEmpty(themeName) ? DefaultThemeName : themeName;
            if (GlobalPrefabDict.TryGetValue(key, out var list))
            {
                list.Remove(this);
                if (list.Count == 0)
                    GlobalPrefabDict.Remove(key);
            }
        }

        #endregion
    }
}
