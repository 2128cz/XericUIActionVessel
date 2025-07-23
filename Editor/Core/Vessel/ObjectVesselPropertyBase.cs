using System;
using System.Collections.Generic;
using UnityEditor;
using XericUI.Core.Vessel;

namespace XericUIEditor.Core.Vessel
{
    public class ObjectVesselPropertyBase : PropertyDrawer
    {
        /// <summary>
        /// 递归获取实际的泛型类型（处理嵌套情况）
        /// </summary>
        /// <param name="type"></param>
        /// <param name="currentGenericDefinition"></param>
        /// <returns></returns>
        protected Type GetActualGenericType(Type type, Type currentGenericDefinition)
        {
            // 如果是泛型类型，检查是否为ObjectVessel<,>或继续递归
            if (type.IsGenericType)
            {
                // 检查是否为ObjectVessel<,>的实例
                Type genericDefinition = type.GetGenericTypeDefinition();
                if (genericDefinition == currentGenericDefinition)
                {
                    return type; // 找到目标类型
                }

                // 处理嵌套泛型（如List<ObjectVessel<,>>）
                foreach (Type argType in type.GetGenericArguments())
                {
                    Type nestedType = GetActualGenericType(argType, currentGenericDefinition);
                    if (nestedType != null)
                    {
                        return nestedType;
                    }
                }
            }

            // 处理数组或其他可能的容器类型
            if (type.IsArray)
            {
                return GetActualGenericType(type.GetElementType(), currentGenericDefinition);
            }

            // 检查基类
            if (type.BaseType != null && type.BaseType != typeof(object))
            {
                return GetActualGenericType(type.BaseType, currentGenericDefinition);
            }

            return null; // 未找到
        }
    }
}