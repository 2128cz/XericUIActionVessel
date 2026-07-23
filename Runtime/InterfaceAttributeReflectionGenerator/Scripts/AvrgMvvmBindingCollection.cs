using System.Collections;
using System.Collections.Generic;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 绑定集合 — 统一管理所有 AvrgMvvmBinding
    /// </summary>
    public class AvrgMvvmBindingCollection : IEnumerable<AvrgMvvmBinding>
    {
        private List<AvrgMvvmBinding> _bindings = new List<AvrgMvvmBinding>();
        private AvrgMvvmScheduler _scheduler;

        /// <summary>
        /// 绑定的元数据集引用（可选）
        /// </summary>
        public AvrgMemberMetaDataSet SourceMetaDataSet { get; set; }

        /// <summary>
        /// 绑定数量
        /// </summary>
        public int Count => _bindings.Count;

        /// <summary>
        /// 添加绑定
        /// </summary>
        public void Add(AvrgMvvmBinding binding)
        {
            _bindings.Add(binding);
        }

        /// <summary>
        /// 获取指定索引的绑定
        /// </summary>
        public AvrgMvvmBinding this[int index] => _bindings[index];

        /// <summary>
        /// 获取指定成员名称的绑定
        /// </summary>
        public AvrgMvvmBinding FindByMemberName(string memberName)
        {
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
            _scheduler?.Clear();
        }

        public IEnumerator<AvrgMvvmBinding> GetEnumerator() => _bindings.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
