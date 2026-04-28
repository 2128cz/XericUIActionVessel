using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Serialization;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 反射属性中间层
    /// </summary>
    [Serializable]
    public class XuiavrgInspectorProperty
    {
        #region 字段属性
        
        public object valueTarget;
        public bool isProperty;
        public PropertyInfo propertyInfo;
        public FieldInfo fieldInfo;

        // 基本信息
        public string labelName;                        // 字段名称
        public string uiPropertyName;                   // ui属性名称
        public string unitLabelName;                    // 单位名称
        public bool required;                           // 项目必填
        public bool notEmpty;                           // 不可为空
        public int inspectorOrder;                      // 显示在检查器上的顺序
        public string groupName;                        // 编组名称
        public string onTriggerCall;                    // 值变更时触发的反射
        public XuiavrgSize argumentSize;                // 属性尺寸
        public bool hideInInspector;                    // 在检查器上隐藏显示
        
        // 字符串检查
        public int stringMinLength;                     // 最小长度 
        public int stringMaxLength = int.MaxValue;      // 最大长度
        public string stringRegexPattern = "";          // 正则检查
        public bool stringOnlyLetters;                  // 允许字符
        public bool stringOnlyNumber;                   // 允许数字
        public bool stringOnlyEmail = true;             // 允许邮箱
        public bool stringOnlyPhone = true;             // 允许电话号码
        public bool stringOnlyUrl = true;               // 允许网址

        // 数值检查
        public float numberMinValue;                    // 最小值
        public float numberMaxValue;                    // 最大值
        
        // 集合检查
        public int collectionMinCount;                  // 集合的最小尺寸
        public int collectionMaxCount = int.MaxValue;   // 集合的最大尺寸
        public bool collectionNotEmpty;                 // 集合不可为空
        
        #endregion

        #region 检查方法
        
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
        
        #endregion
    }
}