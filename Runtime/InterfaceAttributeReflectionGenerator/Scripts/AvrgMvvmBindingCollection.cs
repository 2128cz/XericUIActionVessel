using System.Collections;
using System.Collections.Generic;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 绑定集合 — 统一管理所有 AvrgMvvmBinding，提供字符串索引器和前缀树快速查找
    /// </summary>
    public class AvrgMvvmBindingCollection : IEnumerable<AvrgMvvmBinding>
    {
        private List<AvrgMvvmBinding> _bindings = new List<AvrgMvvmBinding>();
        private AvrgMvvmScheduler _scheduler;
        private AvrgBindingTrie _trie;

        /// <summary>
        /// 绑定的元数据集引用（可选）
        /// </summary>
        public AvrgMemberMetaDataSet SourceMetaDataSet { get; set; }

        /// <summary>
        /// 绑定数量
        /// </summary>
        public int Count => _bindings.Count;

        /// <summary>
        /// 调度器引用（在 BuildMDVM 时设置）
        /// </summary>
        public AvrgMvvmScheduler Scheduler => _scheduler;

        /// <summary>
        /// 添加绑定
        /// </summary>
        public void Add(AvrgMvvmBinding binding)
        {
            _bindings.Add(binding);
        }

        /// <summary>
        /// 设置前缀树索引（由 BuildMDVM 构建后注入）
        /// </summary>
        internal void SetTrie(AvrgBindingTrie trie)
        {
            _trie = trie;
        }

        /// <summary>
        /// 获取指定索引的绑定
        /// </summary>
        public AvrgMvvmBinding this[int index] => _bindings[index];

        /// <summary>
        /// 通过成员名访问绑定（如 bindingCollection["speed"]）
        /// get: 从数据源读取当前值
        /// set: 走完整的 MVVM 管线：WriteFromData → MarkDataDirty → Flush → UI 同步
        /// </summary>
        public object this[string memberName]
        {
            get
            {
                var binding = FindByMemberName(memberName);
                return binding?.ReadFromDataSource();
            }
            set
            {
                var binding = FindByMemberName(memberName);
                if (binding != null && _scheduler != null)
                {
                    binding.WriteFromData(value);
                    _scheduler.MarkDataDirty(binding);
                    _scheduler.Flush();
                }
            }
        }

        /// <summary>
        /// 获取指定成员名称的绑定（优先查 Trie，回退线性查找）
        /// </summary>
        public AvrgMvvmBinding FindByMemberName(string memberName)
        {
            if (_trie != null)
            {
                var found = _trie.Find(memberName) ?? _trie.FindByMemberName(memberName);
                if (found != null) return found;
            }
            return _bindings.Find(b => b.MetaData?.MemberName == memberName);
        }

        /// <summary>
        /// 获取指定 Tag 类型的绑定列表
        /// </summary>
        public List<AvrgMvvmBinding> FindByTag(XuiavrgFieldTag tag)
        {
            return _bindings.FindAll(b => (b.MetaData?.Tag & tag) == tag);
        }

        /// <summary>
        /// 附加调度器
        /// </summary>
        internal void AttachScheduler(AvrgMvvmScheduler scheduler)
        {
            _scheduler = scheduler;
        }

        /// <summary>
        /// 触发调度器 Flush，处理所有脏标记
        /// </summary>
        public void Flush()
        {
            _scheduler?.Flush();
        }

        /// <summary>
        /// 清空所有绑定
        /// </summary>
        public void Clear()
        {
            _bindings.Clear();
            _trie?.Clear();
            _scheduler?.Clear();
        }

        public IEnumerator<AvrgMvvmBinding> GetEnumerator() => _bindings.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
