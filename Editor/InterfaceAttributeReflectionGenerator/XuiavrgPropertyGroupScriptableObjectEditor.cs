using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using XericUI.ReflectionUIGenerator;

namespace XericUIEditor.ReflectionUIGenerator
{
    [CustomEditor(typeof(XuiavrgPropertyGroupScriptableObject))]
    internal class XuiavrgPropertyGroupScriptableObjectEditor : Editor
    {
        SerializedProperty _themeNameProp;
        SerializedProperty _uiNameProp;
        SerializedProperty _valueTypeNameProp;
        SerializedProperty _prefabProp;
        SerializedProperty _supportedTagsProp;

        private XuiavrgPropertyGroupScriptableObject _target;
        private bool _showDetectedComponents = true;

        private void OnEnable()
        {
            _themeNameProp = serializedObject.FindProperty("themeName");
            _uiNameProp = serializedObject.FindProperty("uiName");
            _valueTypeNameProp = serializedObject.FindProperty("valueTypeName");
            _prefabProp = serializedObject.FindProperty("prefab");
            _supportedTagsProp = serializedObject.FindProperty("supportedTags");
            _target = target as XuiavrgPropertyGroupScriptableObject;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // 标题
            EditorGUILayout.LabelField("UI Property Group", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "将一个 C# 数据类型绑定到一个 UI 预制体，并归属到某个主题下。\n" +
                "Manager 根据当前主题 + 字段 Tag 自动匹配。",
                MessageType.Info);
            EditorGUILayout.Space();

            // 基本属性
            EditorGUILayout.PropertyField(_themeNameProp, new GUIContent("主题名称 (Theme)"));
            EditorGUILayout.PropertyField(_uiNameProp, new GUIContent("UI 名称 (uiName)"));

            // 绑定类型 — 带快捷选择 + 自动纠错
            DrawValueTypeField();

            EditorGUILayout.Space();

            // 预制体 + 自动扫描
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_prefabProp, new GUIContent("UI 预制体 (prefab)"));
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                if (_target.prefab != null)
                {
                    _target.ScanPrefabComponents();
                    EditorUtility.SetDirty(_target);
                }
            }

            EditorGUILayout.Space();

            // 支持的功能标记
            EditorGUILayout.PropertyField(_supportedTagsProp, new GUIContent("支持的功能标记 (supportedTags)"), true);

            EditorGUILayout.Space();

            // 组件审查区域
            _showDetectedComponents = EditorGUILayout.Foldout(_showDetectedComponents, "预制体组件审查", true);
            if (_showDetectedComponents)
            {
                EditorGUI.indentLevel++;

                if (_target.prefab == null)
                {
                    EditorGUILayout.HelpBox("请先拖入 UI 预制体，系统将自动扫描其中的 IXuiavrgFieldTagProvider 组件。", MessageType.Warning);
                }
                else if (_target.detectedComponents == null || _target.detectedComponents.Count == 0)
                {
                    if (GUILayout.Button("扫描预制体组件"))
                    {
                        _target.ScanPrefabComponents();
                        EditorUtility.SetDirty(_target);
                    }
                    if (_target.detectedComponents != null && _target.detectedComponents.Count == 0)
                        EditorGUILayout.HelpBox("未检测到 IXuiavrgFieldTagProvider 组件。请确保预制体上已挂载 XericUIrAttribute* 组件。", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.LabelField($"检测到 {_target.detectedComponents.Count} 个标记组件:", EditorStyles.boldLabel);

                    // 按 Tag 分组显示
                    var groups = _target.detectedComponents
                        .Where(c => c != null)
                        .GroupBy(c =>
                        {
                            if (c is IXuiavrgFieldTagProvider tp)
                                return $"{tp.EntryTag}" + (tp.TagIndex > 0 ? $" [{tp.TagIndex}]" : "");
                            return c.GetType().Name;
                        });

                    foreach (var group in groups)
                    {
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        EditorGUILayout.LabelField($"Tag: {group.Key}", EditorStyles.miniBoldLabel);
                        foreach (var comp in group)
                        {
                            using (new EditorGUI.DisabledScope(true))
                            {
                                EditorGUILayout.ObjectField(comp, typeof(Component), true);
                            }
                        }
                        EditorGUILayout.EndVertical();
                    }
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // 快捷操作按钮
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("重新扫描"))
            {
                _target.ScanPrefabComponents();
                EditorUtility.SetDirty(_target);
            }
            if (GUILayout.Button("同步 Tags"))
            {
                _target.ScanPrefabComponents();
                EditorUtility.SetDirty(_target);
            }
            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();
        }

        private static List<string> _cachedTypeNames;
        private static List<string> _cachedFullNames;

        /// <summary>
        /// 从 AppDomain 动态收集所有可用类型名（Editor 侧缓存）
        /// </summary>
        private static void EnsureEditorTypeCache()
        {
            if (_cachedTypeNames != null) return;

            _cachedTypeNames = new List<string>();
            _cachedFullNames = new List<string>();
            var seen = new HashSet<string>();

            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in asm.GetTypes())
                    {
                        var full = type.FullName;
                        if (string.IsNullOrEmpty(full) || full.Contains("`") || full.Contains("<")) continue;
                        if (!seen.Add(full)) continue;

                        _cachedTypeNames.Add(type.Name);
                        _cachedFullNames.Add(full);
                    }
                }
                catch { }
            }
        }

        private void DrawValueTypeField()
        {
            EditorGUILayout.LabelField("绑定类型 (valueType)", EditorStyles.label);

            // 输入行
            EditorGUILayout.BeginHorizontal();
            var currentValue = _valueTypeNameProp.stringValue;
            var newValue = EditorGUILayout.TextField(currentValue);

            if (newValue != currentValue)
            {
                var corrected = XuiavrgPropertyGroupScriptableObject.NormalizeTypeName(newValue);
                _valueTypeNameProp.stringValue = corrected;
                currentValue = corrected;
            }

            // "搜索类型" 按钮
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                var menu = new GenericMenu();
                EnsureEditorTypeCache();

                // 如果有输入，用 LevenshteinDistance 排序，最近的排前面
                var ranked = new List<(int dist, string name, string full)>();
                for (int i = 0; i < _cachedTypeNames.Count; i++)
                {
                    XericLibrary.Runtime.MacroLibrary.MacroMath.LevenshteinDistance(
                        new[] { _cachedTypeNames[i] }, currentValue, out int dist, out _);
                    ranked.Add((dist, _cachedTypeNames[i], _cachedFullNames[i]));
                }

                ranked.Sort((a, b) => a.dist.CompareTo(b.dist));

                int shown = 0;
                // 完全匹配的排最前面
                foreach (var (dist, name, full) in ranked)
                {
                    if (dist != 0 || shown >= 30) break;
                    menu.AddItem(new GUIContent($"{full}  ({name})"), false, () =>
                    {
                        _valueTypeNameProp.stringValue = full;
                        serializedObject.ApplyModifiedProperties();
                    });
                    shown++;
                }

                // 用分隔线隔开，然后显示近匹配
                if (shown > 0 && ranked.Count > shown)
                    menu.AddSeparator("");

                int nearTotal = 0;
                foreach (var (dist, name, full) in ranked)
                {
                    if (dist == 0) continue;
                    if (nearTotal >= 20) break;
                    menu.AddItem(new GUIContent($"{full}  ({name})"), false, () =>
                    {
                        _valueTypeNameProp.stringValue = full;
                        serializedObject.ApplyModifiedProperties();
                    });
                    nearTotal++;
                }

                menu.ShowAsContext();
            }

            EditorGUILayout.EndHorizontal();

            // 纠错提示
            var normalized = XuiavrgPropertyGroupScriptableObject.NormalizeTypeName(currentValue);
            if (normalized != currentValue && !string.IsNullOrEmpty(currentValue))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.HelpBox($"识别为: {normalized}", MessageType.Info);
                if (GUILayout.Button("纠正", GUILayout.Width(60), GUILayout.Height(38)))
                {
                    _valueTypeNameProp.stringValue = normalized;
                }
                EditorGUILayout.EndHorizontal();
            }
        }
    }
}
