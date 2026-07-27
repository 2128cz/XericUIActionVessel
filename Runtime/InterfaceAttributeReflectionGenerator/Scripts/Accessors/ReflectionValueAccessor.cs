using System;
using System.Reflection;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 反射值访问器 — 通过 FieldInfo / PropertyInfo 读写 C# 对象成员
    /// </summary>
    public class ReflectionValueAccessor : IAvrgValueAccessor
    {
        private readonly MemberInfo _memberInfo;
        private object _targetInstance;

        public Type ValueType { get; }
        public bool IsReadOnly { get; }

        public ReflectionValueAccessor(MemberInfo memberInfo, object targetInstance)
        {
            _memberInfo = memberInfo;
            _targetInstance = targetInstance;

            ValueType = memberInfo switch
            {
                FieldInfo fi => fi.FieldType,
                PropertyInfo pi => pi.PropertyType,
                _ => typeof(object)
            };

            IsReadOnly = memberInfo is PropertyInfo pi2 && !pi2.CanWrite;
        }

        public object GetValue()
        {
            if (_targetInstance == null) return null;
            return _memberInfo switch
            {
                FieldInfo fi => fi.GetValue(_targetInstance),
                PropertyInfo pi => pi.GetValue(_targetInstance),
                _ => null
            };
        }

        public void SetValue(object value)
        {
            if (IsReadOnly || _targetInstance == null) return;
            switch (_memberInfo)
            {
                case FieldInfo fi:
                    fi.SetValue(_targetInstance, value);
                    break;
                case PropertyInfo pi:
                    pi.SetValue(_targetInstance, value);
                    break;
            }
        }

        public string FormatDisplay(object value) => value?.ToString() ?? "";
    }

    /// <summary>
    /// 反射值访问器工厂 — 持有 MemberInfo，创建时绑定 TargetInstance
    /// </summary>
    public class ReflectionAccessorFactory : IAvrgValueAccessorFactory
    {
        private readonly MemberInfo _memberInfo;

        public ReflectionAccessorFactory(MemberInfo memberInfo)
        {
            _memberInfo = memberInfo;
        }

        public IAvrgValueAccessor CreateAccessor(object targetInstance)
        {
            return new ReflectionValueAccessor(_memberInfo, targetInstance);
        }
    }
}
