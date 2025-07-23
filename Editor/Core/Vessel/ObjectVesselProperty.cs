using System;
using UnityEditor;
using UnityEngine;
using XericUI.Core.Vessel;
using Object = UnityEngine.Object;

namespace XericUIEditor.Core.Vessel
{
    [CustomPropertyDrawer(typeof(ObjectVessel<>))]
    public class ObjectVesselProperty : ObjectVesselPropertyBase
    {
        private Type property0Type = typeof(Object);
        private string originTooltip = null;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var targetProperty0 = property.FindPropertyRelative("target");

            if (targetProperty0 == null)
            {
                // 如果找不到字段，显示错误信息
                EditorGUI.HelpBox(position, "[PROPERTY ERROR]", MessageType.Error);
                EditorGUI.EndProperty();
                return;
            }

            Type genericType = GetActualGenericType(fieldInfo.FieldType, typeof(ObjectVessel<>));
            if (genericType != null && genericType.IsGenericType)
            {
                Type[] typeArgs = genericType.GetGenericArguments();
                if (typeArgs.Length >= 1) property0Type = typeArgs[0];

                // 更新Tooltip
                if (string.IsNullOrEmpty(originTooltip))
                {
                    originTooltip = $"<{property0Type?.Name}>\n{label.tooltip}";
                    label.tooltip = originTooltip;
                }
            }

            targetProperty0.objectReferenceValue = EditorGUI.ObjectField(
                position,
                label,
                targetProperty0.objectReferenceValue,
                property0Type,
                true
            );

            EditorGUI.EndProperty();
        }
        
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }
    }
}