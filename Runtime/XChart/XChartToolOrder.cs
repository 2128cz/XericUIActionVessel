namespace XericUI.XChart
{
    /// <summary>
    /// 工具的<b>生命周期分组</b>（他 2026-10-23 的口径："背景（背景网格）/ 前景（参数点位、轴线标尺、
    /// tooltip、其他可互动元素）"）。<b>定义 → 背景 → 前景</b> 依次跑更新：定义网格的那个工具先算出
    /// 各维范围、把插槽发布出去，同一帧里轴线和数据图元就能取到——这是"注册先后不影响正确性"的前提。
    ///
    /// <para><b>分组不是绘制序</b>：画家序由 <see cref="XChartToolBase.DrawOrder"/> 决定（§11.4：
    /// "轴线的线可以绘制在点的上面"）。两者分开，是因为"谁先算"和"谁盖住谁"是两件事。</para>
    ///
    /// <para>分组是<b>类型标记</b>（工具自己声明），不入序列化——省得又多一个要调的旋钮。</para>
    /// </summary>
    public enum XChartToolCategory
    {
        /// <summary>定义几何/范围/插槽的工具（空间、网格背景、立方体背景）：最先跑</summary>
        Definition = 0,

        /// <summary>只画背景、不定义几何的工具（今天还没有工具落在这里，位置先留着）</summary>
        Background = 1,

        /// <summary>数据图元与可交互元素（散点、箭头、圆柱、轴线、tooltip 之类）：最后跑</summary>
        Foreground = 2,
    }

    /// <summary>
    /// 工具顺序的<b>唯一规则</b>：先按分组（定义→背景→前景），再按各自的值，值相等时按<b>注册序</b>
    /// （先注册的在前）——最后这一条是稳定性的来源，缺了它"值相同"的两个工具的相对次序就看运气，
    /// 而"看运气"的次序在画面上就是"有时点在上面、有时轴在上面"。
    ///
    /// <para>抽成纯算术是为了离线判：次序错了不会报错，只是有人被别人盖住。</para>
    /// </summary>
    public static class XChartToolOrder
    {
        /// <summary><paramref name="a"/> 是否应该排在 <paramref name="b"/> 前面（−1 前，0 同序，1 后）</summary>
        public static int Compare(XChartToolCategory aCategory, int aOrder, int aRegistration,
            XChartToolCategory bCategory, int bOrder, int bRegistration)
        {
            if (aCategory != bCategory) return (int)aCategory < (int)bCategory ? -1 : 1;
            if (aOrder != bOrder) return aOrder < bOrder ? -1 : 1;
            if (aRegistration != bRegistration) return aRegistration < bRegistration ? -1 : 1;
            return 0;
        }

        /// <summary>
        /// 按 <see cref="Compare"/> 做<b>稳定插入排序</b>（就地，不分配）：条目数有上限（工具几十个），
        /// 且它跑在渲染/初始化路径上。返回是否真的动过（报表读数用：动没动能看出旋钮生效没）。
        /// </summary>
        public static bool Sort(int count, XChartToolCategory[] categories, int[] orders, int[] registrations,
            int[] output)
        {
            if (count <= 0) return false;
            for (int i = 0; i < count; i++) output[i] = i;

            bool moved = false;
            for (int i = 1; i < count; i++)
            {
                int key = output[i];
                int j = i - 1;
                while (j >= 0 && Compare(categories[output[j]], orders[output[j]], registrations[output[j]],
                           categories[key], orders[key], registrations[key]) > 0)
                {
                    output[j + 1] = output[j];
                    moved = true;
                    j--;
                }
                output[j + 1] = key;
            }
            return moved;
        }
    }
}