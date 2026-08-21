using System;
using System.Collections.Generic;
using UnityEngine;

namespace XericUI.XChart
{
    /// <summary>
    /// 3D 散点图数据模型。
    ///
    /// 存储 XYZ 三维坐标点数据，支持脏标记机制。
    /// 每个数据点包含 (X, Y, Z) 三个浮点坐标值。
    /// </summary>
    public class XChartDataModel3D
    {
        // ── 数据点结构 ────────────────────────────────────────────

        /// <summary>
        /// 单个数据点。
        /// </summary>
        [Serializable]
        public struct DataPoint
        {
            public double x;
            public double y;
            public double z;

            public DataPoint(double x, double y, double z)
            {
                this.x = x;
                this.y = y;
                this.z = z;
            }

            public Vector3 ToVector3() => new Vector3((float)x, (float)y, (float)z);
        }

        // ── 数据存储 ──────────────────────────────────────────────

        private List<DataPoint> m_Points = new List<DataPoint>();

        /// <summary>所有数据点列表（只读）</summary>
        public IReadOnlyList<DataPoint> Points => m_Points;

        /// <summary>数据点数量</summary>
        public int PointCount => m_Points.Count;

        /// <summary>是否有数据</summary>
        public bool HasData => m_Points.Count > 0;

        // ── 数据范围（自动追踪） ──────────────────────────────────

        /// <summary>X 轴最小值</summary>
        public double MinX { get; private set; }
        /// <summary>X 轴最大值</summary>
        public double MaxX { get; private set; }
        /// <summary>Y 轴最小值</summary>
        public double MinY { get; private set; }
        /// <summary>Y 轴最大值</summary>
        public double MaxY { get; private set; }
        /// <summary>Z 轴最小值</summary>
        public double MinZ { get; private set; }
        /// <summary>Z 轴最大值</summary>
        public double MaxZ { get; private set; }

        /// <summary>X 轴跨度</summary>
        public double RangeX => MaxX - MinX;
        /// <summary>Y 轴跨度</summary>
        public double RangeY => MaxY - MinY;
        /// <summary>Z 轴跨度</summary>
        public double RangeZ => MaxZ - MinZ;

        /// <summary>所有轴的最大跨度（用于归一化）</summary>
        public double MaxRange => Math.Max(Math.Max(RangeX, RangeY), RangeZ);

        // ── 脏标记 ────────────────────────────────────────────────

        [Flags]
        public enum DirtyFlags
        {
            None = 0,
            DataChanged = 1 << 0,
            RangeChanged = 1 << 1,
            All = DataChanged | RangeChanged,
        }

        public DirtyFlags CurrentDirty { get; private set; }

        public void MarkDirty(DirtyFlags flag)
        {
            CurrentDirty |= flag;
        }

        public void ClearDirty(DirtyFlags flag)
        {
            CurrentDirty &= ~flag;
        }

        public bool IsDirty(DirtyFlags flag) => (CurrentDirty & flag) != 0;

        // ── 数据操作 ──────────────────────────────────────────────

        /// <summary>设置所有数据点（覆盖模式）</summary>
        public void SetPoints(IList<DataPoint> points)
        {
            m_Points.Clear();
            if (points != null)
            {
                foreach (var p in points)
                    m_Points.Add(p);
            }
            RecalculateRange();
            MarkDirty(DirtyFlags.All);
        }

        /// <summary>设置原始坐标数据（批量）</summary>
        public void SetPoints(double[] xs, double[] ys, double[] zs)
        {
            m_Points.Clear();
            int count = Math.Min(Math.Min(xs.Length, ys.Length), zs.Length);
            for (int i = 0; i < count; i++)
                m_Points.Add(new DataPoint(xs[i], ys[i], zs[i]));
            RecalculateRange();
            MarkDirty(DirtyFlags.All);
        }

        /// <summary>添加一个数据点</summary>
        public void AddPoint(double x, double y, double z)
        {
            m_Points.Add(new DataPoint(x, y, z));
            UpdateRangeForPoint(x, y, z);
            MarkDirty(DirtyFlags.DataChanged);
        }

        /// <summary>批量添加数据点</summary>
        public void AddPoints(IList<DataPoint> points)
        {
            foreach (var p in points)
            {
                m_Points.Add(p);
                UpdateRangeForPoint(p.x, p.y, p.z);
            }
            MarkDirty(DirtyFlags.DataChanged);
        }

        /// <summary>移除指定索引的数据点</summary>
        public void RemovePoint(int index)
        {
            if (index < 0 || index >= m_Points.Count) return;
            m_Points.RemoveAt(index);
            RecalculateRange();
            MarkDirty(DirtyFlags.All);
        }

        /// <summary>清空所有数据</summary>
        public void ClearAll()
        {
            m_Points.Clear();
            MinX = MaxX = MinY = MaxY = MinZ = MaxZ = 0;
            MarkDirty(DirtyFlags.All);
        }

        /// <summary>获取指定索引的数据点</summary>
        public DataPoint GetPoint(int index)
        {
            return m_Points[index];
        }

        /// <summary>获取所有数据点作为 Vector3 数组</summary>
        public Vector3[] GetPointsAsVector3()
        {
            var result = new Vector3[m_Points.Count];
            for (int i = 0; i < m_Points.Count; i++)
                result[i] = m_Points[i].ToVector3();
            return result;
        }

        /// <summary>
        /// 将数据点归一化到 [-1, 1] 立方体空间。
        /// 基于各轴的最大跨度进行等比例缩放，保持比例。
        /// </summary>
        public Vector3[] GetNormalizedPoints()
        {
            if (m_Points.Count == 0) return Array.Empty<Vector3>();
            double range = MaxRange;
            if (range < double.Epsilon) range = 1.0;

            double cx = (MinX + MaxX) * 0.5;
            double cy = (MinY + MaxY) * 0.5;
            double cz = (MinZ + MaxZ) * 0.5;

            var result = new Vector3[m_Points.Count];
            for (int i = 0; i < m_Points.Count; i++)
            {
                result[i] = new Vector3(
                    (float)((m_Points[i].x - cx) / range * 2.0),
                    (float)((m_Points[i].y - cy) / range * 2.0),
                    (float)((m_Points[i].z - cz) / range * 2.0));
            }
            return result;
        }

        // ── 范围计算 ──────────────────────────────────────────────

        private void RecalculateRange()
        {
            if (m_Points.Count == 0)
            {
                MinX = MaxX = MinY = MaxY = MinZ = MaxZ = 0;
                return;
            }

            MinX = MaxX = m_Points[0].x;
            MinY = MaxY = m_Points[0].y;
            MinZ = MaxZ = m_Points[0].z;

            for (int i = 1; i < m_Points.Count; i++)
            {
                UpdateRangeForPoint(m_Points[i].x, m_Points[i].y, m_Points[i].z);
            }
        }

        private void UpdateRangeForPoint(double x, double y, double z)
        {
            if (x < MinX) MinX = x;
            if (x > MaxX) MaxX = x;
            if (y < MinY) MinY = y;
            if (y > MaxY) MaxY = y;
            if (z < MinZ) MinZ = z;
            if (z > MaxZ) MaxZ = z;
        }
    }
}