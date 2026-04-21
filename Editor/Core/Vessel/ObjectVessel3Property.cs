using System;
using UnityEditor;
using UnityEngine;
using XericLibrary.Runtime.MacroLibrary;
using XericUI.Core.Vessel;
using Object = UnityEngine.Object;

namespace XericUIEditor.Core.Vessel
{
    [CustomPropertyDrawer(typeof(ObjectVessel<,,>))]
    internal class ObjectVessel3Property : ObjectVesselPropertyBase
    {
        private Type property0Type = typeof(Object);
        private Type property1Type = typeof(Object);
        private Type property2Type = typeof(Object);
        private string originTooltip = null;
        private const float horizontalSpacing = 2f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var targetProperty0 = property.FindPropertyRelative("target0");
            var targetProperty1 = property.FindPropertyRelative("target1");
            var targetProperty2 = property.FindPropertyRelative("target2");

            if (targetProperty0 == null || targetProperty1 == null || targetProperty2 == null)
            {
                EditorGUI.HelpBox(position, "[PROPERTY ERROR: target0/target1/target2 not found]", MessageType.Error);
                EditorGUI.EndProperty();
                return;
            }

            Type genericType = GetActualGenericType(fieldInfo.FieldType, typeof(ObjectVessel<,,>));
            if (genericType != null && genericType.IsGenericType)
            {
                Type[] typeArgs = genericType.GetGenericArguments();
                if (typeArgs.Length >= 1) property0Type = typeArgs[0];
                if (typeArgs.Length >= 2) property1Type = typeArgs[1];
                if (typeArgs.Length >= 3) property2Type = typeArgs[2];

                // 更新Tooltip
                if (string.IsNullOrEmpty(originTooltip))
                {
                    originTooltip =
                        $"<{property0Type.Name}, {property1Type.Name}, {property2Type.Name}>\n{label.tooltip}";
                    label.tooltip = originTooltip;
                }
            }

            // 布局计算（三个属性等分剩余空间）
            float labelWidth = EditorGUIUtility.labelWidth;
            float indentOffset = EditorGUI.indentLevel * 15f;
            float totalFieldWidth = position.width - labelWidth - indentOffset;
            float singleFieldWidth = (totalFieldWidth - horizontalSpacing * 2) / 3; // 三个属性，两个间距

            // 计算三个属性框的位置
            Rect fieldRect0 = new Rect(
                position.x + labelWidth + indentOffset,
                position.y,
                singleFieldWidth,
                EditorGUIUtility.singleLineHeight
            );

            Rect fieldRect1 = new Rect(
                fieldRect0.x + singleFieldWidth + horizontalSpacing,
                position.y,
                singleFieldWidth,
                EditorGUIUtility.singleLineHeight
            );

            Rect fieldRect2 = new Rect(
                fieldRect1.x + singleFieldWidth + horizontalSpacing,
                position.y,
                singleFieldWidth,
                EditorGUIUtility.singleLineHeight
            );

            // 绘制标签
            EditorGUI.PrefixLabel(
                new Rect(position.x + indentOffset, position.y, labelWidth, position.height),
                label
            );

            targetProperty0.objectReferenceValue = EditorGUI.ObjectField(
                fieldRect0, GUIContent.none, targetProperty0.objectReferenceValue, property0Type, true
            );

            targetProperty1.objectReferenceValue = EditorGUI.ObjectField(
                fieldRect1, GUIContent.none, targetProperty1.objectReferenceValue, property1Type, true
            );

            targetProperty2.objectReferenceValue = EditorGUI.ObjectField(
                fieldRect2, GUIContent.none, targetProperty2.objectReferenceValue, property2Type, true
            );

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }
    }
}