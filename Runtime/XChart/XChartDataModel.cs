using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace XericUI.XChart
{
    public enum XChartDimensionKind
    {
        Number,
        Time,
        Label,
    }

    [Serializable]
    public sealed class XChartDimensionDefinition
    {
        [SerializeField] private string m_Key;
        [SerializeField] private string m_DisplayName;
        [SerializeField] private XChartDimensionKind m_Kind;
        [SerializeField] private string m_Format = "0.##";
        [SerializeField] private bool m_Visible = true;

        public string Key => m_Key;
        public string DisplayName => m_DisplayName;
        public XChartDimensionKind Kind => m_Kind;
        public string Format => m_Format;
        public bool Visible => m_Visible;

        public XChartDimensionDefinition(string key, string displayName, XChartDimensionKind kind)
        {
            m_Key = key;
            m_DisplayName = displayName;
            m_Kind = kind;
        }
    }

    [Serializable]
    public sealed class XChartRecord
    {
        [SerializeField] private long m_RecordId;
        [SerializeField] private double m_Timestamp;
        [SerializeField] private float[] m_Values = Array.Empty<float>();
        [SerializeField] private string[] m_Labels = Array.Empty<string>();

        public long RecordId => m_RecordId;
        public double Timestamp => m_Timestamp;
        public IReadOnlyList<float> Values => m_Values;
        public IReadOnlyList<string> Labels => m_Labels;

        internal XChartRecord(long recordId, double timestamp, float[] values, string[] labels)
        {
            m_RecordId = recordId;
            m_Timestamp = timestamp;
            m_Values = values;
            m_Labels = labels;
        }
    }

    public sealed class XChartDataSnapshot : IDisposable
    {
        public NativeArray<double> Times { get; private set; }
        public NativeArray<long> RecordIds { get; private set; }
        public NativeArray<float> Values { get; private set; }
        public int RecordCount { get; private set; }
        public int NumericDimensionCount { get; private set; }
        public long Version { get; private set; }

        internal XChartDataSnapshot(IReadOnlyList<XChartRecord> records, int numericDimensionCount, long version)
        {
            RecordCount = records.Count;
            NumericDimensionCount = numericDimensionCount;
            Version = version;
            var times = new NativeArray<double>(RecordCount, Allocator.Persistent);
            var recordIds = new NativeArray<long>(RecordCount, Allocator.Persistent);
            var values = new NativeArray<float>(RecordCount * numericDimensionCount, Allocator.Persistent);
            for (int i = 0; i < RecordCount; i++)
            {
                times[i] = records[i].Timestamp;
                recordIds[i] = records[i].RecordId;
                for (int j = 0; j < numericDimensionCount; j++) values[i * numericDimensionCount + j] = records[i].Values[j];
            }
            Times = times;
            RecordIds = recordIds;
            Values = values;
        }

        public void Dispose()
        {
            if (Times.IsCreated) Times.Dispose();
            if (RecordIds.IsCreated) RecordIds.Dispose();
            if (Values.IsCreated) Values.Dispose();
        }
    }

    [Serializable]
    public sealed class XChartDataModel
    {
        [SerializeField] private int m_Capacity = 4096;
        [SerializeField] private List<XChartDimensionDefinition> m_Dimensions = new List<XChartDimensionDefinition>();
        [SerializeField] private List<XChartRecord> m_Records = new List<XChartRecord>();
        [NonSerialized] private XChartDataSnapshot m_Snapshot;
        [NonSerialized] private long m_Version;
        [NonSerialized] private long m_NextRecordId = 1;

        public IReadOnlyList<XChartDimensionDefinition> Dimensions => m_Dimensions;
        public IReadOnlyList<XChartRecord> Records => m_Records;
        public XChartDataSnapshot Snapshot => m_Snapshot;
        public long Version => m_Version;
        public int Capacity { get => m_Capacity; set => m_Capacity = Mathf.Max(1, value); }
        public int NumericDimensionCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < m_Dimensions.Count; i++) if (m_Dimensions[i].Kind == XChartDimensionKind.Number) count++;
                return count;
            }
        }

        public void SetDimensions(IList<XChartDimensionDefinition> dimensions)
        {
            m_Dimensions.Clear();
            if (dimensions != null) for (int i = 0; i < dimensions.Count; i++) m_Dimensions.Add(dimensions[i]);
            int numericCount = NumericDimensionCount;
            for (int i = 0; i < m_Records.Count; i++)
            {
                var values = new float[numericCount];
                int copyCount = Math.Min(values.Length, m_Records[i].Values.Count);
                for (int j = 0; j < copyCount; j++) values[j] = m_Records[i].Values[j];
                m_Records[i] = new XChartRecord(m_Records[i].RecordId, m_Records[i].Timestamp, values, CopyLabels(m_Records[i].Labels));
            }
            Publish();
        }

        public bool TryAddRecord(double timestamp, float[] values, string[] labels = null)
        {
            if (double.IsNaN(timestamp) || double.IsInfinity(timestamp) || values == null || values.Length != NumericDimensionCount) return false;
            for (int i = 0; i < values.Length; i++) if (float.IsNaN(values[i]) || float.IsInfinity(values[i])) return false;
            if (m_Records.Count > 0 && timestamp < m_Records[m_Records.Count - 1].Timestamp) return false;
            if (m_Records.Count == Capacity) m_Records.RemoveAt(0);
            var copy = new float[values.Length];
            Array.Copy(values, copy, values.Length);
            m_Records.Add(new XChartRecord(m_NextRecordId++, timestamp, copy, labels ?? Array.Empty<string>()));
            Publish();
            return true;
        }

        public void Clear()
        {
            m_Records.Clear();
            Publish();
        }

        public void Publish()
        {
            var oldSnapshot = m_Snapshot;
            m_Version++;
            m_Snapshot = new XChartDataSnapshot(m_Records, NumericDimensionCount, m_Version);
            oldSnapshot?.Dispose();
        }

        public void DisposeRuntime()
        {
            m_Snapshot?.Dispose();
            m_Snapshot = null;
        }

        private static string[] CopyLabels(IReadOnlyList<string> labels)
        {
            var copy = new string[labels.Count];
            for (int i = 0; i < labels.Count; i++) copy[i] = labels[i];
            return copy;
        }
    }

    public class XChartDataModel3D
    {
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

        private List<DataPoint> m_Points = new List<DataPoint>();
        public IReadOnlyList<DataPoint> Points => m_Points;
        public int PointCount => m_Points.Count;
        public bool HasData => m_Points.Count > 0;
        public double MinX { get; private set; }
        public double MaxX { get; private set; }
        public double MinY { get; private set; }
        public double MaxY { get; private set; }
        public double MinZ { get; private set; }
        public double MaxZ { get; private set; }
        public double RangeX => MaxX - MinX;
        public double RangeY => MaxY - MinY;
        public double RangeZ => MaxZ - MinZ;
        public double MaxRange => Math.Max(Math.Max(RangeX, RangeY), RangeZ);

        [Flags]
        public enum DirtyFlags
        {
            None = 0,
            DataChanged = 1 << 0,
            RangeChanged = 1 << 1,
            All = DataChanged | RangeChanged,
        }

        public DirtyFlags CurrentDirty { get; private set; }
        public void MarkDirty(DirtyFlags flag) { CurrentDirty |= flag; }
        public void ClearDirty(DirtyFlags flag) { CurrentDirty &= ~flag; }
        public bool IsDirty(DirtyFlags flag) => (CurrentDirty & flag) != 0;

        public void SetPoints(IList<DataPoint> points)
        {
            m_Points.Clear();
            if (points != null) for (int i = 0; i < points.Count; i++) m_Points.Add(points[i]);
            RecalculateRange();
            MarkDirty(DirtyFlags.All);
        }

        public void SetPoints(double[] xs, double[] ys, double[] zs)
        {
            m_Points.Clear();
            int count = Math.Min(Math.Min(xs.Length, ys.Length), zs.Length);
            for (int i = 0; i < count; i++) m_Points.Add(new DataPoint(xs[i], ys[i], zs[i]));
            RecalculateRange();
            MarkDirty(DirtyFlags.All);
        }

        public void AddPoint(double x, double y, double z)
        {
            m_Points.Add(new DataPoint(x, y, z));
            UpdateRangeForPoint(x, y, z);
            MarkDirty(DirtyFlags.DataChanged);
        }

        public void ClearAll()
        {
            m_Points.Clear();
            MinX = MaxX = MinY = MaxY = MinZ = MaxZ = 0;
            MarkDirty(DirtyFlags.All);
        }

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
                result[i] = new Vector3((float)((m_Points[i].x - cx) / range * 2.0), (float)((m_Points[i].y - cy) / range * 2.0), (float)((m_Points[i].z - cz) / range * 2.0));
            return result;
        }

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
            for (int i = 1; i < m_Points.Count; i++) UpdateRangeForPoint(m_Points[i].x, m_Points[i].y, m_Points[i].z);
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
