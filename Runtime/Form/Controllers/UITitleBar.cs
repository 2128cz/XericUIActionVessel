#if dUI_TextMeshPro
using UI_Text = TMPro.TextMeshProUGUI;
#else
using UI_Text = UnityEngine.UI.Text;
#endif

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XericUI.Form
{
    /// <summary>
    /// 窗口标题栏 — 独立子组件，挂载在 UIFormController 的子对象上。
    /// Start 时向上查找父级 UIFormController，绑定按钮/Toggle 事件到其公开 API。
    /// </summary>
    [AddComponentMenu("Xeric UI Vessel/Form/UI Title Bar")]
    public class UITitleBar : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("控件引用")]
        [SerializeField, Tooltip("窗口图标")]
        private Image m_IconImage;

        [SerializeField, Tooltip("标题文本")]
        private UI_Text m_TitleText;

        [SerializeField, Tooltip("关闭按钮")]
        private Button m_CloseButton;

        [SerializeField, Tooltip("最大化 Toggle（isOn=全屏）")]
        private Toggle m_MaximizeToggle;

        [SerializeField, Tooltip("最小化按钮")]
        private Button m_MinimizeButton;

        [SerializeField, Tooltip("自动吸附 Toggle")]
        private Toggle m_AutoSnapToggle;

        [SerializeField, Tooltip("自动隐藏 Toggle")]
        private Toggle m_AutoHideToggle;

        private UIFormController _controller;

        private void Start()
        {
            _controller = GetComponentInParent<UIFormController>();

            if (_controller == null)
            {
                Debug.LogWarning($"[UITitleBar] 未在父级找到 UIFormController: {name}", this);
                return;
            }

            BindEvents();
            RefreshUI();
        }

        private void Update()
        {
            if (_controller == null) return;

            if (m_TitleText != null && m_TitleText.text != _controller.WindowTitle)
                m_TitleText.text = _controller.WindowTitle;

            // 同步最大化 Toggle 状态
            if (m_MaximizeToggle != null)
            {
                bool maxed = _controller.IsMaximized;
                if (m_MaximizeToggle.isOn != maxed)
                    m_MaximizeToggle.SetIsOnWithoutNotify(maxed);
            }

            // 同步图标
            if (m_IconImage != null && _controller.Entry != null)
            {
                if (_controller.Entry.Icon != null && m_IconImage.sprite != _controller.Entry.Icon)
                    m_IconImage.sprite = _controller.Entry.Icon;
                if (m_IconImage.color != _controller.Entry.TintColor)
                    m_IconImage.color = _controller.Entry.TintColor;
            }
        }

        #region 事件绑定

        private void BindEvents()
        {
            if (m_CloseButton != null)
                m_CloseButton.onClick.AddListener(() => _controller.Close());

            if (m_MaximizeToggle != null)
                m_MaximizeToggle.onValueChanged.AddListener(_ => _controller.ToggleMaximize());

            if (m_MinimizeButton != null)
                m_MinimizeButton.onClick.AddListener(() => _controller.Minimize());

            if (m_AutoSnapToggle != null)
            {
                m_AutoSnapToggle.isOn = _controller.AutoSnapEnabled;
                m_AutoSnapToggle.onValueChanged.AddListener(v => _controller.SetAutoSnapEnabled(v));
            }

            if (m_AutoHideToggle != null)
            {
                m_AutoHideToggle.isOn = _controller.AutoHideEnabled;
                m_AutoHideToggle.onValueChanged.AddListener(v => _controller.SetAutoHideEnabled(v));
            }
        }

        #endregion

        #region 拖拽转发

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_controller != null)
                _controller.OnBeginDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_controller != null)
                _controller.OnDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_controller != null)
                _controller.OnEndDrag(eventData);
        }

        #endregion

        #region UI 刷新

        private void RefreshUI()
        {
            if (m_TitleText != null)
                m_TitleText.text = _controller.WindowTitle;

            if (m_MaximizeToggle != null)
                m_MaximizeToggle.isOn = _controller.IsMaximized;

            if (m_AutoSnapToggle != null)
                m_AutoSnapToggle.isOn = _controller.AutoSnapEnabled;

            if (m_AutoHideToggle != null)
                m_AutoHideToggle.isOn = _controller.AutoHideEnabled;

            // 图标
            if (m_IconImage != null && _controller.Entry != null && _controller.Entry.Icon != null)
            {
                m_IconImage.sprite = _controller.Entry.Icon;
                m_IconImage.color = _controller.Entry.TintColor;
            }
        }

        #endregion
    }
}
