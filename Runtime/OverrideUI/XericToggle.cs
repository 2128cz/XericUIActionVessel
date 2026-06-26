using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using XericUI.Helper;
using XericUI.OverrideUI;

namespace XericUI
{
    
    [AddComponentMenu("Xeric UI Vessel/UI/Toggle", 31)]
    [DisallowMultipleComponent]
    public class XericToggle : Toggle
    {
#if dUI_TextMeshPro 
        public TMP_Text m_text;

        public string TextContent
        {
            get => m_text?.text ?? string.Empty;
            set { if(m_text) m_text.text = value; }
        }

        public Color TextColor
        {
            get => m_text?.color ?? Color.black;
            set { if(m_text) m_text.color = value; }
        }
#else
        public Text m_text;
        
        public string TextContent
        {
            get => m_text.text;
            set => m_text.text = value;
        }

        public Color TextColor
        {
            get => m_text.color;
            set => m_text.color = value;
        }
#endif
        
        public bool m_enableTextColorTransition = true;
        public ColorBlock m_textTransitionColorMode = ColorBlock.defaultColorBlock;
        public Color m_isonStatusColor = Color.white;
        public bool m_enableTextContextTransition = false;
        public AnimationTriggers m_textTransitionContextMode = new AnimationTriggers()
        {
            normalTrigger = "toggle_normal",
            highlightedTrigger = "toggle_highlight",
            pressedTrigger = "toggle_pressed",
            selectedTrigger = "toggle_selected",
            disabledTrigger = "toggle_disabled",
        };

        public string m_isonStatusTextContext = "toggle_ison";
        
        
        public bool EnableTextColorTransition
        {
            get => m_enableTextColorTransition;
            set => m_enableTextColorTransition = value;
        }
        
        public ColorBlock TextTransitionColorMode
        {
            get => m_textTransitionColorMode;
            set => m_textTransitionColorMode = value;
        }


        private XericImage m_ximage;

        protected XericImage ximage
        {
            get
            {
                if (m_ximage)
                    return m_ximage;
#if UNITY_EDITOR
                if (!graphic)
                    return null;
#endif
                m_ximage = graphic.GetComponent<XericImage>();
                if (m_ximage)
                    return m_ximage;
                Debug.Log("ע���滻toggle��checkmarkΪXericImage��֧�������Ĺ���");
                return null;
            }
        }

        private bool bindXImage;

        protected override void InstantClearState()
        {
            base.InstantClearState();
            
            if (m_enableTextColorTransition)
                TextColor = colors.normalColor;
            if (m_enableTextContextTransition)
                TextContent = m_textTransitionContextMode.normalTrigger;
        }
        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            if (!gameObject.activeInHierarchy)
                return;
            
            Color tintColor;
            Sprite transitionSprite;
            string triggerName;
            Color textColor;
            string transitionName;

            switch (state)
            {
                case SelectionState.Normal:
                    tintColor = colors.normalColor;
                    textColor = m_textTransitionColorMode.normalColor;
                    transitionSprite = null;
                    triggerName = animationTriggers.normalTrigger;
                    transitionName = m_textTransitionContextMode.normalTrigger;
                    break;
                case SelectionState.Highlighted:
                    tintColor = colors.highlightedColor;
                    textColor = m_textTransitionColorMode.highlightedColor;
                    transitionSprite = spriteState.highlightedSprite;
                    triggerName = animationTriggers.highlightedTrigger;
                    transitionName = m_textTransitionContextMode.highlightedTrigger;
                    break;
                case SelectionState.Pressed:
                    tintColor = colors.pressedColor;
                    textColor = m_textTransitionColorMode.pressedColor;
                    transitionSprite = spriteState.pressedSprite;
                    triggerName = animationTriggers.pressedTrigger;
                    transitionName = m_textTransitionContextMode.pressedTrigger;
                    break;
                case SelectionState.Selected:
                    tintColor = colors.selectedColor;
                    textColor = m_textTransitionColorMode.selectedColor;
                    transitionSprite = spriteState.selectedSprite;
                    triggerName = animationTriggers.selectedTrigger;
                    transitionName = m_textTransitionContextMode.selectedTrigger;
                    break;
                case SelectionState.Disabled:
                    tintColor = colors.disabledColor;
                    textColor = m_textTransitionColorMode.disabledColor;
                    transitionSprite = spriteState.disabledSprite;
                    triggerName = animationTriggers.disabledTrigger;
                    transitionName = m_textTransitionContextMode.disabledTrigger;
                    break;
                default:
                    tintColor = colors.normalColor;
                    textColor = m_textTransitionColorMode.normalColor;
                    transitionSprite = null;
                    triggerName = string.Empty;
                    transitionName = m_textTransitionContextMode.normalTrigger;
                    break;
            }
            
            switch (transition)
            {
                case Transition.ColorTint:
                    StartColorTween(tintColor * colors.colorMultiplier, instant);
                    break;
                case Transition.SpriteSwap:
                    DoSpriteSwap(transitionSprite);
                    break;
                case Transition.Animation:
                    TriggerAnimation(triggerName);
                    break;
            }

            if (!bindXImage && ximage != null)
            {
                bindXImage = true;
                ximage.OnToggleCrossFadeCall = b =>
                {
                    if (m_enableTextColorTransition)
                        TextColor = b ? m_textTransitionColorMode.pressedColor : m_textTransitionColorMode.normalColor;
                    if (m_enableTextContextTransition)
                        TextContent = b ? m_textTransitionContextMode.pressedTrigger : m_textTransitionContextMode.normalTrigger;
                };
            }
            
            
            if (m_enableTextColorTransition)
                TextColor = isOn ? m_isonStatusColor : textColor;
            if (m_enableTextContextTransition)
                TextContent = transitionName;
        }

        void StartColorTween(Color targetColor, bool instant)
        {
            if (targetGraphic == null)
                return;

            targetGraphic.CrossFadeColor(targetColor, instant ? 0f : colors.fadeDuration, true, true);
        }

        void DoSpriteSwap(Sprite newSprite)
        {
            if (image == null)
                return;

            image.overrideSprite = newSprite;
        }

        void TriggerAnimation(string triggername)
        {
#if PACKAGE_ANIMATION
            if (transition != Transition.Animation || animator == null || !animator.isActiveAndEnabled || !animator.hasBoundPlayables || string.IsNullOrEmpty(triggername))
                return;

            animator.ResetTrigger(animationTriggers.normalTrigger);
            animator.ResetTrigger(animationTriggers.highlightedTrigger);
            animator.ResetTrigger(animationTriggers.pressedTrigger);
            animator.ResetTrigger(animationTriggers.selectedTrigger);
            animator.ResetTrigger(animationTriggers.disabledTrigger);

            animator.SetTrigger(triggername);
#endif
        }

        #region ����滻
#if UNITY_EDITOR
        [UnityEditor.MenuItem("CONTEXT/Toggle/Replace To/Xeric Toggle", true)]
        private static bool ValidateReplaceToXeric(UnityEditor.MenuCommand command)
            => command.context is Toggle and not XericToggle;

        [UnityEditor.MenuItem("CONTEXT/Toggle/Replace To/Xeric Toggle", false, 10)]
        private static void ReplaceToXeric(UnityEditor.MenuCommand command)
        {
            command.ReplaceCommandContext<Toggle, XericToggle,
                (Graphic graphic, ToggleGroup group, ToggleEvent onValueChanged)>(
                o =>
                    (o.graphic, o.group, o.onValueChanged),
                (t, d) =>
                {
                    t.graphic = d.graphic;
                    t.group = d.group;
                    t.onValueChanged = d.onValueChanged;
                });
            Debug.Log("���ֶ��滻toggle�µ�Checkmark Image���ΪXericImage������Toggle����ʱȡ��ѡ���״̬�ᱣ��Ϊison״̬��");
        }

        [UnityEditor.MenuItem("CONTEXT/Toggle/Replace To/Unity Toggle", true)]
        private static bool ValidateReplaceToUnity(UnityEditor.MenuCommand command)
            => command.context is XericToggle;

        [UnityEditor.MenuItem("CONTEXT/Toggle/Replace To/Unity Toggle", false, 100)]
        private static void ReplaceToUnity(UnityEditor.MenuCommand command) 
            => command.ReplaceCommandContext<XericToggle, Toggle,
                (Graphic graphic, ToggleGroup group, ToggleEvent onValueChanged)>(
                o =>
                    (o.graphic, o.group, o.onValueChanged),
                (t, d) =>
                {
                    t.graphic = d.graphic;
                    t.group = d.group;
                    t.onValueChanged = d.onValueChanged;
                });
#endif
        #endregion
        
    }
}
