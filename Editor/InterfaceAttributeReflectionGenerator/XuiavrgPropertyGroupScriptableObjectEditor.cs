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
            EditorGUILayout.PropertyField(_valueTypeNameProp, new GUIContent("绑定类型 (valueType)"));
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
    }
}
