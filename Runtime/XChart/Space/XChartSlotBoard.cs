using System;
using System.Collections.Generic;
using UnityEngine;

namespace XericUI.XChart.Space
{
    /// <summary>
    /// 插槽的数据模式：决定"怎么切值 + 每个参数怎么解释"。**纯数据描述** —— 切值与解释都能离线断言。
    /// </summary>
    public enum XChartSlotMode
    {
        /// <summary>2 值/点，2 点一组：起点 → 终点（面内坐标）</summary>
        Line2D = 0,
        /// <summary>3 值/点，2 点一组：起点 → 终点（立方体单位）</summary>
        Line3D = 1,
        /// <summary>2 值＝圆心，1 值＝半径（面内圆）</summary>
        Circle2D = 2,
        /// <summary>3 值＝圆心，3 值＝法向，1 值＝半径（空间圆，法向定斜切面）</summary>
        Circle3D = 3,
        /// <summary>3 值/点，4 点一组（ST00→ST01→ST11→ST10）：空间四边形面片</summary>
        Quad3D = 4,
    }

    /// <summary>读写权限：只读槽被写要拒（可写用于"拖手柄写回去让网格跟随"）。</summary>
    public enum XChartSlotAccess
    {
        ReadOnly = 0,
        ReadWrite = 1,
    }

    /// <summary>模式的切值规则（各一条判据：一组几个值、个数不对就拒、越界就拒）。</summary>
    public static class XChartSlotModes
    {
        /// <summary>空间面片固定四个角（ST00→ST01→ST11→ST10）</summary>
        public const int QuadCorners = 4;

        /// <summary>这种模式一组要几个值；未知模式返回 0</summary>
        public static int ValuesPerGroup(XChartSlotMode mode)
        {
            switch (mode)
            {
                case XChartSlotMode.Line2D: return 4;    // 2 值 × 2 点
                case XChartSlotMode.Line3D: return 6;    // 3 值 × 2 点
                case XChartSlotMode.Circle2D: return 3;  // 圆心 2 + 半径 1
                case XChartSlotMode.Circle3D: return 7;  // 圆心 3 + 法向 3 + 半径 1
                case XChartSlotMode.Quad3D: return 12;   // 3 值 × 4 点
                default: return 0;
            }
        }

        /// <summary>一组里的点数（圆算 0，它的"点"是圆心 + 半径、另有法向）</summary>
        public static int PointsPerGroup(XChartSlotMode mode)
        {
            switch (mode)
            {
                case XChartSlotMode.Line2D:
                case XChartSlotMode.Line3D: return 2;
                case XChartSlotMode.Quad3D: return QuadCorners;
                default: return 0;
            }
        }

        /// <summary>按模式切组：值个数必须正好装下整数个组，否则拒收（越界/个数不对是一类错）</summary>
        public static bool TryGroupCount(XChartSlotMode mode, int valueCount, out int groups)
        {
            groups = 0;
            int perGroup = ValuesPerGroup(mode);
            if (perGroup <= 0 || valueCount <= 0 || valueCount % perGroup != 0) return false;
            groups = valueCount / perGroup;
            return true;
        }

        /// <summary>值必须是有限数：NaN/Inf 会让几何静默画歪，所以发布时就拒（零要说清零在哪一层）</summary>
        public static bool AllFinite(float[] values, int count)
        {
            if (values == null) return false;
            for (int i = 0; i < count; i++)
            {
                if (float.IsNaN(values[i]) || float.IsInfinity(values[i])) return false;
            }
            return true;
        }
    }

    /// <summary>
    /// 一个插槽的视图：值留在板子里（不复制），视图只带模式/权限/版本 + 该模式的取点方法。
    /// <b>过期视图不许写</b>：板子版本前进过就说明它已经不是最新的那份。
    /// </summary>
    public struct XChartSlotView
    {
        private float[] m_Values;
        private XChartSlotBoard m_Board;
        private int m_BoardVersion;

        public string Id { get; internal set; }
        public XChartSlotMode Mode { get; internal set; }
        public XChartSlotAccess Access { get; internal set; }
        public int Version { get; internal set; }
        public int GroupCount { get; internal set; }

        internal void Bind(float[] values, XChartSlotBoard board, int boardVersion)
        {
            m_Values = values;
            m_Board = board;
            m_BoardVersion = boardVersion;
        }

        private bool InRange(int group, int point, int pointCount)
        {
            return m_Values != null && group >= 0 && group < GroupCount
                && point >= 0 && point < pointCount;
        }

        /// <summary>取第 group 组第 point 个点（2D 模式 z 补 0）</summary>
        public bool TryGetPoint(int group, int point, out Vector3 result)
        {
            result = Vector3.zero;
            int points = XChartSlotModes.PointsPerGroup(Mode);
            if (!InRange(group, point, points)) return false;
            int stride = 3;
            if (Mode == XChartSlotMode.Line2D) stride = 2;
            int baseIndex = group * XChartSlotModes.ValuesPerGroup(Mode) + point * stride;
            result = new Vector3(m_Values[baseIndex], m_Values[baseIndex + 1],
                stride == 3 ? m_Values[baseIndex + 2] : 0f);
            return true;
        }

        /// <summary>取一条线（Line2D / Line3D）</summary>
        public bool TryGetLine(int group, out Vector3 from, out Vector3 to)
        {
            from = Vector3.zero;
            to = Vector3.zero;
            if (Mode != XChartSlotMode.Line2D && Mode != XChartSlotMode.Line3D) return false;
            return TryGetPoint(group, 0, out from) && TryGetPoint(group, 1, out to);
        }

        /// <summary>取一个圆（Circle2D 的法向固定 +Z；Circle3D 用法向定斜切面）</summary>
        public bool TryGetCircle(int group, out Vector3 center, out Vector3 normal, out float radius)
        {
            center = Vector3.zero;
            normal = Vector3.forward;
            radius = 0f;
            if (group < 0 || group >= GroupCount || m_Values == null) return false;

            if (Mode == XChartSlotMode.Circle2D)
            {
                int i = group * 3;
                center = new Vector3(m_Values[i], m_Values[i + 1], 0f);
                radius = m_Values[i + 2];
                return true;
            }
            if (Mode == XChartSlotMode.Circle3D)
            {
                int i = group * 7;
                center = new Vector3(m_Values[i], m_Values[i + 1], m_Values[i + 2]);
                normal = new Vector3(m_Values[i + 3], m_Values[i + 4], m_Values[i + 5]);
                radius = m_Values[i + 6];
                return true;
            }
            return false;
        }

        /// <summary>取一个面片（Quad3D），角序固定 ST00→ST01→ST11→ST10</summary>
        public bool TryGetQuad(int group, out Vector3 cornerST00, out Vector3 cornerST01,
            out Vector3 cornerST11, out Vector3 cornerST10)
        {
            cornerST00 = Vector3.zero;
            cornerST01 = Vector3.zero;
            cornerST11 = Vector3.zero;
            cornerST10 = Vector3.zero;
            if (Mode != XChartSlotMode.Quad3D) return false;
            return TryGetPoint(group, 0, out cornerST00)
                && TryGetPoint(group, 1, out cornerST01)
                && TryGetPoint(group, 2, out cornerST11)
                && TryGetPoint(group, 3, out cornerST10);
        }

        /// <summary>能不能写：只读槽、或视图已经过期（板子版本前进过）都不行</summary>
        public bool CanWrite => m_Board != null && Access == XChartSlotAccess.ReadWrite
            && m_BoardVersion == m_Board.Version && m_Values != null;

        /// <summary>把一条线写回去（可拖动的手柄就是靠它反写网格）；写成功会推板子版本，别的消费者据此重建</summary>
        public bool TryWriteLine(int group, Vector3 from, Vector3 to)
        {
            if (!CanWrite) return false;
            if (Mode != XChartSlotMode.Line2D && Mode != XChartSlotMode.Line3D) return false;
            if (group < 0 || group >= GroupCount) return false;

            int stride = Mode == XChartSlotMode.Line2D ? 2 : 3;
            int baseIndex = group * XChartSlotModes.ValuesPerGroup(Mode);
            WritePoint(baseIndex, from, stride);
            WritePoint(baseIndex + stride, to, stride);
            m_Board.BumpVersionForWrite();
            m_BoardVersion = m_Board.Version;   // 写成功之后自己也跟着前进
            return true;
        }

        private void WritePoint(int index, Vector3 point, int stride)
        {
            m_Values[index] = point.x;
            m_Values[index + 1] = point.y;
            if (stride == 3) m_Values[index + 2] = point.z;
        }
    }

    /// <summary>
    /// 插槽板：定义网格的工具把自己的一组组标定顶点放上来，其余工具按 id 取用。
    /// <b>几何的通路，不碰数据</b>（数据仍走 IXChartDataSource）。
    /// </summary>
    public interface IXChartSlotBoard
    {
        /// <summary>板子版本：任何一次成功发布/写回都会前进；消费者据此判断要不要重建</summary>
        int Version { get; }

        /// <summary>发布的槽数（报表用）</summary>
        int SlotCount { get; }

        bool Publish(string id, XChartSlotMode mode, XChartSlotAccess access, float[] values, out string reason);
        bool TryRead(string id, out XChartSlotView view);

        /// <summary>
        /// 第 <paramref name="index"/> 个已发布槽的 id 与模式。<b>按索引取、不给集合</b>：
        /// 这份清单只给面板做"已发布插槽"下拉用（每帧取一次不能分配）。
        /// </summary>
        bool TryGetSlotAt(int index, out string id, out XChartSlotMode mode);

        /// <summary>
        /// 撤下一个插槽：从这一刻起<b>这里没有几何可吸附</b>。真撤掉了返回 true，不在板上返回 false。
        /// </summary>
        bool Unpublish(string id);

        void Clear();
    }

    /// <inheritdoc />
    public sealed class XChartSlotBoard : IXChartSlotBoard
    {
        private sealed class Entry
        {
            public string Id;
            public XChartSlotMode Mode;
            public XChartSlotAccess Access;
            public float[] Values;
            public int Version;
        }

        private readonly List<Entry> m_Entries = new List<Entry>(8);

        public int Version { get; private set; }
        public int SlotCount => m_Entries.Count;

        /// <summary>发布（同 id 覆盖）。拒收：空值、个数不是整数组、含 NaN/Inf、未知模式。</summary>
        public bool Publish(string id, XChartSlotMode mode, XChartSlotAccess access, float[] values, out string reason)
        {
            reason = null;
            if (string.IsNullOrEmpty(id)) { reason = "插槽 id 为空"; return false; }

            int groups;
            if (!XChartSlotModes.TryGroupCount(mode, values == null ? 0 : values.Length, out groups))
            {
                reason = "值个数不是这种模式的整数组：" + (values == null ? 0 : values.Length);
                return false;
            }
            if (!XChartSlotModes.AllFinite(values, values.Length)) { reason = "值里有 NaN/Inf"; return false; }

            var entry = Find(id);
            if (entry == null)
            {
                entry = new Entry { Id = id };
                m_Entries.Add(entry);
            }
            entry.Mode = mode;
            entry.Access = access;
            if (entry.Values == null || entry.Values.Length != values.Length) entry.Values = new float[values.Length];
            Array.Copy(values, entry.Values, values.Length);
            Version++;
            entry.Version = Version;
            return true;
        }

        public bool TryRead(string id, out XChartSlotView view)
        {
            view = default(XChartSlotView);
            var entry = Find(id);
            if (entry == null) return false;

            view.Id = entry.Id;
            view.Mode = entry.Mode;
            view.Access = entry.Access;
            view.Version = entry.Version;
            XChartSlotModes.TryGroupCount(entry.Mode, entry.Values.Length, out int groups);
            view.GroupCount = groups;
            view.Bind(entry.Values, this, Version);
            return true;
        }

        public void Clear()
        {
            if (m_Entries.Count == 0) return;
            m_Entries.Clear();
            Version++;
        }

        /// <summary>
        /// 撤下一个插槽（不在板上就没什么可撤 ⇒ false，并且不推版本）。
        ///
        /// <para><b>为什么板子必须有这一面</b>：只有 <see cref="Publish"/>（同 id 覆盖）的话，
        /// 定义者说得出"这里的线变成这一批"，却说不出"<b>这里不再有线</b>"——上一轮那批值会一直留在板上，
        /// 消费者照吸不误。本轮把格线收成一个作者之后，这件事必须有出口：网格掩码筛掉某个面、
        /// 或者整个网格元素被删掉，那些面的格线插槽就得撤下来，否则"关掉一边、另一边还跟着"
        /// 只是换到插槽那一层复发（规范 §22）。空数组发布是<em>拒</em>的（组数必须是正数，
        /// 见 <see cref="XChartSlotModes.TryGroupCount"/>），所以"撤下"只能是一个显式动作。</para>
        /// </summary>
        public bool Unpublish(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            for (int i = 0; i < m_Entries.Count; i++)
            {
                if (m_Entries[i].Id != id) continue;
                m_Entries.RemoveAt(i);
                Version++;
                return true;
            }
            return false;
        }

        public bool TryGetSlotAt(int index, out string id, out XChartSlotMode mode)
        {
            id = null;
            mode = default(XChartSlotMode);
            if (index < 0 || index >= m_Entries.Count) return false;

            id = m_Entries[index].Id;
            mode = m_Entries[index].Mode;
            return true;
        }

        private Entry Find(string id)
        {
            for (int i = 0; i < m_Entries.Count; i++)
            {
                if (string.Equals(m_Entries[i].Id, id, StringComparison.Ordinal)) return m_Entries[i];
            }
            return null;
        }

        /// <summary>写回也算"内容变了"：推版本号，别的消费者下一帧会重建</summary>
        internal void BumpVersionForWrite() => Version++;
    }
}