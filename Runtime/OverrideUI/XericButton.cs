using TMPro;
using UnityEngine;
using UnityEngine.UI;
using XericUI.Helper;

namespace XericUI.OverrideUI
{
    public class XericButton : Button
    {
        
#if dUI_TextMeshPro 
        public TMP_Text m_text;

        public string TextContent
        {
            get => m_text != null ? m_text.text ?? string.Empty : string.Empty;
            set { if(m_text) m_text.text = value; }
        }

        public Color TextColor
        {
            get => m_text != null ? m_text.color : Color.black;
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
        public bool m_enableTextContextTransition = false;
        public AnimationTriggers m_textTransitionContextMode = new AnimationTriggers()
        {
            normalTrigger = "button_normal",
            highlightedTrigger = "button_highlight",
            pressedTrigger = "button_pressed",
            selectedTrigger = "button_selected",
            disabledTrigger = "button_disabled",
        };
        
        public ColorBlock TextTransitionColorMode
        {
            get => m_textTransitionColorMode;
            set => m_textTransitionColorMode = value;
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
                    tintColor = Color.black;
                    textColor = Color.white;
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

            if (m_enableTextColorTransition)
                TextColor = textColor;
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

        #region 组件替换
#if UNITY_EDITOR
        [UnityEditor.MenuItem("CONTEXT/Button/Replace To XericButton", true)]
        private static bool ValidateReplaceToXeric(UnityEditor.MenuCommand command)
            => command.context is Button and not XericButton;

        [UnityEditor.MenuItem("CONTEXT/Button/Replace To XericButton", false, 10)]
        private static void ReplaceToXeric(UnityEditor.MenuCommand command) 
            => command.ReplaceCommandContext<Button, XericButton,
                (Graphic targetGraphic, ButtonClickedEvent onClick)>(
                o =>
                    (o.targetGraphic, o.onClick),
                (t, d) =>
                {
                    t.targetGraphic = d.targetGraphic;
                    t.onClick = d.onClick;
                });

        [UnityEditor.MenuItem("CONTEXT/Button/Replace To Button", true)]
        private static bool ValidateReplaceToUnity(UnityEditor.MenuCommand command)
            => command.context is XericButton;

        [UnityEditor.MenuItem("CONTEXT/Button/Replace To Button", false, 10)]
        private static void ReplaceToUnity(UnityEditor.MenuCommand command) 
            => command.ReplaceCommandContext<XericButton, Button,
                (Graphic targetGraphic, ButtonClickedEvent onClick)>(
                o =>
                    (o.targetGraphic, o.onClick),
                (t, d) =>
                {
                    t.targetGraphic = d.targetGraphic;
                    t.onClick = d.onClick;
                });
#endif
        #endregion
    }
}