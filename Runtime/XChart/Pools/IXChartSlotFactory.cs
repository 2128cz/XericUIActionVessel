namespace XericUI.XChart.Pools
{
    /// <summary>
    /// 槽位工厂。<b>池的调度器只认这一个接口</b>，所以调度算法是纯托管的，能在离线探针里
    /// 用假工厂跑（规范 <c>Documentation/XChart-RenderFramework.md</c> §5.3）。
    ///
    /// <para><c>host</c> 对调度器是透明引用，只有实现认识它（运行时里是标签宿主 RectTransform）。
    /// 把 host 交给 <see cref="Assign"/> 与 <see cref="IsUsable"/>，是为了让"挂在哪"成为有效性判据
    /// 的一部分：父节点不对就是被人挪走了（或者宿主节点随域重载没了），必须丢弃重补，
    /// 而不是继续画在一个来历不明的位置。</para>
    /// </summary>
    public interface IXChartSlotFactory<TSlot>
    {
        /// <summary>
        /// 取一个可用槽位：工厂自己的空闲表里有就复用，没有就新建（新建件的 hideFlags 由实现负责）。
        /// 给不出来就返回 null——调度器会保持该 owner 为脏，下一帧再试，不会静默少画。
        /// </summary>
        TSlot Acquire();

        /// <summary>
        /// 真正归还：停用并放回工厂的空闲表。
        /// <b>只在顺位复用之后还有剩余时调用</b>——抽出来的槽位默认先在本帧回收队列里等别人认领。
        /// </summary>
        void Release(TSlot slot);

        /// <summary>发落给某个 owner：挂到 host 下并置为激活</summary>
        void Assign(TSlot slot, object host);

        /// <summary>§5.3 的合取判据：任一条件不满足就返回 false（调度器随即丢弃它并补新的）</summary>
        bool IsUsable(TSlot slot, object host);

        /// <summary>
        /// 硬销毁池的一切（<c>chart.RebuildAll()</c> 与调试菜单走这一条）。
        /// 调度器只管列表，只有实现知道"件"是什么、池根挂在哪——所以销毁也归它。
        /// </summary>
        void DestroyAll();
    }
}
