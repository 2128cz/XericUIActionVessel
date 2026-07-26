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
    /// Start 时向上查找父级 UIFormController，绑定按钮事件到其公开 API。
    /// 与窗口完全解耦：Controller 不关心标题栏是否存在。
    /// </summary>
    [AddComponentMenu("Xeric UI Vessel/Form/UI Title Bar")]
    public class UITitleBar : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("控件引用")]
        [SerializeField, Tooltip("标题文本")]
        private UI_Text m_TitleText;

        [SerializeField, Tooltip("关闭按钮")]
        private Button m_CloseButton;

        [SerializeField, Tooltip("最大化按钮")]
        private Button m_MaximizeButton;

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

            // 实时同步标题（可能由外部修改）
            if (m_TitleText != null && m_TitleText.text != _controller.WindowTitle)
                m_TitleText.text = _controller.WindowTitle;
        }

        #region 事件绑定

        private void BindEvents()
        {
            if (m_CloseButton != null)
                m_CloseButton.onClick.AddListener(() => _controller.Close());

            if (m_MaximizeButton != null)
                m_MaximizeButton.onClick.AddListener(() => _controller.ToggleMaximize());

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

            if (m_AutoSnapToggle != null)
                m_AutoSnapToggle.isOn = _controller.AutoSnapEnabled;

            if (m_AutoHideToggle != null)
                m_AutoHideToggle.isOn = _controller.AutoHideEnabled;
        }

        #endregion
    }
}
