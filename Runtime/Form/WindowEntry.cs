using System.Collections.Generic;
using UnityEngine;

namespace XericUI.Form
{
    /// <summary>
    /// 窗口数据描述对象 — 由 UIFormManager 创建和持有。
    /// 描述一个窗口实例的标识信息、运行时引用、显示状态和层级关系。
    /// </summary>
    public class WindowEntry
    {
        #region 基本标识

        /// <summary>唯一 ID（GUID）</summary>
        public string Id { get; }

        /// <summary>窗口标题</summary>
        public string Title;

        /// <summary>窗口图标（任务栏和标题栏使用）</summary>
        public Sprite Icon;

        /// <summary>任务栏图标着色</summary>
        public Color TintColor = Color.white;

        #endregion

        #region 实例引用

        /// <summary>运行时窗口实例</summary>
        public UIFormController Controller;

        /// <summary>窗口预制体引用</summary>
        public RectTransform Prefab;

        #endregion

        #region 显示状态

        /// <summary>当前窗口状态</summary>
        public WindowState State;

        /// <summary>是否在任务栏上显示图标</summary>
        public bool ShowInTaskBar = true;

        #endregion

        #region 层级关系

        /// <summary>父窗口 Entry（关闭父窗口时级联关闭子窗口）</summary>
        public WindowEntry ParentEntry { get; private set; }

        /// <summary>子窗口 Entry 列表</summary>
        public readonly List<WindowEntry> ChildEntries = new List<WindowEntry>();

        /// <summary>添加子窗口依赖</summary>
        public void AddChild(WindowEntry child)
        {
            if (child == null || ChildEntries.Contains(child)) return;
            child.ParentEntry = this;
            ChildEntries.Add(child);
        }

        /// <summary>移除子窗口依赖</summary>
        public void RemoveChild(WindowEntry child)
        {
            if (child == null) return;
            child.ParentEntry = null;
            ChildEntries.Remove(child);
        }

        #endregion

        #region 进度

        /// <summary>任务栏进度 (0-1)</summary>
        public float Progress;

        /// <summary>是否在任务栏显示进度条</summary>
        public bool ShowProgress;

        #endregion

        #region 构造

        public WindowEntry(string id)
        {
            Id = id ?? System.Guid.NewGuid().ToString();
        }

        public WindowEntry() : this(null) { }

        #endregion
    }

    #region 委托

    /// <summary>窗口 Entry 事件委托</summary>
    public delegate void WindowEntryEventHandler(WindowEntry entry);

    #endregion
}
