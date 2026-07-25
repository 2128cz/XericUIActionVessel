using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 材质属性访问器 — 通过 Material.Get/Set 系列方法读写 Shader 属性
    /// </summary>
    public class MaterialPropertyAccessor : IAvrgValueAccessor
    {
        private readonly Material _material;
        private readonly string _propName;
        private readonly ShaderPropertyType _shaderType;

        public Type ValueType { get; }
        public bool IsReadOnly { get; }

        public MaterialPropertyAccessor(
            Material material,
            string propName,
            ShaderPropertyType shaderType,
            Type valueType,
            bool isReadOnly = false)
        {
            _material = material;
            _propName = propName;
            _shaderType = shaderType;
            ValueType = valueType;
            IsReadOnly = isReadOnly;
        }

        public object GetValue()
        {
            if (_material == null) return null;

            return _shaderType switch
            {
                ShaderPropertyType.Float or ShaderPropertyType.Range => _material.GetFloat(_propName),
                ShaderPropertyType.Int => _material.GetInt(_propName),
                ShaderPropertyType.Color => _material.GetColor(_propName),
                ShaderPropertyType.Vector => _material.GetVector(_propName),
                ShaderPropertyType.Texture => _material.GetTexture(_propName),
                _ => null
            };
        }

        public void SetValue(object value)
        {
            if (IsReadOnly || _material == null) return;

            switch (_shaderType)
            {
                case ShaderPropertyType.Float:
                case ShaderPropertyType.Range:
                    _material.SetFloat(_propName, Convert.ToSingle(value));
                    break;
                case ShaderPropertyType.Int:
                    _material.SetInt(_propName, Convert.ToInt32(value));
                    break;
                case ShaderPropertyType.Color:
                    _material.SetColor(_propName, (Color)value);
                    break;
                case ShaderPropertyType.Vector:
                    _material.SetVector(_propName, (Vector4)value);
                    break;
                case ShaderPropertyType.Texture:
                    _material.SetTexture(_propName, value as Texture);
                    break;
            }
        }

        public string FormatDisplay(object value)
        {
            if (value == null) return "None";
            if (value is Texture tex && tex != null) return tex.name;
            return value.ToString();
        }
    }

    /// <summary>
    /// 材质属性访问器工厂 — 持有 Shader 属性名和类型，创建时绑定 Material 实例
    /// </summary>
    public class MaterialPropertyAccessorFactory : IAvrgValueAccessorFactory
    {
        private readonly string _propName;
        private readonly ShaderPropertyType _shaderType;
        private readonly Type _valueType;
        private readonly bool _isReadOnly;

        public MaterialPropertyAccessorFactory(
            string propName,
            ShaderPropertyType shaderType,
            Type valueType,
            bool isReadOnly = false)
        {
            _propName = propName;
            _shaderType = shaderType;
            _valueType = valueType;
            _isReadOnly = isReadOnly;
        }

        public IAvrgValueAccessor CreateAccessor(object targetInstance)
        {
            var material = targetInstance as Material;
            if (material == null) return null;
            return new MaterialPropertyAccessor(material, _propName, _shaderType, _valueType, _isReadOnly);
        }
    }
}
