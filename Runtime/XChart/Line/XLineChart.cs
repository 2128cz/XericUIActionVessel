using System;
using System.Collections.Generic;
using UnityEngine;
using XericLibrary.Runtime.UIGraph;

namespace XericUI.XChart.Line
{
    [AddComponentMenu("Xeric/XChart/Line/Line Chart")]
    [ExecuteAlways]
    public sealed class XLineChart : XChartBase
    {
        [SerializeField] private Color m_LineColor = new Color(0.33f, 0.67f, 1f, 1f);
        [SerializeField] private float m_LineThickness = 2f;
        [SerializeField] private int m_MaximumBuckets = 1024;
        [SerializeField] private float m_WindowDuration = 60f;
        private UILineRendererV2 m_Renderer;
        private long m_LastVersion = -1;

        protected override void OnEnable()
        {
            base.OnEnable();
            EnsureRenderer();
        }

        private void EnsureRenderer()
        {
            if (m_Renderer != null) return;
            var layer = new GameObject("LineRenderer", typeof(RectTransform), typeof(UILineRendererV2));
            var rect = layer.GetComponent<RectTransform>();
            rect.SetParent(ToolsRoot, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            layer.hideFlags = CurrentFlags;
            m_Renderer = layer.GetComponent<UILineRendererV2>();
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            EnsureRenderer();
            var snapshot = DataModel.Snapshot;
            if (snapshot == null || snapshot.RecordCount < 2 || snapshot.NumericDimensionCount == 0 || snapshot.Version == m_LastVersion) return;
            m_LastVersion = snapshot.Version;
            Draw(snapshot);
        }

        public bool TryAddSample(double timestamp, float value)
        {
            EnsureDefaultDimension();
            return DataModel.TryAddRecord(timestamp, new[] { value });
        }

        private void EnsureDefaultDimension()
        {
            if (DataModel.NumericDimensionCount > 0) return;
            DataModel.SetDimensions(new List<XChartDimensionDefinition>
            {
                new XChartDimensionDefinition("value", "Value", XChartDimensionKind.Number),
            });
        }

        private void Draw(XChartDataSnapshot snapshot)
        {
            m_Renderer.ClearAll();
            float width = Mathf.Max(1f, ((RectTransform)transform).rect.width);
            float height = Mathf.Max(1f, ((RectTransform)transform).rect.height);
            int start = Mathf.Max(0, snapshot.RecordCount - Mathf.Clamp(Mathf.CeilToInt(width), 2, m_MaximumBuckets));
            float min = float.MaxValue;
            float max = float.MinValue;
            for (int i = start; i < snapshot.RecordCount; i++)
            {
                float value = snapshot.Values[i * snapshot.NumericDimensionCount];
                min = Mathf.Min(min, value);
                max = Mathf.Max(max, value);
            }
            if (Mathf.Approximately(min, max)) max = min + 1f;
            var points = new Vector2[snapshot.RecordCount - start];
            for (int i = start; i < snapshot.RecordCount; i++)
            {
                float x = (i - start) * width / Mathf.Max(1, snapshot.RecordCount - start - 1) - width * 0.5f;
                float value = snapshot.Values[i * snapshot.NumericDimensionCount];
                float y = Mathf.InverseLerp(min, max, value) * height - height * 0.5f;
                points[i - start] = new Vector2(x, y);
            }
            m_Renderer.DrawPolyline(points, m_LineColor, m_LineThickness);
        }
    }
}
