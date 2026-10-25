using System.Collections.Generic;

namespace XericUI.XChart
{
    /// <summary>
    /// 维度表的<b>下标口径</b>：表里一行是"一列"（键、种类、显示名、范围覆盖），而每条记录的
    /// 值数组只装<b>数值列</b>——中间夹一个 Label/Time 列时，"表里第几行"和"值是第几格"会错开。
    ///
    /// <para>这条映射以前在数据模型里数一遍、在数据窗口里再数一遍，两处各数各的就会出现
    /// "窗口把刚填过数的格子显示成 <c>-</c>，导出里那两个数却还在"。规则收在这里，
    /// 模型与界面都调它，判据也只需要盯这一处（见规范 §21.7）。</para>
    /// </summary>
    public static class XChartColumnMath
    {
        /// <summary>数值列的个数＝每条记录值数组的长度（非数值列不占格）</summary>
        public static int ValueCount(IReadOnlyList<XChartDimensionDefinition> dimensions)
        {
            if (dimensions == null) return 0;
            int count = 0;
            for (int i = 0; i < dimensions.Count; i++)
                if (dimensions[i] != null && dimensions[i].Kind == XChartDimensionKind.Number) count++;
            return count;
        }

        /// <summary>
        /// 表里第 <paramref name="tableIndex"/> 行对应的<b>值下标</b>；
        /// 那一列不占值（种类不是 Number）或行号越界时返回 <c>-1</c>（调用方据此显示"没有数值"而不是猜）。
        /// </summary>
        public static int ValueSlot(IReadOnlyList<XChartDimensionDefinition> dimensions, int tableIndex)
        {
            if (dimensions == null || tableIndex < 0 || tableIndex >= dimensions.Count) return -1;
            var target = dimensions[tableIndex];
            if (target == null || target.Kind != XChartDimensionKind.Number) return -1;

            int slot = 0;
            for (int i = 0; i < tableIndex; i++)
                if (dimensions[i] != null && dimensions[i].Kind == XChartDimensionKind.Number) slot++;
            return slot;
        }

        /// <summary>
        /// 反方向：值下标 <paramref name="valueSlot"/> 属于表里哪一列（拿到那一列的定义）。
        ///
        /// <para>工具绑的"第几维"是<b>值下标</b>（它按 <c>GetValueAt(record, dimension)</c> 取数），
        /// 而范围覆盖存在<b>表里那一列</b>上。两边都按下标直接查就会在混合种类的表上错位：
        /// 表是 <c>x, tag, y</c> 时，工具的"第 1 维"是 <c>y</c>，而表里第 1 行是 <c>tag</c>。
        /// 这条函数把那一次换算固定下来（规范 §21.7）。</para>
        /// </summary>
        public static XChartDimensionDefinition DefinitionOfValueSlot(
            IReadOnlyList<XChartDimensionDefinition> dimensions, int valueSlot)
        {
            if (dimensions == null || valueSlot < 0) return null;
            int slot = 0;
            for (int i = 0; i < dimensions.Count; i++)
            {
                var candidate = dimensions[i];
                if (candidate == null || candidate.Kind != XChartDimensionKind.Number) continue;
                if (slot == valueSlot) return candidate;
                slot++;
            }
            return null;
        }
    }
}
