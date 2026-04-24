using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif
using UnityEngine;
using XericLibrary.Runtime.MacroLibrary;
using Object = UnityEngine.Object;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 属性映射器
    /// </summary>
    [Serializable]
    [CreateAssetMenu(fileName = "Xeric UI avr Property", menuName = "Xeric Library/UI Action Vessel/Reflection UI Gen/UI avr Property", order = 10)]
    public class XuiavrPropertyScriptableObject : ScriptableObject
    {
#if ODIN_INSPECTOR
        [LabelText("属性类型")]
        [ValueDropdown("AssemblyValueTypeNames")]
#endif
        public string valueType; 
#if ODIN_INSPECTOR
        [LabelText("属性名称")]
#endif
        public string propertyName;
#if ODIN_INSPECTOR
        [LabelText("界面预制体")]
#endif
        public GameObject prefab;
        
        /// <summary>
        /// 属性类型
        /// </summary>
        public System.Type ValueType
        {
            get
            {
                if (_valueType != null)
                    return _valueType;
                return _valueType = Type.GetType(valueType);
            }
        }
        private System.Type _valueType;

        /// <summary>
        /// 属性类型名称枚举
        /// </summary>
        private IEnumerable AssemblyValueTypeNames
        {
            get
            {
                if (_assemblyValueTypeName != null)
                    return _assemblyValueTypeName;
                _assemblyValueTypeName = new ValueDropdownList<string>();
                
                // foreach (var typeName in MacroReflection.GetAllAssignableTypeInAppDomainAssembly(
                //              typeof(float), 
                //              typeof(string), 
                //              typeof(bool), 
                //              typeof(Enum), 
                //              typeof(int) 
                //              ).Select(a => a.FullName))
                // {
                //     _assemblyValueTypeName.Add(typeName);
                // }
                
                _assemblyValueTypeName.AddRange(AllValueType.Select(a => new ValueDropdownItem<string>(a.Name, a.FullName)));
                return _assemblyValueTypeName;
            }
        }
        private static ValueDropdownList<string> _assemblyValueTypeName = null;
        
        /// <summary>
        /// 直接定义所有需要检查的类型
        /// </summary>
        public static IEnumerable<Type> AllValueType
        {
            get
            {
                yield return typeof(Enum);
                yield return typeof(decimal);
                yield return typeof(double);
                yield return typeof(float);
                yield return typeof(ulong);
                yield return typeof(long);
                yield return typeof(int);
                yield return typeof(uint);
                yield return typeof(short);
                yield return typeof(ushort);
                yield return typeof(byte);
                yield return typeof(sbyte);
                yield return typeof(string);
                yield return typeof(char);
                yield return typeof(bool);
            }
        }
        
        
        
        
    }
}