using System;
using System.Collections.Generic;
using UnityEngine;
using XericLibrary.Runtime.UIGraph;
using XericUI.Core;
using XericUI.XChart.Space;

namespace XericUI.XChart.Mesh
{
    /// <summary>
    /// 向量场 / 流形箭头元素。**数据口径（规范 §25.2，2026-10-25 他澄清后定稿）**：
    /// <b>前三维＝坐标 x/y/z</b>（与圆柱、散点同一套归一化），<b>一支箭头＝相邻两条记录</b>
    /// （第 i 条是起点，第 i+1 条按 <see cref="VectorMode"/> 解释成"第二个点"或"方向矢量"），
    /// <b>第 4~7 维是颜色</b>（色相 0..256 色环 / 饱和度 / 明度 / 不透明度，可选）。
    ///
    /// <para><b>两种读法</b>：绝对 ⇒ 第二条记录的坐标就是方向（起点 + 方向×倍率），螺旋点画出来是一片"刺猬"；
    /// 相对 ⇒ 它是第二个点（起点 + 差向量×倍率），一串首尾相接的点读起来就是"连着头转"。</para>
    ///
    /// <para><b>连续绘制</b>：把每条记录的坐标按顺序连成折线（只有最后一段带箭头）；
    /// 这时"相对/绝对"不适用 ⇒ 配置里那一项隐藏（互斥显示）。</para>
    ///
    /// <para><b>两端各自的颜色</b>：起点读第 i 条记录、终点读第 i+1 条 ⇒ 两支记录颜色不同时，
    /// 杆与头**各显一色**。真正的"沿杆逐顶点渐变"要等渲染器支持逐顶点双色（P18）。</para>
    ///
    /// <para><b>箭头是自己拼的，没有用库里的 ArrowShape</b>：那套箭头是给曲线的屏幕空间图元用的，
    /// 而这里的箭头必须跟着立方体姿态走（投影后长短和方向都在变）。做法是只让空间负责两端的投影，
    /// 杆和头都在<b>像素空间</b>里量出来：杆是一段等宽四边形，头是一个退化四边形（三角形），
    /// 宽度恒定所以任何姿态下都不会塌成一条线。</para>
    ///
    /// <para><b>数据只经过中转接口</b>：按维度下标 <see cref="IXChartDataSource.GetValueAt"/> 逐条取，
    /// 不碰记录数组；支数由 <see cref="MaxArrowCount"/> 兜住，免得十万条记录一口气投影。</para>
    /// </summary>
    [AddComponentMenu(XericRegistrationPaths.Chart + "/Mesh/Vector Arrow Tool")]
    public sealed class ChartToolVectorArrows : XChartToolBase
    {
        /// <summary>坐标的维度下标（x/y/z 三维）</summary>
        public int CoordinateDimension { get; set; }
        /// <summary>第二条记录怎么读成方向</summary>
        public XChartArrowVectorMode VectorMode { get; set; } = XChartArrowVectorMode.Absolute;
        /// <summary>把每条记录的坐标按顺序连成折线（此时相对/绝对不适用）</summary>
        public bool Continuous { get; set; }
        /// <summary>方向向量的长度倍率（立方体单位）；相对模式下作用在差向量上</summary>
        public float DirectionScale { get; set; } = 0.25f;
        /// <summary>最多画多少支（一支＝相邻两条记录）</summary>
        public int MaxArrowCount { get; set; } = 512;

        /// <summary>箭头自己的变换（叠在图的空间之上）</summary>
        public XChartToolTransform Transform { get; set; } = XChartToolTransform.Identity;

        // ── 颜色：可选的三到四个数值维 ────────────────────────────

        /// <summary>是否用额外的数值维给每支箭头上色</summary>
        public bool UseColorDimensions { get; set; }
        /// <summary>颜色维的起点（色相、饱和度、明度 [+不透明度]）</summary>
        public int ColorDimension { get; set; } = 3;
        /// <summary>是否多读一维不透明度</summary>
        public bool HasAlphaDimension { get; set; }

        public Color ArrowColor { get; set; } = new Color(0.95f, 0.6f, 0.25f, 0.9f);
        public float ShaftWidth { get; set; } = 2f;
        public float HeadLength { get; set; } = 10f;
        public float HeadWidth { get; set; } = 7f;
        /// <summary>方向为零向量时什么都不画；给个数就画成一个点</summary>
        public bool ShowZeroVectorsAsDots { get; set; }

        public XChartMeshFillRenderer FillRenderer { get; private set; }

        /// <summary>
        /// 连续绘制用的曲线渲染器（库里的贝塞尔细分渲染器，圆柱弧段同一件）。
        /// <b>折线渲染器只留给直线</b>——他 2026-10-23 的原话，而路径正是"会弯的那种线"（§10.4）。
        /// </summary>
        public UICurveRenderer CurveRenderer { get; private set; }

        /// <summary>
        /// 连续绘制时<b>画到路径的哪个位置</b>（0＝起点、1＝整条，默认 1）：
        /// 箭头就放在这一处，路径在这一点被剪断（剪口之后不画）——
        /// 不剪的话路径会从箭头底下穿出去，看着像"箭头朝里长"。
        /// </summary>
        public float HeadPosition { get; set; } = 1f;

        /// <summary>连续绘制时每段贝塞尔细分成几节（曲线渲染器的 tessellation）</summary>
        public int PathSegments { get; set; } = 12;

        private readonly List<Vector2> m_Pixels = new List<Vector2>(64);
        private string m_PathRenderer = "-";
        private readonly Vector2[] m_Bezier = new Vector2[XChartArrowPathMath.ControlPointsPerSegment];
        private readonly Vector2[] m_Left = new Vector2[XChartArrowPathMath.ControlPointsPerSegment];
        private readonly Vector2[] m_Right = new Vector2[XChartArrowPathMath.ControlPointsPerSegment];
        private readonly float[] m_Widths = new float[XChartArrowPathMath.ControlPointsPerSegment];
        private int m_CurveEntries;

        private Matrix4x4 m_Composed = Matrix4x4.identity;
        private int m_LastSpaceVersion = -1;
        private long m_LastDataVersion = -1;
        private bool m_Dirty = true;

        private readonly List<ArrowFace> m_Faces = new List<ArrowFace>(256);

        // 本帧的自述读数：算了几支 / 表里几个数值维 / 需要几个 / 几条记录
        private int m_ArrowsBuilt;
        private int m_DimensionsHave;
        private int m_DimensionsNeed;
        private int m_RecordsHave;

        private struct ArrowFace
        {
            public Vector2 A;
            public Vector2 B;
            public Vector2 C;
            public Vector2 D;
            public float Depth;
            /// <summary>00/10 这一对边的颜色（线性时＝起点色）</summary>
            public Color Color;
            /// <summary>01/11 这一对边的颜色（只有线性用；＝终点色）</summary>
            public Color ColorTo;
            /// <summary>true＝沿杆渐变（杆），false＝单色（头、退化点）。
        /// 必须 public：嵌套类型的 private 成员**外层类也读不到**（C# 的这条反直觉，本项目踩过）</summary>
            public bool Linear;
        }

        private static readonly Comparison<ArrowFace> s_FarToNear =
            (left, right) => left.Depth.CompareTo(right.Depth);

        public void MarkDirty() => m_Dirty = true;

        /// <summary>
        /// 这个元素要几个数值维：坐标 3 ＋（可选）颜色 3。
        /// <b>不透明度那一维不算硬需求</b>：多给就多读、没给退回单色 alpha——
        /// 把它算进门槛的话，勾一下"有不透明度"会让整个元素突然一条都不画，
        /// 那是把这一个开关变成了总闸。
        /// </summary>
        public int RequiredDimensionCount
            => XChartArrowConfig.CoordinateDimensionCount + (UseColorDimensions ? XChartArrowConfig.ColorDimensionCount : 0);

        /// <summary>一支箭头要两条记录</summary>
        public int RequiredRecordCount => 2;

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

            // 连续绘制走曲线渲染器（贝塞尔细分）；控制点每帧由本工具喂，别让它自己重建默认那条
            CurveRenderer = CreateChildComponent<UICurveRenderer>("ArrowPath");
            if (CurveRenderer != null)
            {
                CurveRenderer.autoRebuild = false;
                var rect = CurveRenderer.rectTransform;
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
            // 曲线渲染器不在位也要出图（退回直线段），所以它不是"能不能画"的前提

            var space = Context?.Space;
            if (space == null) return;

            long dataVersion = Data != null ? Data.Version : -1;
            if (!m_Dirty && space.ProjectionVersion == m_LastSpaceVersion && dataVersion == m_LastDataVersion)
                return;

            m_Dirty = false;
            m_LastSpaceVersion = space.ProjectionVersion;
            m_LastDataVersion = dataVersion;
            m_Composed = space.Compose(Transform);

            if (CurveRenderer != null) CurveRenderer.ClearAll();
            BuildFaces(space);

            FillRenderer.ClearFaces();
            for (int i = 0; i < m_Faces.Count; i++)
            {
                ArrowFace face = m_Faces[i];
                if (face.Linear)
                    FillRenderer.AddFaceLinear(face.A, face.B, face.C, face.D, face.Color, face.ColorTo);
                else
                    FillRenderer.AddFace(face.A, face.B, face.C, face.D, face.Color, face.Color);
            }
            FillRenderer.Apply();
            if (CurveRenderer != null) CurveRenderer.RebuildAll();
        }

        private void BuildFaces(ChartToolSpace space)
        {
            m_Faces.Clear();
            m_ArrowsBuilt = 0;
            m_DimensionsHave = 0;
            m_RecordsHave = 0;
            m_DimensionsNeed = RequiredDimensionCount;
            m_CurveEntries = 0;
            m_PathRenderer = Continuous ? (CurveRenderer != null ? "curve" : "fill") : "-";

            var source = Data;
            if (source == null || !source.HasData) return;

            m_DimensionsHave = source.NumericDimensionCount;
            m_RecordsHave = source.RecordCount;
            // 形状不够就一支都不画（现象是"加了工具但什么都没显示"）——需求与实际几个数都留在报表里，
            // 面板也印同一对（规范 §25.1）
            if (m_DimensionsHave < CoordinateDimension + RequiredDimensionCount) return;
            if (m_RecordsHave < RequiredRecordCount) return;

            // 一支箭头＝相邻两条记录 ⇒ 支数由纯算术算（离线钉着：记录不够 vs 维数不够要分得清）
            int limit = XChartVectorFieldMath.ArrowCount(m_RecordsHave, MaxArrowCount);

            if (Continuous)
            {
                BuildContinuous(source, space, limit);
                m_Faces.Sort(s_FarToNear);
                return;
            }

            for (int i = 0; i < limit; i++)
            {
                Vector3 first = ReadPoint(source, i);
                Vector3 second = ReadPoint(source, i + 1);
                // 两种读法的算式在纯算术里（离线钉着）：绝对＝第二条的坐标就是方向；相对＝它是第二个点
                Vector3 tip = XChartVectorFieldMath.Tip(VectorMode, first, second, DirectionScale);
                if (AddArrow(space, first, tip, ColorAt(source, i), ColorAt(source, i + 1))) m_ArrowsBuilt++;
            }

            m_Faces.Sort(s_FarToNear);
        }

        /// <summary>
        /// 连续绘制：一串途经点 → Catmull-Rom 贝塞尔链，交给曲线渲染器（贝塞尔细分，接缝不断口）；
        /// 路径在 <see cref="HeadPosition"/> 处<b>剪断</b>，箭头放在剪口上、顺着那一点的<b>切线</b>。
        /// 不剪的话路径会从箭头底下穿出去，看着像"箭头朝里长"。
        /// 段与段之间是同一串坐标，所以"相对/绝对"在这里没有意义（配置里那一项也隐藏了）；
        /// 曲线渲染器不在位时退回直线段（形状仍读得出来），报表里 <c>path=fill</c> 会说出来。
        /// </summary>
        private void BuildContinuous(IXChartDataSource source, ChartToolSpace space, int limit)
        {
            m_Pixels.Clear();
            for (int i = 0; i <= limit; i++)
                m_Pixels.Add(space.Project(ReadPoint(source, i), m_Composed));

            int segments = m_Pixels.Count - 1;
            if (segments < 1) return;

            XChartArrowPathMath.ResolvePosition(segments, HeadPosition, out int headSegment, out float headT);

            Vector2 cut = Vector2.zero;
            Vector2 cutTangent = Vector2.zero;

            for (int s = 0; s <= headSegment; s++)
            {
                BuildSegmentBezier(s);
                Vector2[] drawn = m_Bezier;
                if (s == headSegment && headT < 1f)
                {
                    XChartArrowPathMath.Split(m_Bezier, headT, m_Left, m_Right);
                    drawn = m_Left;
                }

                AddPathSegment(drawn, ColorAt(source, s), ColorAt(source, s + 1));

                cut = XChartArrowPathMath.Evaluate(drawn, 1f);
                cutTangent = XChartArrowPathMath.Tangent(drawn, 1f);
                if (s == headSegment) break;
            }

            AddHeadAt(cut, cutTangent, ColorAt(source, Mathf.Min(headSegment + 1, m_RecordsHave - 1)));
            m_ArrowsBuilt = headSegment + 1;
        }

        /// <summary>段 s（点 s → 点 s+1）的贝塞尔控制点。端点之外重复端点：端点处切线指向段内。</summary>
        private void BuildSegmentBezier(int s)
        {
            int last = m_Pixels.Count - 1;
            XChartArrowPathMath.CatmullRomToBezier(
                m_Pixels[s > 0 ? s - 1 : 0], m_Pixels[s], m_Pixels[s + 1],
                m_Pixels[s + 2 <= last ? s + 2 : last], m_Bezier);
        }

        private void AddPathSegment(Vector2[] bezier, Color fromColor, Color toColor)
        {
            if (CurveRenderer != null)
            {
                for (int i = 0; i < m_Widths.Length; i++) m_Widths[i] = ShaftWidth;
                CurveRenderer.AddCurve(new CurveCacheEntry
                {
                    startColor = fromColor,
                    endColor = toColor,
                    tessellationSegments = Mathf.Clamp(PathSegments, 2, 64),
                }, bezier, m_Widths);
                m_CurveEntries++;
                return;
            }

            Vector2 a = bezier[0];
            Vector2 b = bezier[3];
            Vector2 side = new Vector2(-(b.y - a.y), b.x - a.x);
            if (side.sqrMagnitude < 1e-6f) return;
            side.Normalize();
            float half = ShaftWidth * 0.5f;
            m_Faces.Add(new ArrowFace
            {
                A = a + side * half, B = b + side * half, C = b - side * half, D = a - side * half,
                Depth = 0f, Color = fromColor, ColorTo = toColor, Linear = true,
            });
        }

        /// <summary>箭头放在剪口上，方向顺那一点的<b>切线</b>（不是两端连线——弯处会歪）。</summary>
        private void AddHeadAt(Vector2 tip, Vector2 tangent, Color color)
        {
            if (tangent.sqrMagnitude < 1e-6f) return;
            Vector2 unit = tangent.normalized;
            Vector2 side = new Vector2(-unit.y, unit.x);
            Vector2 baseCenter = tip - unit * HeadLength;
            Vector2 a, b, c, d;
            XChartArrowGeometry.BuildHead(baseCenter, tip, side, HeadWidth, out a, out b, out c, out d);
            m_Faces.Add(new ArrowFace { A = a, B = b, C = c, D = d, Depth = 0f, Color = color });
        }

        /// <summary>
        /// 一支箭头 = 杆（用起点色）+（可选）头（用终点色）。
        /// <b>两端各显一色</b>是颜色维"随记录自带"的直接结果；真正的沿杆渐变要等渲染器支持逐顶点双色（P18）。
        /// </summary>
        private bool AddArrow(ChartToolSpace space, Vector3 origin, Vector3 tip, Color startColor, Color endColor,
            bool withHead = true)
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
                    Color = startColor,
                });
                return true;
            }

            // 杆：沿杆渐变（起点一侧 ← 终点一侧）。BuildShaft 的绕序把 a/d 放在起点、b/c 放在末端，
            // 而 AddFaceLinear 取的是"00/10 一色、01/11 一色"——这条配对由探针钉着，配错会横着染到杆宽上
            m_Faces.Add(new ArrowFace
            {
                A = shaftA, B = shaftB, C = shaftC, D = shaftD,
                Depth = depth, Color = startColor, ColorTo = endColor, Linear = true,
            });
            if (!withHead) return true;

            m_Faces.Add(new ArrowFace
            {
                A = headA, B = headB, C = headC, D = headD,
                Depth = depth, Color = endColor,
            });
            return true;
        }

        private Vector3 ReadPoint(IXChartDataSource source, int record)
        {
            return new Vector3(
                (float)source.GetValueAt(record, CoordinateDimension),
                (float)source.GetValueAt(record, CoordinateDimension + 1),
                (float)source.GetValueAt(record, CoordinateDimension + 2));
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
            if (source.NumericDimensionCount < ColorDimension + XChartArrowConfig.ColorDimensionCount) return ArrowColor;

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
            CurveRenderer = null;
            m_Faces.Clear();
            m_Pixels.Clear();
        }

#if UNITY_EDITOR
        /// <summary>
        /// 报表里点名这一帧：<b>维数（现有/需要）与记录数</b>＋算了几支＋读法＋颜色来源＋面数/块数。
        /// "加了工具什么都没显示"这件事只有这一行分得清是"维数不够"还是"记录不够"还是"算了但没提交"
        /// （他 2026-10-25 现场；规范 §25.1）。
        /// </summary>
        internal override void AppendDebugLine(System.Text.StringBuilder sb)
        {
            base.AppendDebugLine(sb);
            sb.Append(" dims=").Append(m_DimensionsHave).Append('/').Append(m_DimensionsNeed)
              .Append(" records=").Append(m_RecordsHave)
              .Append(" arrows=").Append(m_ArrowsBuilt)
              .Append(" mode=").Append(Continuous ? "continuous" : (VectorMode == XChartArrowVectorMode.Relative ? "relative" : "absolute"))
              .Append(" colors=").Append(UseColorDimensions ? (HasAlphaDimension ? "hsv+a" : "hsv") : "single")
              .Append(" path=").Append(m_PathRenderer)
              .Append(" at=").Append(HeadPosition.ToString("0.###"))
              .Append(" curves=").Append(m_CurveEntries)
              .Append(" curveVerts=").Append(CurveRenderer != null ? CurveRenderer.LiveVertexCount : 0)
              .Append(" arrowFaces=").Append(FillRenderer != null ? FillRenderer.FaceCount : 0)
              .Append(" fillChunks=").Append(FillRenderer != null ? FillRenderer.ChunkCount : 0)
              .Append(" fillMaxVerts=").Append(FillRenderer != null ? FillRenderer.MaxChunkVertices : 0);
        }
#endif
    }
}