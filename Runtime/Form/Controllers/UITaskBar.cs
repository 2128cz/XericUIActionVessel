using System.Collections.Generic;
using UnityEngine;

namespace XericUI.Form
{
    /// <summary>
    /// 任务栏控制器 — 监听 UIFormManager 的窗口创建/销毁/状态变更事件，
    /// 动态创建/销毁 UITaskBarItem 图标。
    /// </summary>
    public class UITaskBar : MonoBehaviour
    {
        [Header("绑定")]
        [SerializeField, Tooltip("窗口管理器引用")]
        private UIFormManager m_Manager;

        [SerializeField, Tooltip("任务栏图标项预制体")]
        private UITaskBarItem m_ItemPrefab;

        [SerializeField, Tooltip("图标项的父容器")]
        private Transform m_ItemsContainer;

        private readonly Dictionary<string, UITaskBarItem> _items = new Dictionary<string, UITaskBarItem>();

        #region 生命周期

        private void Start()
        {
            if (m_Manager == null)
            {
                Debug.LogError("[UITaskBar] 未绑定 UIFormManager!", this);
                return;
            }

            m_Manager.OnWindowCreated += OnWindowCreated;
            m_Manager.OnWindowDestroyed += OnWindowDestroyed;
            m_Manager.OnWindowStateChanged += OnWindowStateChanged;
        }

        private void OnDestroy()
        {
            if (m_Manager != null)
            {
                m_Manager.OnWindowCreated -= OnWindowCreated;
                m_Manager.OnWindowDestroyed -= OnWindowDestroyed;
                m_Manager.OnWindowStateChanged -= OnWindowStateChanged;
            }

            foreach (var item in _items.Values)
            {
                if (item != null)
                    Destroy(item.gameObject);
            }
            _items.Clear();
        }

        #endregion

        #region 事件回调

        private void OnWindowCreated(WindowEntry entry)
        {
            if (entry == null || !entry.ShowInTaskBar) return;
            if (m_ItemPrefab == null || m_ItemsContainer == null) return;

            var item = Instantiate(m_ItemPrefab, m_ItemsContainer);
            item.Bind(entry, m_Manager);
            _items[entry.Id] = item;
        }

        private void OnWindowDestroyed(WindowEntry entry)
        {
            if (entry == null) return;

            if (_items.TryGetValue(entry.Id, out var item))
            {
                if (item != null)
                    Destroy(item.gameObject);
                _items.Remove(entry.Id);
            }
        }

        private void OnWindowStateChanged(WindowEntry entry)
        {
            if (entry == null) return;

            // 状态变更时触发 Repaint（进度、高亮等由 Item 自己的 Update 处理）
        }

        #endregion
    }
}
