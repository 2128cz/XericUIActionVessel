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

        // ── 标签列（String 那一族）：与数值列同一套规则，只是占的是标签数组的格 ──
        //
        // 记录身上有两个数组：值数组的长度＝<see cref="ValueCount"/>，标签数组的长度＝
        // <see cref="LabelCount"/>。<b>标签数组的长度就是它自己的判据</b>：一条记录能写出
        // 比标签列更多的字符串，那些字符串没有一个列能叫出名字，屏幕上便凭空多出一个标签
        // （以前这里靠调用方自觉，所以导入能塞进去、窗口显示得出来、元素却读不到）。

        /// <summary>标签列的个数＝每条记录标签数组的长度（非标签列不占格）</summary>
        public static int LabelCount(IReadOnlyList<XChartDimensionDefinition> dimensions)
        {
            if (dimensions == null) return 0;
            int count = 0;
            for (int i = 0; i < dimensions.Count; i++)
                if (dimensions[i] != null && dimensions[i].Kind == XChartDimensionKind.Label) count++;
            return count;
        }

        /// <summary>
        /// 表里第 <paramref name="tableIndex"/> 行对应的<b>标签下标</b>；
        /// 那一列不占标签格（种类不是 Label）或行号越界时返回 <c>-1</c>。
        /// </summary>
        public static int LabelSlotOf(IReadOnlyList<XChartDimensionDefinition> dimensions, int tableIndex)
        {
            if (dimensions == null || tableIndex < 0 || tableIndex >= dimensions.Count) return -1;
            var target = dimensions[tableIndex];
            if (target == null || target.Kind != XChartDimensionKind.Label) return -1;

            int slot = 0;
            for (int i = 0; i < tableIndex; i++)
                if (dimensions[i] != null && dimensions[i].Kind == XChartDimensionKind.Label) slot++;
            return slot;
        }

        /// <summary>
        /// 把一条记录的标签数组按<b>标签列个数</b>定长——这是"记录标签长度"唯一的定义处：
        /// 按位置逐格搬（不是按列号搬：列号已经被调用方换算过），短的补空串、长的截掉。
        /// <b>加/删列之后重排就靠它</b>，所以各写入路径不许各自 Copy 一遍。
        /// </summary>
        public static string[] SizedLabels(IReadOnlyList<string> labels, int labelCount)
        {
            if (labelCount < 0) labelCount = 0;
            var copy = new string[labelCount];
            // 空位一律写空串，不写 null：null 与 "" 在窗口里都显示为空，却在"这一格是空的"这条判据上
            // 各答一套（导入写出的是 ""，而这里补 null，往返一次就变了样）。
            for (int i = 0; i < labelCount; i++)
                copy[i] = labels != null && i < labels.Count ? labels[i] : string.Empty;
            return copy;
        }

        /// <summary>反方向：标签下标属于表里哪一列（拿到那一列的定义）——与 <see cref="DefinitionOfValueSlot"/> 对称</summary>
        public static XChartDimensionDefinition DefinitionOfLabelSlot(
            IReadOnlyList<XChartDimensionDefinition> dimensions, int labelSlot)
        {
            if (dimensions == null || labelSlot < 0) return null;
            int slot = 0;
            for (int i = 0; i < dimensions.Count; i++)
            {
                var candidate = dimensions[i];
                if (candidate == null || candidate.Kind != XChartDimensionKind.Label) continue;
                if (slot == labelSlot) return candidate;
                slot++;
            }
            return null;
        }
    }
}
