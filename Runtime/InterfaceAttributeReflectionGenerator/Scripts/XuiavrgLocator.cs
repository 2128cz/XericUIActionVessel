using System;
using System.Collections;
using System.Collections.Generic;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif
using UnityEngine;
using UnityEngine.Serialization;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// ui反射属性定位器，放在UI对象的根节点上，设定好接下来的目标即可
    /// </summary>
    public class XuiavrgLocator : MonoBehaviour
    {
        public delegate void AnyTrigger(LocatorProperty locator, object value, int selectStart, int selectEnd);

#if ODIN_INSPECTOR
        [LabelText("映射组件")]
#endif
        public List<LocatorProperty> Properties;

        [Serializable]
        public class LocatorProperty
        {
#if ODIN_INSPECTOR
            [LabelText("组件标识名")]
#endif
            [Tooltip("建议使用 name, label, unit, sign, input, output, button, slider, edit, select 等简单易懂的名称进行标记")]
            public string propertyName;
#if ODIN_INSPECTOR
            [LabelText("组件类型")]
#endif
            public UIElementClassify classify;
#if dUI_TextMeshPro
#if ODIN_INSPECTOR
            [ShowIf("classify", UIElementClassify.TMPText)]
#endif
            public TMPro.TMP_Text TMPText;
#if ODIN_INSPECTOR
            [ShowIf("classify", UIElementClassify.TMPInputField)]
#endif
            public TMPro.TMP_InputField TMPInputField;
#endif
#if ODIN_INSPECTOR
            [ShowIf("classify", UIElementClassify.UIText)]
#endif
            public UnityEngine.UI.Text UIText;
#if ODIN_INSPECTOR
            [ShowIf("classify", UIElementClassify.UIInputField)]
#endif
            public UnityEngine.UI.InputField UIInputField;
#if ODIN_INSPECTOR
            [ShowIf("classify", UIElementClassify.UIToggle)]
#endif
            public UnityEngine.UI.Toggle UIToggle;
#if ODIN_INSPECTOR
            [ShowIf("classify", UIElementClassify.UIButton)]
#endif
            public UnityEngine.UI.Button UIButton;
#if ODIN_INSPECTOR
            [ShowIf("classify", UIElementClassify.UISlider)]
#endif
            public UnityEngine.UI.Slider UISlider;
#if ODIN_INSPECTOR
            [ShowIf("classify", UIElementClassify.UIDropdown)]
#endif
            public UnityEngine.UI.Dropdown UIDropdown;
#if dUI_TextMeshPro
#if ODIN_INSPECTOR
            [ShowIf("classify", UIElementClassify.TMPInputField)]
#endif
            public TMPInputTriggerEvent TMPInputTriggerOnEventType;
#endif
#if ODIN_INSPECTOR
            [ShowIf("classify", UIElementClassify.UIInputField)]
#endif
            public UIInputTriggerEvent UIInputTriggerOnEventType;

            /// <summary>
            /// 支持的组件类型
            /// </summary>
            public enum UIElementClassify
            {
                None,
#if dUI_TextMeshPro
                TMPText,
                TMPInputField,
#endif
                UIText,
                UIInputField,
                UIToggle,
                UIButton,
                UISlider,
                UIDropdown,
            }

            [Flags]
            public enum TMPInputTriggerEvent
            {
                none,
                onEndEdit = 1 << 1,
                onSubmit = 1 << 2,
                onSelect = 1 << 3,
                onDeselect = 1 << 4,
                onTextSelection = 1 << 5,
                onEndTextSelection = 1 << 6,
                onValueChanged = 1 << 7,
                onTouchScreenKeyboardStatusChanged = 1 << 8,
            }

            public enum UIInputTriggerEvent
            {
                none,
                onEndEdit,
                onSubmit,
                onValueChanged,
            }

            /// <summary>
            /// 获取值
            /// </summary>
            /// <returns></returns>
            /// <exception cref="ArgumentOutOfRangeException"></exception>
            public object GetValue()
            {
                switch (classify)
                {
#if dUI_TextMeshPro
                    case UIElementClassify.TMPText:
                        return TMPText.text;
                    case UIElementClassify.TMPInputField:
                        return TMPInputField.text;
#endif
                    case UIElementClassify.UIText:
                        return UIText.text;
                    case UIElementClassify.UIInputField:
                        return UIInputField.text;
                    case UIElementClassify.UIToggle:
                        return UIToggle.isOn;
                    case UIElementClassify.UIButton:
                        return false;
                    case UIElementClassify.UISlider:
                        return UISlider.value;
                    case UIElementClassify.UIDropdown:
                        return UIDropdown.value;
                    default:
                        return null;
                }
            }

            /// <summary>
            /// 设置值
            /// </summary>
            /// <param name="value"></param>
            /// <exception cref="ArgumentOutOfRangeException"></exception>
            public void SetValue(object value)
            {
                switch (classify)
                {
#if dUI_TextMeshPro
                    case UIElementClassify.TMPText:
                        TMPText.text = value.ToString();
                        break;
                    case UIElementClassify.TMPInputField:
                        TMPInputField.text = value.ToString();
                        break;
#endif
                    case UIElementClassify.UIText:
                        UIText.text = value.ToString();
                        break;
                    case UIElementClassify.UIInputField:
                        UIInputField.text = value.ToString();
                        break;
                    case UIElementClassify.UIToggle:
                        UIToggle.isOn = Convert.ToBoolean(value);
                        break;
                    case UIElementClassify.UIButton:
                        // UIButton do nothing
                        break;
                    case UIElementClassify.UISlider:
                        UISlider.value = Convert.ToSingle(value);
                        break;
                    case UIElementClassify.UIDropdown:
                        UIDropdown.value = Convert.ToInt32(value);
                        break;
                    default:
                        break;
                }
            }

            /// <summary>
            /// ui触发事件
            /// </summary>
            public event AnyTrigger OnUITrigger
            {
                add
                {
                    switch (classify)
                    {
#if dUI_TextMeshPro
                        case UIElementClassify.TMPInputField:
                            switch (TMPInputTriggerOnEventType)
                            {
                                case TMPInputTriggerEvent.onEndEdit:
                                    TMPInputField.onEndEdit.AddListener(a => value?.Invoke(this, a, 0, 0));
                                    break;
                                case TMPInputTriggerEvent.onSubmit:
                                    TMPInputField.onSubmit.AddListener(a => value?.Invoke(this, a, 0, 0));
                                    break;
                                case TMPInputTriggerEvent.onSelect:
                                    TMPInputField.onSelect.AddListener(a => value?.Invoke(this, a, 0, 0));
                                    break;
                                case TMPInputTriggerEvent.onDeselect:
                                    TMPInputField.onDeselect.AddListener(a => value?.Invoke(this, a, 0, 0));
                                    break;
                                case TMPInputTriggerEvent.onTextSelection:
                                    TMPInputField.onTextSelection.AddListener((content, start, end) =>
                                        value?.Invoke(this, content, start, end));
                                    break;
                                case TMPInputTriggerEvent.onEndTextSelection:
                                    TMPInputField.onEndTextSelection.AddListener((content, start, end) =>
                                        value?.Invoke(this, content, start, end));
                                    break;
                                case TMPInputTriggerEvent.onValueChanged:
                                    TMPInputField.onValueChanged.AddListener(a => value?.Invoke(this, a, 0, 0));
                                    break;
                                case TMPInputTriggerEvent.onTouchScreenKeyboardStatusChanged:
                                    TMPInputField.onTouchScreenKeyboardStatusChanged.AddListener(a =>
                                        value?.Invoke(this, a, 0, 0));
                                    break;
                                default:
                                    break;
                            }

                            break;
#endif
                        case UIElementClassify.UIInputField:
                            switch (UIInputTriggerOnEventType)
                            {
                                case UIInputTriggerEvent.onEndEdit:
                                    UIInputField.onEndEdit.AddListener(a => value?.Invoke(this, a, 0, 0));
                                    break;
                                case UIInputTriggerEvent.onSubmit:
                                    UIInputField.onSubmit.AddListener(a => value?.Invoke(this, a, 0, 0));
                                    break;
                                case UIInputTriggerEvent.onValueChanged:
                                    UIInputField.onValueChanged.AddListener(a => value?.Invoke(this, a, 0, 0));
                                    break;
                                default: break;
                            }

                            break;
                        case UIElementClassify.UIToggle:
                            UIToggle.onValueChanged.AddListener(a => value?.Invoke(this, value, 0, 0));
                            break;
                        case UIElementClassify.UIButton:
                            UIButton.onClick.AddListener(() => value?.Invoke(this, true, 0, 0));
                            break;
                        case UIElementClassify.UISlider:
                            UISlider.onValueChanged.AddListener(a => value?.Invoke(this, a, 0, 0));
                            break;
                        case UIElementClassify.UIDropdown:
                            UIDropdown.onValueChanged.AddListener(a => value?.Invoke(this, a, 0, 0));
                            break;
                        default:
                            break;
                    }
                }
                remove { Debug.Log("任意触发器无法卸载特定事件"); }
            }

            /// <summary>
            /// 清空ui触发事件
            /// </summary>
            public void ClearTriggerEvent()
            {
#if dUI_TextMeshPro
                if (TMPInputField)
                {
                    TMPInputField.onEndEdit.RemoveAllListeners();
                    TMPInputField.onSubmit.RemoveAllListeners();
                    TMPInputField.onSelect.RemoveAllListeners();
                    TMPInputField.onDeselect.RemoveAllListeners();
                    TMPInputField.onTextSelection.RemoveAllListeners();
                    TMPInputField.onEndTextSelection.RemoveAllListeners();
                    TMPInputField.onValueChanged.RemoveAllListeners();
                    TMPInputField.onTouchScreenKeyboardStatusChanged.RemoveAllListeners();
                }
#endif
                if (UIInputField)
                {
                    UIInputField.onEndEdit.RemoveAllListeners();
                    UIInputField.onSubmit.RemoveAllListeners();
                    UIInputField.onValueChanged.RemoveAllListeners();
                }

                UIToggle?.onValueChanged.RemoveAllListeners();
                UIButton?.onClick.RemoveAllListeners();
                UISlider?.onValueChanged.RemoveAllListeners();
                UIDropdown?.onValueChanged.RemoveAllListeners();
            }
        }
        
        // todo 使用名称，类型筛选得到组件
        // todo 根据名称组建字典树加速查找（或许不用）
    }
}