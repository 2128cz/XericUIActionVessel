using UnityEngine;
using UnityEngine.UI;
using XericUI.Helper;
using XericUI.ReflectionUIGenerator;

namespace XericUI.OverrideUI
{
    /// <summary>
    /// 带字段标记的增强滑块组件
    /// 继承 Unity Slider，额外支持 XuiavrgFieldTag 标记，便于反射界面生成器自动绑定
    /// </summary>
    [AddComponentMenu("Xeric UI Vessel/Attribute/Slider", 51)]
    public class XericUIrAttributeSlider : Slider, IXuiavrgFieldTagProvider
    {
        [SerializeField]
        [Tooltip("Entry 功能标记，用于自动绑定匹配")]
        private XuiavrgFieldTag _entryTag = XuiavrgFieldTag.Slider;

        [SerializeField]
        [Tooltip("同一 Tag 存在多个时的索引区分")]
        private int _tagIndex = 0;

        public XuiavrgFieldTag EntryTag => _entryTag;
        public int TagIndex => _tagIndex;

        #region 组件替换

#if UNITY_EDITOR
        [UnityEditor.MenuItem("CONTEXT/Slider/Replace To/Xeric (Attribute)", true)]
        private static bool ValidateReplaceToAttribute(UnityEditor.MenuCommand command)
            => command.context is Slider and not XericUIrAttributeSlider;

        [UnityEditor.MenuItem("CONTEXT/Slider/Replace To/Xeric (Attribute)", false, 10)]
        private static void ReplaceToAttribute(UnityEditor.MenuCommand command)
            => command.ReplaceCommandContext<Slider, XericUIrAttributeSlider,
                (Graphic targetGraphic, float value, Slider.SliderEvent onValueChanged)>(
                o =>
                    (o.targetGraphic, o.value, o.onValueChanged),
                (t, d) =>
                {
                    t.targetGraphic = d.targetGraphic;
                    t.value = d.value;
                    t.onValueChanged = d.onValueChanged;
                });

        [UnityEditor.MenuItem("CONTEXT/Slider/Replace To/Unity Slider", true)]
        private static bool ValidateReplaceToUnity(UnityEditor.MenuCommand command)
            => command.context is XericUIrAttributeSlider;

        [UnityEditor.MenuItem("CONTEXT/Slider/Replace To/Unity Slider", false, 100)]
        private static void ReplaceToUnity(UnityEditor.MenuCommand command)
            => command.ReplaceCommandContext<XericUIrAttributeSlider, Slider,
                (Graphic targetGraphic, float value, Slider.SliderEvent onValueChanged)>(
                o =>
                    (o.targetGraphic, o.value, o.onValueChanged),
                (t, d) =>
                {
                    t.targetGraphic = d.targetGraphic;
                    t.value = d.value;
                    t.onValueChanged = d.onValueChanged;
                });
#endif

        #endregion
    }
}
