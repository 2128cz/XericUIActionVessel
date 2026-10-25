using UnityEngine;

namespace XericUI.XChart
{
    /// <summary>
    /// 元素参数"从数据哪来"的翻译：<b>记录下标 + 维度下标</b> → 一个几何通道的值。
    ///
    /// <para><b>顺序只有一个权威</b>（他 2026-10-24 的要求："所有工具访问数据时，顺序、方向、索引、
    /// 尺寸、内容都是一样的"）：记录下标 <c>0</c> 就是表里最早的、<c>n-1</c> 是最新的，
    /// 和数据窗口显示的行号<b>同一套下标</b>。这里<b>不提供</b>"往回数第几条"这种相对量——
    /// 它让同一份数据在窗口里是第 1 条、在工具里变成第 n-2 条（那次现场就是它：
    /// 圆柱默认取"上一条→最新"，于是他改第 1 条时圆柱不动，而末两条相等时两端合并）。</para>
    ///
    /// <para><b>多记录一图元走"编组"</b>：组 <c>g</c> 覆盖 <c>[g*size, g*size+size-1]</c>，
    /// 组与组<b>严格不重叠、也不把上一组并进来</b>；凑不满一组的尾巴<b>不画</b>
    /// （宁可少画一个，也不要画一个用错记录的）。这样 32 条记录、组大小 2 ⇒ 16 个图元。</para>
    /// </summary>
    public static class XChartBindingMath
    {
        /// <summary>能凑出几个完整组：<c>记录数 / 组大小</c>（向下取整，尾巴不算）。</summary>
        public static int CompleteGroupCount(int recordCount, int groupSize)
        {
            if (groupSize < 2 || recordCount < groupSize) return 0;
            return recordCount / groupSize;
        }

        /// <summary>
        /// 组 <paramref name="groupIndex"/> 的首尾记录下标（<b>绝对下标</b>，组内 = 首…尾）。
        /// 尾巴凑不满一组时返回 false。
        /// </summary>
        public static bool TryResolveGroup(int recordCount, int groupIndex, int groupSize,
            out int firstRecord, out int lastRecord)
        {
            firstRecord = lastRecord = -1;
            if (groupSize < 2 || groupIndex < 0) return false;

            int first = groupIndex * groupSize;
            int last = first + groupSize - 1;
            if (last >= recordCount) return false;

            firstRecord = first;
            lastRecord = last;
            return true;
        }

        /// <summary>
        /// 三个维度 + <b>一条指定的记录</b> → 一个位置（原始数值，还在数据口径里）。
        /// 下标越界、维度越界、值不是有限数一律 false：<b>宁可这一帧不画，也不往几何里塞 NaN</b>
        /// ——NaN 进去以后是静默毁掉整条管线，不是画歪。
        /// </summary>
        public static bool TryResolvePosition(IXChartDataSource data, int recordIndex,
            int dimensionX, int dimensionY, int dimensionZ, out Vector3 position)
        {
            position = Vector3.zero;
            if (data == null) return false;
            if (recordIndex < 0 || recordIndex >= data.RecordCount) return false;
            if (dimensionX < 0 || dimensionY < 0 || dimensionZ < 0) return false;

            int dimensions = data.NumericDimensionCount;
            if (dimensionX >= dimensions || dimensionY >= dimensions || dimensionZ >= dimensions) return false;

            double x = data.GetValueAt(recordIndex, dimensionX);
            double y = data.GetValueAt(recordIndex, dimensionY);
            double z = data.GetValueAt(recordIndex, dimensionZ);
            if (!IsFinite(x) || !IsFinite(y) || !IsFinite(z)) return false;

            position = new Vector3((float)x, (float)y, (float)z);
            return true;
        }

        /// <summary>
        /// 数据值 → 立方体单位（±1 就是盒子边界）。<b>值域退化（两个端点相等或非有限）时返回 false</b>：
        /// 那一刻没有"数据在哪儿"可言，硬画出来是一个假位置，不如让调用方这一帧不画。
        /// 不裁剪到 ±1——超出盒子的值是数据的事实，裁了就看不出来了。
        /// </summary>
        public static bool TryNormalize(double value, double minimum, double maximum, out float unit)
        {
            unit = 0f;
            if (!IsFinite(value) || !IsFinite(minimum) || !IsFinite(maximum)) return false;
            double span = maximum - minimum;
            if (span <= 0d) return false;

            unit = (float)((value - minimum) / span * 2d - 1d);
            return true;
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}