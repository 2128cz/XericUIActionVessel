using System;
using System.Reflection;
using UnityEngine.Serialization;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 反射属性中间层
    /// </summary>
    [Serializable]
    public class XuiavrgInspectorProperty
    {
        public object valueTarget;
        public bool isProperty;
        public PropertyInfo propertyInfo;
        public FieldInfo fieldInfo;

        public string labelName;
        public string uiPropertyName;
        public string unitLabelName;
        public bool mustFillIn;
        public int inspectorOrder;
        public string groupName;
        public string onTriggerCall;
        public XuiavrgSize argumentSize;
        public bool hideInInspector;


        /// <summary>
        /// 绑定到数据端
        /// </summary>
        /// <param name="memberInfo"></param>
        /// <param name="target"></param>
        public void RegsiterDataValue(MemberInfo memberInfo, object target)
        {
            switch (memberInfo)
            {
                case PropertyInfo propertyInfo:
                    RegsiterDataValue(propertyInfo, target);
                    break;
                case FieldInfo fieldInfo:
                    RegsiterDataValue(fieldInfo, target);
                    break;
            }
        }

        /// <summary>
        /// 绑定到数据端
        /// </summary>
        /// <param name="memberInfo"></param>
        /// <param name="target"></param>
        public virtual void RegsiterDataValue(PropertyInfo memberInfo, object target)
        {
            valueTarget = target;
            isProperty = true;
            propertyInfo = memberInfo;
        }

        /// <summary>
        /// 绑定到数据端
        /// </summary>
        /// <param name="memberInfo"></param>
        /// <param name="target"></param>
        public virtual void RegsiterDataValue(FieldInfo memberInfo, object target)
        {
            valueTarget = target;
            isProperty = false;
            fieldInfo = memberInfo;
        }

        /// <summary>
        /// 绑定到界面端
        /// </summary>
        public virtual void RegsiterUIElement()
        {
            
        }

    
        /// <summary>
        /// 数据端值发生变更
        /// </summary>
        public void CallDataValueChange()
        {
            
        }

        /// <summary>
        /// 界面端数据发生变更
        /// </summary>
        public void CallUIValueChange()
        {
            
        }

        /// <summary>
        /// 从数据端获取值
        /// </summary>
        /// <returns></returns>
        public object GetValueFromTargetMember()
        {
            if (isProperty)
                return propertyInfo.GetValue(valueTarget);
            else
                return fieldInfo.GetValue(valueTarget);
        }

        /// <summary>
        /// 设置值到数据端
        /// </summary>
        /// <param name="value"></param>
        public void SetValueToTargetMember(object value)
        {
            if (isProperty)
                propertyInfo.SetValue(valueTarget, value);
            else
                fieldInfo.SetValue(valueTarget, value);
        }

        /// <summary>
        /// 从界面端获取值
        /// </summary>
        /// <returns></returns>
        public object GetValueFormUIElement()
        {
            return null;
        }

        /// <summary>
        /// 设置值到界面端
        /// </summary>
        /// <param name="value"></param>
        public void SetValueToUIElement(object value)
        {
            
        }

        public void Clear()
        {
        }
    }
}