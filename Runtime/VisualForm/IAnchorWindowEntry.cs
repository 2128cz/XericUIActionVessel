using System.Collections.Generic;

namespace XericUI.VisualForm
{
    public interface IAnchorWindowEntry
    {
        /// <summary>
        /// 默认窗口条目列表
        /// </summary>
        public IEnumerable<WindowEntry> DefaultTypeWindowEntries { get; }

        /// <summary>
        /// 默认窗口条目数量
        /// </summary>
        public int DefaultTypeWindowEntriesCount { get; }

        /// <summary>
        /// 窗口层级变更事件 — 当某个条目的溢出状态发生变化时触发。
        /// </summary>
        /// <param name="entryIndex">发生变化的条目索引</param>
        /// <param name="state">新的溢出状态</param>
        public void OnWindowLevelChange(int entryIndex, OverflowState state);
    }

    public interface IAnchorWindowEntry<T> : IAnchorWindowEntry
        where T : WindowEntry
    {
        /// <summary>
        /// 窗口条目列表
        /// </summary>
        public IList<T> WindowEntries { get; }
    }
}