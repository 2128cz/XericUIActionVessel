using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 界面属性编组
    /// 提供预制体与 UI 标记的绑定，支持全局静态访问
    /// </summary>
    [Serializable]
    [CreateAssetMenu(fileName = "Xeric UI avr Property group", menuName = "Xeric Library/UI Action Vessel/Reflection UI Gen/UI avr Property Group", order = 10)]
    public class XuiavrgPropertyGroupScriptableObject : ScriptableObject, IList<XuiavrPropertyScriptableObject>
    {
        #region 全局注册表

        /// <summary>
        /// 全局预制体组列表
        /// 所有 PropertyGroupScriptableObject 在 OnEnable 时自动注册，OnDisable 时自动注销
        /// </summary>
        internal static List<XuiavrgPropertyGroupScriptableObject> GlobalPrefabList
            = new List<XuiavrgPropertyGroupScriptableObject>();

        /// <summary>
        /// 根据组名查找预制体组
        /// </summary>
        public static XuiavrgPropertyGroupScriptableObject FindGroupByName(string groupName)
        {
            return GlobalPrefabList.FirstOrDefault(g => g.groupName == groupName);
        }

        /// <summary>
        /// 查找包含指定标记的预制体组
        /// </summary>
        public static List<XuiavrgPropertyGroupScriptableObject> FindGroupsByTag(XuiavrgFieldTag tag)
        {
            return GlobalPrefabList.Where(g => g.supportedTags.Contains(tag)).ToList();
        }

        /// <summary>
        /// 查找同时包含所有指定标记的预制体组
        /// </summary>
        public static XuiavrgPropertyGroupScriptableObject FindGroupByTags(params XuiavrgFieldTag[] tags)
        {
            return GlobalPrefabList.FirstOrDefault(g =>
                tags.All(t => g.supportedTags.Contains(t)));
        }

        #endregion

        #region 字段

        /// <summary>预制体组名称（用于 XuiavrgFieldPrefabOverride 匹配）</summary>
        public string groupName;

        /// <summary>此组支持的功能标记集合</summary>
        public List<XuiavrgFieldTag> supportedTags = new List<XuiavrgFieldTag>();

        public List<XuiavrPropertyScriptableObject> propertys = new List<XuiavrPropertyScriptableObject>();

        #endregion

        #region 生命周期

        void OnEnable()
        {
            if (!GlobalPrefabList.Contains(this))
                GlobalPrefabList.Add(this);
        }

        void OnDisable()
        {
            GlobalPrefabList.Remove(this);
        }

        #endregion

        public IEnumerator<XuiavrPropertyScriptableObject> GetEnumerator()
        {
            return propertys.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)propertys).GetEnumerator();
        }

        public void Add(XuiavrPropertyScriptableObject item)
        {
            propertys.Add(item);
        }

        public void Clear()
        {
            propertys.Clear();
        }

        public bool Contains(XuiavrPropertyScriptableObject item)
        {
            return propertys.Contains(item);
        }

        public void CopyTo(XuiavrPropertyScriptableObject[] array, int arrayIndex)
        {
            propertys.CopyTo(array, arrayIndex);
        }

        public bool Remove(XuiavrPropertyScriptableObject item)
        {
            return propertys.Remove(item);
        }

        public int Count => propertys.Count;

        public bool IsReadOnly => true;

        public int IndexOf(XuiavrPropertyScriptableObject item)
        {
            return propertys.IndexOf(item);
        }

        public void Insert(int index, XuiavrPropertyScriptableObject item)
        {
            propertys.Insert(index, item);
        }

        public void RemoveAt(int index)
        {
            propertys.RemoveAt(index);
        }

        public XuiavrPropertyScriptableObject this[int index]
        {
            get => propertys[index];
            set => propertys[index] = value;
        }
    }
}