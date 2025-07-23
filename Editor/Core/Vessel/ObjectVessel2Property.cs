using System;
using UnityEditor;
using UnityEngine;
using XericLibrary.Runtime.MacroLibrary;
using XericUI.Core.Vessel;
using Object = UnityEngine.Object;

namespace XericUIEditor.Core.Vessel
{
    [CustomPropertyDrawer(typeof(ObjectVessel<,>))]
    public class ObjectVessel2Property : ObjectVesselPropertyBase
    {
        private Type property0Type = typeof(Object);
        private Type property1Type = typeof(Object);
        private string originTooltip = null;
        private const float horizontalSpacing = 2f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var targetProperty0 = property.FindPropertyRelative("target0");
            var targetProperty1 = property.FindPropertyRelative("target1");

            if (targetProperty0 == null || targetProperty1 == null)
            {
                EditorGUI.HelpBox(position, "[PROPERTY ERROR: target0/target1 not found]", MessageType.Error);
                EditorGUI.EndProperty();
                return;
            }

            Type genericType = GetActualGenericType(fieldInfo.FieldType, typeof(ObjectVessel<,>));
            if (genericType != null && genericType.IsGenericType)
            {
                Type[] typeArgs = genericType.GetGenericArguments();
                if (typeArgs.Length >= 1) property0Type = typeArgs[0];
                if (typeArgs.Length >= 2) property1Type = typeArgs[1];

                // 更新Tooltip
                if (string.IsNullOrEmpty(originTooltip))
                {
                    originTooltip = $"<{property0Type?.Name}, {property1Type?.Name}>\n{label.tooltip}";
                    label.tooltip = originTooltip;
                }
            }

            // 布局计算（保持原有逻辑不变）
            float labelWidth = EditorGUIUtility.labelWidth;
            float indentOffset = EditorGUI.indentLevel * 15f;
            float totalFieldWidth = position.width - labelWidth - indentOffset;
            float singleFieldWidth = (totalFieldWidth - horizontalSpacing) / 2;

            Rect leftFieldRect = new Rect(
                position.x + labelWidth + indentOffset,
                position.y,
                singleFieldWidth,
                EditorGUIUtility.singleLineHeight
            );

            Rect rightFieldRect = new Rect(
                leftFieldRect.x + singleFieldWidth + horizontalSpacing,
                position.y,
                singleFieldWidth,
                EditorGUIUtility.singleLineHeight
            );

            // 绘制标签和属性（保持原有逻辑不变）
            EditorGUI.PrefixLabel(
                new Rect(position.x + indentOffset, position.y, labelWidth, position.height),
                label
            );

            targetProperty0.objectReferenceValue = EditorGUI.ObjectField(
                leftFieldRect, GUIContent.none, targetProperty0.objectReferenceValue, property0Type, true
            );

            targetProperty1.objectReferenceValue = EditorGUI.ObjectField(
                rightFieldRect, GUIContent.none, targetProperty1.objectReferenceValue, property1Type, true
            );

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }
    }
}