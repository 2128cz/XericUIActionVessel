using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using XericUI.XChart;

namespace XericUI.XChart.Editor
{
    /// <summary>
    /// 元素类型登记表（编辑器专用）。
    ///
    /// 反射扫描所有带 <see cref="XChartElementAttribute"/> 的 <see cref="XChartElementConfig"/> 派生类，
    /// 供 Inspector 的"添加元素"菜单列出。放在编辑器程序集里是刻意的：运行时不需要枚举全程序集，
    /// 也避免 IL2CPP 裁剪把只在菜单里用到的类型剪掉。
    /// </summary>
    internal static class XChartElementRegistry
    {
        internal struct Entry
        {
            public Type ConfigType;
            public XChartElementAttribute Meta;
        }

        private static List<Entry> s_Entries;

        /// <summary>所有已注册元素（按菜单序）</summary>
        internal static IReadOnlyList<Entry> GetEntries()
        {
            if (s_Entries != null) return s_Entries;

            s_Entries = new List<Entry>();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int a = 0; a < assemblies.Length; a++)
            {
                Type[] types;
                try
                {
                    types = assemblies[a].GetTypes();
                }
                catch (Exception)
                {
                    // 动态程序集/加载失败的程序集直接跳过，不能让一个坏程序集挡住菜单
                    continue;
                }

                for (int i = 0; i < types.Length; i++)
                {
                    var type = types[i];
                    if (type == null || type.IsAbstract || !typeof(XChartElementConfig).IsAssignableFrom(type))
                        continue;

                    var meta = (XChartElementAttribute)Attribute.GetCustomAttribute(
                        type, typeof(XChartElementAttribute), false);
                    if (meta == null) continue;

                    s_Entries.Add(new Entry { ConfigType = type, Meta = meta });
                }
            }

            s_Entries.Sort((x, y) =>
            {
                int byOrder = x.Meta.MenuOrder.CompareTo(y.Meta.MenuOrder);
                return byOrder != 0 ? byOrder : string.CompareOrdinal(x.Meta.DisplayName, y.Meta.DisplayName);
            });
            return s_Entries;
        }

        /// <summary>按类型造一个默认配置</summary>
        internal static XChartElementConfig Create(Type configType)
        {
            try
            {
                return (XChartElementConfig)Activator.CreateInstance(configType);
            }
            catch (Exception exception)
            {
                Debug.LogError("Failed to create a chart element config: " + configType + " / " + exception.Message);
                return null;
            }
        }
    }
}