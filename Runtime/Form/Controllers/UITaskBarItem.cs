using UnityEngine;
using UnityEngine.UI;

namespace XericUI.Form
{
    /// <summary>
    /// 任务栏图标项 — 显示单个窗口的图标、标题、进度条和聚焦高亮。
    /// 点击时通过 UIFormManager 切换窗口显示/隐藏并置顶。
    /// </summary>
    public class UITaskBarItem : MonoBehaviour
    {
        [Header("控件引用")]
        [SerializeField, Tooltip("窗口图标")]
        private Image m_Icon;

        [SerializeField, Tooltip("窗口标题文本")]
        private Text m_TitleText;

        [SerializeField, Tooltip("进度条 Image（fillAmount）")]
        private Image m_ProgressBar;

        [SerializeField, Tooltip("聚焦高亮 Image")]
        private Image m_Highlight;

        [SerializeField, Tooltip("按钮组件")]
        private Button m_Button;

        private WindowEntry _entry;
        private UIFormManager _manager;

        public WindowEntry Entry => _entry;

        #region 初始化

        public void Bind(WindowEntry entry, UIFormManager manager)
        {
            _entry = entry;
            _manager = manager;

            if (m_Icon != null && entry.Icon != null)
                m_Icon.sprite = entry.Icon;

            if (m_Icon != null)
                m_Icon.color = entry.TintColor;

            if (m_TitleText != null)
                m_TitleText.text = entry.Title;

            if (m_Button != null)
                m_Button.onClick.AddListener(OnClick);

            RefreshHighlight();
            RefreshProgress();
        }

        private void OnDestroy()
        {
            if (m_Button != null)
                m_Button.onClick.RemoveListener(OnClick);
        }

        #endregion

        #region 交互

        private void OnClick()
        {
            if (_entry == null || _manager == null) return;

            _manager.ToggleWindow(_entry.Id);
            _manager.MoveToFront(_entry.Id);
        }

        #endregion

        #region 每帧同步

        private void Update()
        {
            if (_entry == null) return;

            if (m_TitleText != null && m_TitleText.text != _entry.Title)
                m_TitleText.text = _entry.Title;

            if (m_Icon != null && m_Icon.color != _entry.TintColor)
                m_Icon.color = _entry.TintColor;

            RefreshProgress();
            RefreshHighlight();
        }

        private void RefreshProgress()
        {
            if (m_ProgressBar == null) return;
            m_ProgressBar.fillAmount = _entry.Progress;
            m_ProgressBar.gameObject.SetActive(_entry.ShowProgress && _entry.Progress > 0f);
        }

        private void RefreshHighlight()
        {
            if (m_Highlight == null || _manager == null) return;
            var focused = _manager.GetFocusedEntry();
            m_Highlight.gameObject.SetActive(focused == _entry);
        }

        #endregion
    }
}
