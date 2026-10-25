using System;
using System.Collections.Generic;
using UnityEngine;
using XericLibrary.Runtime.UIGraph;
using XericUI.Core;
using XericUI.XChart.Space;

namespace XericUI.XChart.Mesh
{
    /// <summary>
    /// 向量场 / 流形箭头元素：一条记录占 <b>6 个数值维</b>——两组三维，
    /// 按 <see cref="VectorMode"/> 解释成一支箭头（绝对＝方向矢量 / 相对＝第二个点）。
    /// 后面可以再接 3~4 维颜色（色相/饱和度/明度 [+不透明度]），见 <see cref="UseColorDimensions"/>。
    ///
    /// <para><b>两种读法</b>（他 2026-10-25 口述）：
    /// 绝对 ⇒ 后三维是方向本身，起点加方向乘倍率就是末端（螺旋点画出来是一片"刺猬"）；
    /// 相对 ⇒ 后三维是第二个点，箭头是第一点指向第二点（一串首尾相接的点读起来就是"连着头转"）。</para>
    ///
    /// <para><b>连续绘制</b>：把所有记录的<b>第一组三维</b>按顺序连成折线（只有最后一段带箭头）。
    /// 这时"相对/绝对"不适用 ⇒ 配置里那一项隐藏（互斥显示）。</para>
    ///
    /// <para><b>箭头是自己拼的，没有用库里的 ArrowShape</b>：那套箭头是给曲线的屏幕空间
    /// 图元用的，而这里的箭头必须跟着立方体姿态走（投影后长短和方向都在变）。做法是
    /// 只让空间负责两端的投影，杆和头都在<b>像素空间</b>里量出来：杆是一段等宽四边形，
    /// 头是一个退化四边形（三角形），宽度恒定所以任何姿态下都不会塌成一条线。</para>
    ///
    /// <para><b>数据只经过中转接口</b>：按维度下标 <see cref="IXChartDataSource.GetValueAt"/> 逐条取，
    /// 不碰记录数组；条数由 <see cref="MaxArrowCount"/> 兜住，免得十万条记录一口气投影。</para>
    /// </summary>
    [AddComponentMenu(XericRegistrationPaths.Chart + "/Mesh/Vector Arrow Tool")]
    public sealed class ChartToolVectorArrows : XChartToolBase
    {
        /// <summary>起点的维度下标（矢量再占三维，颜色再往后）</summary>
        public int OriginDimension { get; set; }
        /// <summary>两组三维怎么读成一支箭头</summary>
        public XChartArrowVectorMode VectorMode { get; set; } = XChartArrowVectorMode.Absolute;
        /// <summary>把每条记录的第一个点按顺序连成折线（此时相对/绝对不适用）</summary>
        public bool Continuous { get; set; }
        /// <summary>方向向量的长度倍率（立方体单位）；相对模式下作用在差向量上</summary>
        public float DirectionScale { get; set; } = 0.25f;
        /// <summary>最多画多少条（数据再多也只取前 N 条，避免一次性铺满）</summary>
        public int MaxArrowCount { get; set; } = 512;

        /// <summary>箭头自己的变换（叠在图的空间之上）</summary>
        public XChartToolTransform Transform { get; set; } = XChartToolTransform.Identity;

        // ── 颜色：可选的三到四个数值维 ────────────────────────────

        /// <summary>是否用额外的数值维给每支箭头上色</summary>
        public bool UseColorDimensions { get; set; }
        /// <summary>颜色维的起点（色相、饱和度、明度 [+不透明度]）</summary>
        public int ColorDimension { get; set; } = 6;
        /// <summary>是否多读一维不透明度</summary>
        public bool HasAlphaDimension { get; set; }

        public Color ArrowColor { get; set; } = new Color(0.95f, 0.6f, 0.25f, 0.9f);
        public float ShaftWidth { get; set; } = 2f;
        public float HeadLength { get; set; } = 10f;
        public float HeadWidth { get; set; } = 7f;
        /// <summary>方向为零向量时什么都不画；给个数就画成一个点</summary>
        public bool ShowZeroVectorsAsDots { get; set; }

        public XChartMeshFillRenderer FillRenderer { get; private set; }

        private Matrix4x4 m_Composed = Matrix4x4.identity;
        private int m_LastSpaceVersion = -1;
        private long m_LastDataVersion = -1;
        private bool m_Dirty = true;

        private readonly List<ArrowFace> m_Faces = new List<ArrowFace>(256);

        // 本帧的自述读数：算了几支 / 表里几个数值维 / 够不够
        private int m_ArrowsBuilt;
        private int m_DimensionsHave;
        private int m_DimensionsNeed;

        private struct ArrowFace
        {
            public Vector2 A;
            public Vector2 B;
            public Vector2 C;
            public Vector2 D;
            public float Depth;
            public Color Color;
        }

        private static readonly Comparison<ArrowFace> s_FarToNear =
            (left, right) => left.Depth.CompareTo(right.Depth);

        public void MarkDirty() => m_Dirty = true;

        /// <summary>
        /// 这个元素要几个数值维：矢量 6 ＋（可选）颜色 3。
        /// <b>不透明度那一维不算硬需求</b>：它多给就多读、没给就退回单色的 alpha——
        /// 把它算进门槛的话，勾一下"有不透明度"会让整个元素突然一条都不画（维数不够），
        /// 那是把这一个开关变成了总闸。
        /// </summary>
        public int RequiredDimensionCount
            => XChartArrowConfig.VectorDimensionCount + (UseColorDimensions ? 3 : 0);

        protected override void OnInitialize()
        {
            FillRenderer = CreateChildComponent<XChartMeshFillRenderer>("ArrowFill");
            if (FillRenderer != null)
            {
                var rect = FillRenderer.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
            SetVisible(true);
        }

        public override void OnRender()
        {
            base.OnRender();
            if (FillRenderer == null) return;

            var space = Context?.Space;
            if (space == null) return;

            long dataVersion = Data != null ? Data.Version : -1;
            if (!m_Dirty && space.ProjectionVersion == m_LastSpaceVersion && dataVersion == m_LastDataVersion)
                return;

            m_Dirty = false;
            m_LastSpaceVersion = space.ProjectionVersion;
            m_LastDataVersion = dataVersion;
            m_Composed = space.Compose(Transform);

            BuildFaces(space);

            FillRenderer.ClearFaces();
            for (int i = 0; i < m_Faces.Count; i++)
            {
                ArrowFace face = m_Faces[i];
                FillRenderer.AddFace(face.A, face.B, face.C, face.D, face.Color, face.Color);
            }
            FillRenderer.Apply();
        }

        private void BuildFaces(ChartToolSpace space)
        {
            m_Faces.Clear();
            m_ArrowsBuilt = 0;
            m_DimensionsHave = 0;
            m_DimensionsNeed = RequiredDimensionCount;

            var source = Data;
            if (source == null || !source.HasData) return;

            m_DimensionsHave = source.NumericDimensionCount;
            // 维数不够就一条都不画（现象是"加了工具但什么都没显示"）——需求与实际两个数都留在报表里，
            // 面板也会印同一对（规范 §25）
            if (m_DimensionsHave < OriginDimension + RequiredDimensionCount) return;

            int records = source.RecordCount;
            int limit = records;
            if (MaxArrowCount > 0 && MaxArrowCount < limit) limit = MaxArrowCount;

            if (Continuous)
            {
                BuildContinuous(source, space, limit);
                m_Faces.Sort(s_FarToNear);
                return;
            }

            for (int i = 0; i < limit; i++)
            {
                Vector3 first = ReadPoint(source, i, OriginDimension);
                Vector3 second = ReadPoint(source, i, OriginDimension + 3);
                // 两种读法的算式在纯算术里（离线钉着）：绝对＝后三维就是方向；相对＝后三维是第二个点
                Vector3 tip = XChartVectorFieldMath.Tip(VectorMode, first, second, DirectionScale);

                if (AddArrow(space, first, tip, ColorAt(source, i))) m_ArrowsBuilt++;
            }

            m_Faces.Sort(s_FarToNear);
        }

        /// <summary>
        /// 连续绘制：把所有记录的第一个点按顺序连成一条折线，<b>只有最后一段带箭头</b>。
        /// 读的是每组的第一组三维（坐标），后面那几维（方向/第二点/颜色维）在这种模式下不参与——
        /// 连起来的本来就是空间里的一条路径。
        /// </summary>
        private void BuildContinuous(IXChartDataSource source, ChartToolSpace space, int limit)
        {
            if (limit < 2) return;

            Vector3 previous = ReadPoint(source, 0, OriginDimension);
            for (int i = 1; i < limit; i++)
            {
                Vector3 current = ReadPoint(source, i, OriginDimension);
                bool last = i == limit - 1;
                // 段用"到达点"的颜色（最后一段的箭头也用它）：一串颜色维读下来就是沿路径渐变
                AddArrow(space, previous, current, ColorAt(source, i), last);
                m_ArrowsBuilt++;
                previous = current;
            }
        }

        /// <summary>一支箭头 = 杆 +（可选）头。退化（零长度）时按开关画成一个小方块</summary>
        private bool AddArrow(ChartToolSpace space, Vector3 origin, Vector3 tip, Color color, bool withHead = true)
        {
            Vector2 from = space.Project(origin, m_Composed);
            Vector2 to = space.Project(tip, m_Composed);
            float depth = (space.DepthOf(origin, m_Composed) + space.DepthOf(tip, m_Composed)) * 0.5f;

            Vector2 shaftA, shaftB, shaftC, shaftD, headA, headB, headC, headD;
            if (!XChartArrowGeometry.TryBuild(from, to, ShaftWidth, HeadLength, HeadWidth,
                out shaftA, out shaftB, out shaftC, out shaftD, out headA, out headB, out headC, out headD))
            {
                if (!ShowZeroVectorsAsDots) return false;
                // 退化箭头：画一个极小的四边形，宽度至少一个像素，否则整条向量看不见
                m_Faces.Add(new ArrowFace
                {
                    A = from + new Vector2(-1f, -1f),
                    B = from + new Vector2(-1f, 1f),
                    C = from + new Vector2(1f, 1f),
                    D = from + new Vector2(1f, -1f),
                    Depth = depth,
                    Color = color,
                });
                return true;
            }

            m_Faces.Add(new ArrowFace
            {
                A = shaftA, B = shaftB, C = shaftC, D = shaftD, Depth = depth, Color = color,
            });
            if (!withHead) return true;

            m_Faces.Add(new ArrowFace
            {
                A = headA, B = headB, C = headC, D = headD, Depth = depth, Color = color,
            });
            return true;
        }

        private static Vector3 ReadPoint(IXChartDataSource source, int record, int dimension)
        {
            return new Vector3(
                (float)source.GetValueAt(record, dimension),
                (float)source.GetValueAt(record, dimension + 1),
                (float)source.GetValueAt(record, dimension + 2));
        }

        /// <summary>
        /// 这一条记录的颜色：没开颜色维就是单色；开了就按
        /// <c>[色相(0..256 色环), 饱和度(0..1), 明度(0..1), (不透明度 0..1)]</c> 读。
        /// <b>读不成（维不够或值不是有限数）就退回单色</b>——不是画成黑：黑在深色背景上等于没画，
        /// 而"退回了单色"这件事在报表的 <c>colors=</c> 读数里看得见。
        /// </summary>
        private Color ColorAt(IXChartDataSource source, int record)
        {
            if (!UseColorDimensions) return ArrowColor;
            if (source.NumericDimensionCount < ColorDimension + 3) return ArrowColor;

            float hue = (float)source.GetValueAt(record, ColorDimension);
            float saturation = (float)source.GetValueAt(record, ColorDimension + 1);
            float value = (float)source.GetValueAt(record, ColorDimension + 2);
            float alpha = HasAlphaDimension
                ? (float)source.GetValueAt(record, ColorDimension + 3)
                : ArrowColor.a;

            Color color;
            return XChartVectorFieldMath.TryColor(hue, saturation, value, alpha, out color) ? color : ArrowColor;
        }

        protected override void OnDispose()
        {
            FillRenderer = null;
            m_Faces.Clear();
        }

#if UNITY_EDITOR
        /// <summary>
        /// 报表里点名这一帧：<b>维数（现有/需要）</b>＋算了几支＋读法＋面数/块数。
        /// "加了工具什么都没显示"这件事只有这一行分得清是"维数不够"还是"算了但没提交"
        /// （他 2026-10-25 现场；规范 §25）。
        /// </summary>
        internal override void AppendDebugLine(System.Text.StringBuilder sb)
        {
            base.AppendDebugLine(sb);
            sb.Append(" dims=").Append(m_DimensionsHave).Append('/').Append(m_DimensionsNeed)
              .Append(" arrows=").Append(m_ArrowsBuilt)
              .Append(" mode=").Append(Continuous ? "continuous" : (VectorMode == XChartArrowVectorMode.Relative ? "relative" : "absolute"))
              .Append(" colors=").Append(UseColorDimensions ? (HasAlphaDimension ? "hsv+a" : "hsv") : "single")
              .Append(" arrowFaces=").Append(FillRenderer != null ? FillRenderer.FaceCount : 0)
              .Append(" fillChunks=").Append(FillRenderer != null ? FillRenderer.ChunkCount : 0)
              .Append(" fillMaxVerts=").Append(FillRenderer != null ? FillRenderer.MaxChunkVertices : 0);
        }
#endif
    }
}