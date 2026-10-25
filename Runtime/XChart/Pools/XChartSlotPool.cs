using System.Collections.Generic;

namespace XericUI.XChart.Pools
{
    /// <summary>
    /// 槽位池调度器（规范 §5.1/§5.2/§5.3）。
    ///
    /// <para><b>纯托管、不引用 UnityEngine</b>：调度是唯一一份实现，判据靠假工厂在离线探针里跑，
    /// 不需要编辑器在场也能证明"先释放、顺位优先、池兜底"这套顺序真的在生效。</para>
    ///
    /// <para>申请单位是<b>一批</b>：<see cref="Request"/> 只把 owner 标脏，分配一律推迟到
    /// <see cref="Dispatch"/>。每个 owner 的列表被<b>整份换回</b>（换新列表实例，不原地改长度），
    /// 所以调用方拿到的旧引用不会被人悄悄改短。</para>
    /// </summary>
    public sealed class XChartSlotPool<TSlot>
    {
        private sealed class OwnerRecord
        {
            public int Index;
            public object Host;
            public int Requested;
            public bool Dirty;
            public List<TSlot> Slots = new List<TSlot>();
        }

        private readonly IXChartSlotFactory<TSlot> m_Factory;

        /// <summary>注册顺序就是次级排序键：申请数相同时先注册者顺位在前，调度可重放</summary>
        private readonly List<OwnerRecord> m_Order = new List<OwnerRecord>();
        private readonly Dictionary<object, OwnerRecord> m_Lookup = new Dictionary<object, OwnerRecord>();
        private readonly List<OwnerRecord> m_Round = new List<OwnerRecord>();
        private readonly List<TSlot> m_Recycle = new List<TSlot>();
        private static readonly IReadOnlyList<TSlot> k_Empty = new TSlot[0];

        private int m_DirtyCount;

        // ── 计数器：都是累计值，探针取两次快照的差来判决 ──────────────
        public int DispatchCount { get; private set; }
        /// <summary>从本帧回收队列里顺位取用的次数（不是向工厂要的）</summary>
        public int ReuseCount { get; private set; }
        /// <summary>向工厂 Acquire 的次数（池兜底 + 新建）</summary>
        public int AcquireCount { get; private set; }
        /// <summary>真正归还工厂的次数（只等于回收队列的剩余）</summary>
        public int ReleaseCount { get; private set; }
        /// <summary>§5.3 校验丢弃的槽位数（域重载、宿主重建都会在这里露头）</summary>
        public int DroppedCount { get; private set; }

        public int OwnerCount => m_Order.Count;

        /// <summary>调度收敛了没有：为 false 说明还有 owner 在等件（工厂给不出或宿主没就绪）</summary>
        public bool IsIdle => m_DirtyCount == 0;

        public XChartSlotPool(IXChartSlotFactory<TSlot> factory)
        {
            m_Factory = factory;
        }

        /// <summary>
        /// 登记一批需求（<paramref name="count"/> 是本帧要用的总数，不是增量）。只置脏，不分配。
        /// <paramref name="host"/> 变了也要重排——域重载后宿主是新实例，旧槽位的父节点就对不上了。
        /// </summary>
        public void Request(object owner, object host, int count)
        {
            if (owner == null) return;
            if (count < 0) count = 0;

            OwnerRecord record;
            if (!m_Lookup.TryGetValue(owner, out record))
            {
                record = new OwnerRecord { Index = m_Order.Count };
                m_Lookup[owner] = record;
                m_Order.Add(record);
            }

            if (record.Requested == count && ReferenceEquals(record.Host, host) && !record.Dirty) return;
            record.Requested = count;
            record.Host = host;
            MarkDirty(record);
        }

        public void Forget(object owner)
        {
            if (owner == null) return;
            OwnerRecord record;
            if (!m_Lookup.TryGetValue(owner, out record)) return;
            m_Lookup.Remove(owner);
            m_Order.Remove(record);
            // 它手上的件全数归还池，不然这些实例就没人认领了
            for (int i = 0; i < record.Slots.Count; i++)
            {
                m_Factory.Release(record.Slots[i]);
                ReleaseCount++;
            }
            record.Slots.Clear();
            if (record.Dirty)
            {
                record.Dirty = false;
                m_DirtyCount--;
            }
        }

        /// <summary>owner 当前持有的槽位（长度就是已发落的数量，申请数看 <see cref="RequestedCount"/>）</summary>
        public IReadOnlyList<TSlot> SlotsOf(object owner)
        {
            OwnerRecord record;
            return m_Lookup.TryGetValue(owner, out record) ? record.Slots : k_Empty;
        }

        public int RequestedCount(object owner)
        {
            OwnerRecord record;
            return m_Lookup.TryGetValue(owner, out record) ? record.Requested : 0;
        }

        public int HeldCount(object owner)
        {
            OwnerRecord record;
            return m_Lookup.TryGetValue(owner, out record) ? record.Slots.Count : 0;
        }

        /// <summary>
        /// 一次调度：校验 → 排序 → 先释放 → 再补充（顺位优先，池兜底）→ 盈余才归还 → 换回列表。
        /// 返回本次是否真的动过（没脏也没丢弃时是 O(槽位总数) 的一次校验而已）。
        /// </summary>
        public bool Dispatch()
        {
            DispatchCount++;

            // 0) 有效性校验：每次调度都做，不只对脏 owner（§5.3）
            for (int i = 0; i < m_Order.Count; i++)
            {
                OwnerRecord record = m_Order[i];
                int held = record.Slots.Count;
                if (held == 0) continue;

                int kept = 0;
                for (int s = 0; s < held; s++)
                {
                    TSlot slot = record.Slots[s];
                    if (m_Factory.IsUsable(slot, record.Host)) record.Slots[kept++] = slot;
                    else DroppedCount++;
                }

                if (kept == held) continue;
                record.Slots.RemoveRange(kept, held - kept);
                MarkDirty(record);
            }

            // 1) 脏 owner 按申请数从大到小排序，让先申请者顺位拿到回收件
            m_Round.Clear();
            for (int i = 0; i < m_Order.Count; i++)
            {
                OwnerRecord record = m_Order[i];
                if (!record.Dirty) continue;
                // 宿主还没就绪又要点件：这轮整个跳过，脏着下一帧再试（不然会反复造出无主的件）
                if (record.Host == null && record.Requested > 0) continue;
                m_Round.Add(record);
            }
            if (m_Round.Count == 0) return false;
            m_Round.Sort(CompareByRequestDescending);

            // 2) 先处理释放：多出来的抽进本帧回收队列——这一步还不还池
            m_Recycle.Clear();
            for (int i = 0; i < m_Round.Count; i++)
            {
                OwnerRecord record = m_Round[i];
                int keep = record.Requested;
                if (record.Slots.Count <= keep) continue;
                for (int s = keep; s < record.Slots.Count; s++) m_Recycle.Add(record.Slots[s]);
                record.Slots.RemoveRange(keep, record.Slots.Count - keep);
            }

            // 3) 再补充：先吃回收队列（顺位复用），队列空了才向工厂要
            for (int i = 0; i < m_Round.Count; i++)
            {
                OwnerRecord record = m_Round[i];
                var next = new List<TSlot>(record.Slots);
                while (next.Count < record.Requested)
                {
                    TSlot slot;
                    if (m_Recycle.Count > 0)
                    {
                        slot = m_Recycle[m_Recycle.Count - 1];
                        m_Recycle.RemoveAt(m_Recycle.Count - 1);
                        ReuseCount++;
                    }
                    else
                    {
                        slot = m_Factory.Acquire();
                        AcquireCount++;
                    }
                    if (slot == null) break;
                    m_Factory.Assign(slot, record.Host);
                    next.Add(slot);
                }

                record.Slots = next;
                ClearDirty(record);
                // 工厂给不出（宿主没了、创建被挡）就保持脏，下一帧重试——不静默少画
                if (next.Count < record.Requested) MarkDirty(record);
            }

            // 4) 回收队列的剩余才是真正归还
            for (int i = 0; i < m_Recycle.Count; i++)
            {
                m_Factory.Release(m_Recycle[i]);
                ReleaseCount++;
            }
            m_Recycle.Clear();
            return true;
        }

        /// <summary>
        /// 拆池：清空全部 owner 记录，再把"件"的销毁交给工厂。
        /// <b>列表在这里没意义</b>——件是别人（GameObject）造的，只有实现知道怎么硬销毁。
        /// </summary>
        public void Dispose()
        {
            m_Lookup.Clear();
            m_Order.Clear();
            m_Round.Clear();
            m_Recycle.Clear();
            m_DirtyCount = 0;
            m_Factory.DestroyAll();
        }

        private int CompareByRequestDescending(OwnerRecord a, OwnerRecord b)
        {
            if (a.Requested != b.Requested) return b.Requested.CompareTo(a.Requested);
            return a.Index.CompareTo(b.Index);
        }

        private void MarkDirty(OwnerRecord record)
        {
            if (record.Dirty) return;
            record.Dirty = true;
            m_DirtyCount++;
        }

        private void ClearDirty(OwnerRecord record)
        {
            if (!record.Dirty) return;
            record.Dirty = false;
            m_DirtyCount--;
        }
    }
}
