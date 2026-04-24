using System;
using System.Collections.Generic;
using System.Reflection;

namespace XericUI.ReflectionUIGenerator
{
    public static class XuiavrgUtilities
    {
        /// <summary>
        /// 获取所有运行时成员（字段和属性）
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static IEnumerable<MemberInfo> GetRuntimeMemberInfo(this Type type)
        {
            foreach (var field in type.GetRuntimeFields())
                yield return field;
            foreach (var property in type.GetRuntimeProperties())
                yield return property;
        }
    }
}