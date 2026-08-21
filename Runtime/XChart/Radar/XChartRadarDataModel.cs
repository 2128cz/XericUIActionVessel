using System;
using System.Collections.Generic;
using UnityEngine;

namespace XericUI.XChart.Radar
{
    /// <summary>
    /// 雷达图数据模型。
    ///
    /// 管理多个数据系列（RadarSeries），每个系列对应一个雷达多边形。
    /// 所有系列共享同一组维度名称（Dimensions）。
    ///
    /// 使用示例：
    ///   var model = new XChartRadarDataModel();
    ///   model.SetDimensions("速度", "力量", "防御", "敏捷", "魔力");
    ///   model.AddSeries("战士",  new float[] { 0.8f, 0.9f, 0.7f, 0.4f, 0.2f });
    ///   model.AddSeries("法师",  new float[] { 0.2f, 0.3f, 0.4f, 0.6f, 0.9f });
    /// </summary>
    public class XChartRadarDataModel
    {
        // ── 数据系列 ──────────────────────────────────────────────

        /// <summary>
        /// 单个数据系列。
        /// </summary>
        [Serializable]
        public struct RadarSeries
        {
            /// <summary>系列名称（用于图例）</summary>
            public string name;

            /// <summary>各维度的值（长度需与 Dimensions 一致）</summary>
            public float[] values;

            /// <summary>系列颜色</summary>
            public Color color;

            /// <summary>线宽</summary>
            public float lineWidth;

            public RadarSeries(string name, float[] values, Color color, float lineWidth = 2f)
            {
                this.name = name;
                this.values = values;
                this.color = color;
                this.lineWidth = lineWidth;
            }
        }

        // ── 数据存储 ──────────────────────────────────────────────

        /// <summary>维度名称列表</summary>
        public string[] Dimensions { get; private set; }

        /// <summary>维度数量</summary>
        public int DimensionCount => Dimensions?.Length ?? 0;

        /// <summary>数据系列列表</summary>
        public IReadOnlyList<RadarSeries> Series => m_Series;

        /// <summary>系列数量</summary>
        public int SeriesCount => m_Series.Count;

        /// <summary>是否有数据系列</summary>
        public bool HasSeries => m_Series.Count > 0;

        private List<RadarSeries> m_Series = new List<RadarSeries>();

        // ── 全局范围 ──────────────────────────────────────────────

        /// <summary>所有系列中的全局最大值（用于归一化）</summary>
        public float GlobalMaxValue { get; private set; } = 1f;

        /// <summary>是否自动缩放（基于 GlobalMaxValue）</summary>
        public bool AutoScale { get; set; } = true;

        /// <summary>自定义最大值（AutoScale=false 时使用）</summary>
        public float CustomMaxValue { get; set; } = 1f;

        /// <summary>当前使用的最大值</summary>
        public float MaxValue => AutoScale ? GlobalMaxValue : CustomMaxValue;

        // ── 脏标记 ────────────────────────────────────────────────

        [Flags]
        public enum DirtyFlags
        {
            None = 0,
            DataChanged = 1 << 0,
            DimensionsChanged = 1 << 1,
            All = DataChanged | DimensionsChanged,
        }

        public DirtyFlags CurrentDirty { get; private set; }

        public void MarkDirty(DirtyFlags flag) { CurrentDirty |= flag; }
        public void ClearDirty(DirtyFlags flag) { CurrentDirty &= ~flag; }
        public bool IsDirty(DirtyFlags flag) => (CurrentDirty & flag) != 0;

        // ── 维度操作 ──────────────────────────────────────────────

        /// <summary>设置维度名称</summary>
        public void SetDimensions(params string[] dimensionNames)
        {
            Dimensions = dimensionNames ?? new string[0];
            MarkDirty(DirtyFlags.DimensionsChanged);
        }

        /// <summary>设置维度名称（从列表）</summary>
        public void SetDimensions(IList<string> dimensionNames)
        {
            if (dimensionNames == null)
            {
                Dimensions = new string[0];
            }
            else
            {
                Dimensions = new string[dimensionNames.Count];
                for (int i = 0; i < dimensionNames.Count; i++)
                    Dimensions[i] = dimensionNames[i];
            }
            MarkDirty(DirtyFlags.DimensionsChanged);
        }

        // ── 系列操作 ──────────────────────────────────────────────

        /// <summary>添加一个数据系列</summary>
        public void AddSeries(string name, float[] values, Color? color = null, float lineWidth = 2f)
        {
            if (DimensionCount == 0) return;

            // 截断或补齐
            int count = Math.Min(values.Length, DimensionCount);
            var trimmed = new float[DimensionCount];
            Array.Copy(values, trimmed, count);

            m_Series.Add(new RadarSeries(name, trimmed, color ?? Color.white, lineWidth));
            RecalculateRange();
            MarkDirty(DirtyFlags.DataChanged);
        }

        /// <summary>批量设置数据系列</summary>
        public void SetSeries(IList<RadarSeries> series)
        {
            m_Series.Clear();
            if (series != null)
            {
                foreach (var s in series)
                    m_Series.Add(s);
            }
            RecalculateRange();
            MarkDirty(DirtyFlags.All);
        }

        /// <summary>移除指定系列</summary>
        public void RemoveSeries(int index)
        {
            if (index < 0 || index >= m_Series.Count) return;
            m_Series.RemoveAt(index);
            RecalculateRange();
            MarkDirty(DirtyFlags.DataChanged);
        }

        /// <summary>移除所有系列</summary>
        public void ClearAll()
        {
            m_Series.Clear();
            Dimensions = new string[0];
            GlobalMaxValue = 1f;
            MarkDirty(DirtyFlags.All);
        }

        /// <summary>获取指定系列</summary>
        public RadarSeries GetSeries(int index) => m_Series[index];

        /// <summary>更新指定系列的值</summary>
        public void UpdateSeries(int index, float[] values)
        {
            if (index < 0 || index >= m_Series.Count) return;
            var s = m_Series[index];
            int count = Math.Min(values.Length, DimensionCount);
            s.values = new float[DimensionCount];
            Array.Copy(values, s.values, count);
            m_Series[index] = s;
            RecalculateRange();
            MarkDirty(DirtyFlags.DataChanged);
        }

        // ── 范围计算 ──────────────────────────────────────────────

        private void RecalculateRange()
        {
            float maxVal = 0f;
            for (int si = 0; si < m_Series.Count; si++)
            {
                var vals = m_Series[si].values;
                if (vals == null) continue;
                for (int vi = 0; vi < vals.Length; vi++)
                {
                    if (vals[vi] > maxVal) maxVal = vals[vi];
                }
            }
            GlobalMaxValue = maxVal > 0.001f ? maxVal : 1f;
        }

        // ── 坐标计算 ──────────────────────────────────────────────

        /// <summary>
        /// 计算指定维度在雷达图上的方向向量（从中心指向端点，单位向量）。
        /// 角度从正上方（12点钟方向）开始，顺时针排列。
        /// </summary>
        public static Vector2 GetDimensionDirection(int index, int total)
        {
            if (total <= 0) return Vector2.up;
            float angle = 90f - index * (360f / total);
            float rad = angle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }

        /// <summary>
        /// 计算所有维度的方向向量。
        /// </summary>
        public static Vector2[] GetDimensionDirections(int total)
        {
            var dirs = new Vector2[total];
            for (int i = 0; i < total; i++)
                dirs[i] = GetDimensionDirection(i, total);
            return dirs;
        }

        /// <summary>
        /// 计算雷达图最大半径时的端点位置。
        /// </summary>
        public static Vector2[] GetOuterPoints(Vector2 center, float radius, int dimensionCount)
        {
            var pts = new Vector2[dimensionCount];
            for (int i = 0; i < dimensionCount; i++)
            {
                Vector2 dir = GetDimensionDirection(i, dimensionCount);
                pts[i] = center + dir * radius;
            }
            return pts;
        }

        /// <summary>
        /// 计算某个系列的数据点位置（归一化后的值 × 半径）。
        /// </summary>
        public static Vector2[] GetSeriesPoints(Vector2 center, float radius,
            float[] values, float maxValue, int dimensionCount)
        {
            var pts = new Vector2[dimensionCount];
            float invMax = maxValue > 0.001f ? 1f / maxValue : 1f;
            for (int i = 0; i < dimensionCount; i++)
            {
                Vector2 dir = GetDimensionDirection(i, dimensionCount);
                float val = (i < values.Length) ? values[i] : 0f;
                float r = Mathf.Clamp01(val * invMax) * radius;
                pts[i] = center + dir * r;
            }
            return pts;
        }
    }
}