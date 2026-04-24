using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 界面属性编组
    /// </summary>
    [Serializable]
    [CreateAssetMenu(fileName = "Xeric UI avr Property group", menuName = "Xeric Library/UI Action Vessel/Reflection UI Gen/UI avr Property Group", order = 10)]
    public class XuiavrgPropertyGroupScriptableObject : ScriptableObject, IList<XuiavrPropertyScriptableObject>
    {
        public List<XuiavrPropertyScriptableObject> propertys = new List<XuiavrPropertyScriptableObject>();

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