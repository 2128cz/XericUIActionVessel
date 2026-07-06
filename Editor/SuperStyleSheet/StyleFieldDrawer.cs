using UnityEditor;
using UnityEngine;
using XericLibrary.Runtime.SuperStyleSheet;

namespace XericUIEditor
{
    /// <summary>
    /// StyleField 的自定义 Inspector 属性绘制器。
    /// 
    /// 默认折叠状态：仅显示样式路径下拉框。
    /// 展开状态：显示命名空间、路径、资源值类型（有值时）、注解描述（有注解时）。
    /// 无值/无注解的行不显示。
    /// </summary>
    [CustomPropertyDrawer(typeof(StyleField))]
    public class StyleFieldDrawer : PropertyDrawer
    {
        private const float LINE_HEIGHT = 18f;
        private const float PADDING = 2f;
        private const float FOLD_BUTTON_WIDTH = 14f;

        private const string PROP_NAMESPACE = "_namespace";
        private const string PROP_STYLEPATH = "_stylePath";

        /// <summary>折叠状态（按 property path 缓存）</summary>
        private static readonly System.Collections.Generic.Dictionary<string, bool> s_FoldoutStates
            = new System.Collections.Generic.Dictionary<string, bool>();

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var nsProp = property.FindPropertyRelative(PROP_NAMESPACE);
            var pathProp = property.FindPropertyRelative(PROP_STYLEPATH);
            if (nsProp == null || pathProp == null)
                return PADDING + (LINE_HEIGHT + PADDING);

            string key = GetFoldKey(property);
            bool expanded = s_FoldoutStates.TryGetValue(key, out bool f) && f;

            if (!expanded)
                return PADDING + (LINE_HEIGHT + PADDING);

            // 基础：命名空间 + 路径 = 2 行
            int rowCount = 2;

            // 有值才显示值类型行
            string ns = GetNamespace(property);
            string path = pathProp.stringValue;
            StyleValue val = StyleManager.GetValue(ns, path);
            if (val != null && val.ValueType != null)
                rowCount++;

            // 有注解才显示注解行
            var annotations = StyleManager.GetAnnotations(ns, path);
            if (annotations != null && annotations.Count > 0)
                rowCount++;

            return PADDING + (1 + rowCount) * (LINE_HEIGHT + PADDING);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            string key = GetFoldKey(property);
            bool expanded = s_FoldoutStates.TryGetValue(key, out bool fold) && fold;

            // 获取子属性
            SerializedProperty nsProp = property.FindPropertyRelative(PROP_NAMESPACE);
            SerializedProperty pathProp = property.FindPropertyRelative(PROP_STYLEPATH);

            if (nsProp == null || pathProp == null)
            {
                EditorGUI.LabelField(position, label != null ? label.text : "Style Field", " (未初始化)");
                return;
            }

            // === 标题行：折叠箭头 + 标签 + 路径下拉 ===
            Rect headerRect = new Rect(position.x, position.y + PADDING, position.width, LINE_HEIGHT);

            // 折叠按钮
            Rect foldRect = new Rect(headerRect.x, headerRect.y, FOLD_BUTTON_WIDTH, LINE_HEIGHT);
            bool newFold = EditorGUI.Foldout(foldRect, expanded, GUIContent.none, true);
            if (newFold != expanded)
            {
                s_FoldoutStates[key] = newFold;
                expanded = newFold;
            }

            // 标签
            Rect labelRect = new Rect(headerRect.x + FOLD_BUTTON_WIDTH, headerRect.y,
                EditorGUIUtility.labelWidth - FOLD_BUTTON_WIDTH, LINE_HEIGHT);
            EditorGUI.LabelField(labelRect, label);

            // 路径下拉框（始终可见）
            Rect pathRect = new Rect(headerRect.x + EditorGUIUtility.labelWidth + 4f, headerRect.y,
                headerRect.width - EditorGUIUtility.labelWidth - 8f, LINE_HEIGHT);

            string currentNs = GetNamespace(property);

            // 首次绘制时 xsss 可能尚未加载，当前命名空间若无效则自动切换
            EnsureValidNamespace(nsProp, ref currentNs);

            DrawPathDropdown(pathRect, pathProp, currentNs);

            if (!expanded)
                return;

            // === 展开区域 ===
            float y = headerRect.y + LINE_HEIGHT + PADDING;
            float indent = 16f;
            float fieldWidth = position.width - indent;

            // --- 命名空间 ---
            Rect nsLabelRect = new Rect(position.x + indent, y, 60f, LINE_HEIGHT);
            Rect nsFieldRect = new Rect(position.x + indent + 64f, y, fieldWidth - 64f, LINE_HEIGHT);
            EditorGUI.LabelField(nsLabelRect, "命名空间");
            DrawNamespaceDropdown(nsFieldRect, nsProp);
            y += LINE_HEIGHT + PADDING;

            // --- 路径 ---
            Rect pathLabelRect = new Rect(position.x + indent, y, 60f, LINE_HEIGHT);
            Rect pathFieldRect = new Rect(position.x + indent + 64f, y, fieldWidth - 64f, LINE_HEIGHT);
            EditorGUI.LabelField(pathLabelRect, "路径");
            DrawPathDropdown(pathFieldRect, pathProp, currentNs);
            y += LINE_HEIGHT + PADDING;

            // --- 资源值类型（仅在有值时显示） ---
            StyleValue currentValue = StyleManager.GetValue(currentNs, pathProp.stringValue);
            if (currentValue != null && currentValue.ValueType != null)
            {
                Rect valLabelRect = new Rect(position.x + indent, y, 80f, LINE_HEIGHT);
                Rect valFieldRect = new Rect(position.x + indent + 84f, y, fieldWidth - 84f, LINE_HEIGHT);

                EditorGUI.LabelField(valLabelRect, "值类型");

                string typeDisplay = GetShortTypeName(currentValue.ValueType);
                string valuePreview = currentValue.RawValue != null
                    ? currentValue.RawValue.ToString()
                    : "";

                EditorGUI.LabelField(valFieldRect, $"{typeDisplay}  =  {valuePreview}");
                y += LINE_HEIGHT + PADDING;
            }

            // --- 注解描述（仅在有注解时显示） ---
            var annotations = StyleManager.GetAnnotations(currentNs, pathProp.stringValue);
            if (annotations != null && annotations.Count > 0)
            {
                Rect annoLabelRect = new Rect(position.x + indent, y, 80f, LINE_HEIGHT);
                Rect annoFieldRect = new Rect(position.x + indent + 84f, y, fieldWidth - 84f, LINE_HEIGHT);

                EditorGUI.LabelField(annoLabelRect, "注解");

                // 优先显示 @Desc
                annotations.TryGetValue("Desc", out string desc);
                if (!string.IsNullOrEmpty(desc))
                    EditorGUI.LabelField(annoFieldRect, desc);
                else
                {
                    foreach (var a in annotations)
                    {
                        EditorGUI.LabelField(annoFieldRect, $"@{a.Key}: {a.Value}");
                        break;
                    }
                }
            }

            // 变更检测
            if (GUI.changed)
            {
                property.serializedObject.ApplyModifiedProperties();
            }
        }

        #region 下拉框绘制

        private void DrawNamespaceDropdown(Rect rect, SerializedProperty nsProp)
        {
            var namespaces = StyleManager.GetAvailableNamespaces();
            if (namespaces.Count == 0)
                namespaces.Add("default");

            int currentIndex = namespaces.IndexOf(nsProp.stringValue);
            if (currentIndex < 0) currentIndex = 0;

            int newIndex = EditorGUI.Popup(rect, currentIndex, namespaces.ToArray());
            if (newIndex >= 0 && newIndex < namespaces.Count)
            {
                nsProp.stringValue = namespaces[newIndex];
            }
        }

        private void DrawPathDropdown(Rect rect, SerializedProperty pathProp, string namespace_)
        {
            var paths = StyleManager.GetPathsForNamespace(namespace_);

            var displayOptions = new System.Collections.Generic.List<string>();
            if (paths.Count == 0)
            {
                displayOptions.Add("(无可用路径)");
            }
            else
            {
                foreach (var p in paths)
                {
                    StyleValue val = StyleManager.GetValue(namespace_, p);
                    string typeStr = val?.ValueType != null ? GetShortTypeName(val.ValueType) : "?";
                    string valStr = val?.RawValue != null ? val.RawValue.ToString() : "";
                    if (valStr.Length > 20) valStr = valStr.Substring(0, 17) + "...";
                    displayOptions.Add($"{p}  [{typeStr}] {valStr}");
                }
            }

            int currentIndex = System.Math.Max(0, paths.IndexOf(pathProp.stringValue));

            int newIndex = EditorGUI.Popup(rect, currentIndex, displayOptions.ToArray());
            if (newIndex >= 0 && newIndex < paths.Count)
            {
                pathProp.stringValue = paths[newIndex];
            }
        }

        #endregion

        #region 工具方法

        private static string GetFoldKey(SerializedProperty property)
        {
            return $"{property.serializedObject.targetObject.GetInstanceID()}_{property.propertyPath}";
        }

        private static string GetNamespace(SerializedProperty property)
        {
            var nsProp = property.FindPropertyRelative(PROP_NAMESPACE);
            string ns = nsProp?.stringValue;
            return string.IsNullOrEmpty(ns) ? "default" : ns;
        }

        private static string GetShortTypeName(System.Type type)
        {
            if (type == null) return "?";
            switch (type.Name)
            {
                case "Single": return "float";
                case "Int32": return "int";
                case "String": return "string";
                case "Boolean": return "bool";
                default: return type.Name;
            }
        }

        /// <summary>
        /// 若当前命名空间无可用路径（xsss 尚未加载），自动切换至首个有路径的命名空间。
        /// </summary>
        private static void EnsureValidNamespace(SerializedProperty nsProp, ref string currentNs)
        {
            var paths = StyleManager.GetPathsForNamespace(currentNs);
            if (paths.Count > 0)
                return;

            // 当前命名空间不可用，尝试其他命名空间
            var allNs = StyleManager.GetAvailableNamespaces();
            foreach (var ns in allNs)
            {
                if (ns == currentNs) continue;
                var altPaths = StyleManager.GetPathsForNamespace(ns);
                if (altPaths.Count > 0)
                {
                    currentNs = ns;
                    nsProp.stringValue = ns;
                    return;
                }
            }
        }

        #endregion
    }
}
