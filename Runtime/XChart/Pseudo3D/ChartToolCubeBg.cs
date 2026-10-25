using System.Collections.Generic;
using UnityEngine;
using XericLibrary.Runtime.UIGraph;
using XericUI.XChart.Mesh;
using XericUI.XChart.Space;
using XericUI.Core;

namespace XericUI.XChart.Pseudo3D
{
    /// <summary>
    /// 伪 3D 立方体<b>线框</b>工具（元素显示名 <c>BG Cube Wire</c>）。
    ///
    /// <para>它只做两件事：<b>12 条棱</b>（相邻面里只要有一个朝向观察者就算前向棱，与面用同一套
    /// 可见性口径）＋ <b>把六个面发布成 <c>mesh.face.&lt;n&gt;</c> 插槽</b>（供轴线/散点取几何）。
    /// 它<b>不画面内格线、也不发布格线插槽</b>——那是 <c>BG Cube Grid</c>
    /// （<see cref="ChartToolMeshBg"/>）作为唯一作者的活（规范 §22）。上一版两边都画格线、还都往
    /// 同一批插槽 id 上发布，于是"关掉一边、另一边又把线显示回来"，看起来就像开关反了
    /// （他 2026-10-25 现场报的两条症状是同一个结构问题的两个面）。</para>
    ///
    /// 几何与投影全部来自 <see cref="XChartCubeGeometry"/> 与 <see cref="XChartCubeView"/>，
    /// 投影权威来自 <see cref="ChartToolSpace"/>（本类不自带顶点/面/棱表，也不自己算投影）：
    /// <see cref="ComposedMatrix"/> / <see cref="Space"/> 是对外的投影出口，
    /// 数据类工具靠它对齐到同一空间。
    ///
    /// <para><b>旋转是一个四元数，不是两轴角度</b>；<b>尺寸是半边长</b>，默认口径下它不随旋转变化
    /// （旧版按投影包围盒 fit，转起来会越转越小）。理由与换算见 <see cref="XChartCubeView"/>。</para>
    ///
    /// 子节点结构：<c>[BG Cube Wire] → CubeEdgeRenderer (UILineRendererV2)</c>
    /// </summary>
    [AddComponentMenu(XericRegistrationPaths.Chart + "/Pseudo3D/Cube Wire Background Tool")]
    public class ChartToolCubeBg : XChartToolBase
    {
        // ── 外观配置 ──────────────────────────────────────────────

        /// <summary>线框自己的变换（立方体局部单位）；默认恒等，即和图的空间重合</summary>
        public XChartToolTransform Transform { get; set; } = XChartToolTransform.Identity;

        /// <summary>前向棱颜色</summary>
        public Color FrontEdgeColor { get; set; } = new Color(0.7f, 0.7f, 0.7f, 1f);

        /// <summary>后向棱颜色</summary>
        public Color BackEdgeColor { get; set; } = new Color(0.4f, 0.4f, 0.4f, 0.4f);

        /// <summary>棱线宽度</summary>
        public float EdgeThickness { get; set; } = 2f;

        /// <summary>是否显示后向棱</summary>
        public bool ShowBackEdges { get; set; } = true;

        // ── 内部组件 ──────────────────────────────────────────────

        /// <summary>棱线渲染器</summary>
        public UILineRendererV2 EdgeRenderer { get; private set; }

        // ── 投影状态 ──────────────────────────────────────────────

        private readonly XChartFaceFrame[] m_FaceFrames = new XChartFaceFrame[XChartCubeGeometry.FaceCount];
        private readonly List<LineVertexData> m_FrontEdges = new List<LineVertexData>(24);
        private readonly List<LineVertexData> m_BackEdges = new List<LineVertexData>(24);

        private ChartToolSpace m_Space;
        private Matrix4x4 m_Composed = Matrix4x4.identity;
        private int m_LastSpaceVersion = -1;
        private bool m_Dirty = true;

        /// <summary>发布插槽用的复用缓冲（一个面的四角 = 12 个值）</summary>
        private readonly float[] m_SlotValues = new float[12];

        // 本帧提交了多少：线框"整块不见了"要能分清是没空间、没提交还是提交了 0 条
        private int m_EdgesDrawn;
        private int m_FacesPublished;

        // ── 对外出口 ──────────────────────────────────────────────

        /// <summary>六个可见性判定共用的面帧（图表局部像素）</summary>
        public XChartFaceFrame GetFaceFrame(XChartCubeGeometry.CubeFace face)
        {
            UpdateProjection();
            return m_FaceFrames[(int)face];
        }

        /// <summary>本元素的变换变了（或想强制重取空间）时调用</summary>
        public void MarkDirty() => m_Dirty = true;

        /// <summary>线框定义立方体的面（发布 <c>mesh.face.*</c> 四角）⇒ 归定义组、最先跑（规范 §11.4）</summary>
        public override XChartToolCategory Category => XChartToolCategory.Definition;

        /// <summary>线框用的复合矩阵。数据元素要落到同一个空间就问它要，别自己再算一遍</summary>
        public Matrix4x4 ComposedMatrix { get { UpdateProjection(); return m_Composed; } }

        /// <summary>空间工具（投影权威）。渲染时现找，所以注册先后顺序不影响正确性</summary>
        public ChartToolSpace Space
        {
            get
            {
                m_Space = Context?.Space;
                return m_Space;
            }
        }

        // ── 插槽发布 ──────────────────────────────────────────────

        /// <summary>
        /// 把六个面发布成 <c>Quad3D</c> 插槽：<b>用哪个网格元素都得能供面给轴线</b>，
        /// 否则"轴线跟随网格"会因为图里只放了线框而静默回落（报表里 <c>frame=cube</c> 就是那个信号）。
        /// 只发四角、不发格线：格线由画它的那个元素发布（两个发布者会抢同一批 id，见规范 §22）。
        /// </summary>
        private void PublishFaceSlots()
        {
            IXChartSlotBoard board = Context?.Slots;
            m_FacesPublished = 0;
            if (board == null) return;

            for (int i = 0; i < XChartCubeGeometry.FaceCount; i++)
            {
                var face = (XChartCubeGeometry.CubeFace)i;
                Vector3 c00, c01, c11, c10;
                XChartCubeGeometry.GetFaceCorners(face, out c00, out c01, out c11, out c10);

                m_SlotValues[0] = c00.x; m_SlotValues[1] = c00.y; m_SlotValues[2] = c00.z;
                m_SlotValues[3] = c01.x; m_SlotValues[4] = c01.y; m_SlotValues[5] = c01.z;
                m_SlotValues[6] = c11.x; m_SlotValues[7] = c11.y; m_SlotValues[8] = c11.z;
                m_SlotValues[9] = c10.x; m_SlotValues[10] = c10.y; m_SlotValues[11] = c10.z;

                board.Publish(XChartCubeGeometry.FaceSlotId(face), XChartSlotMode.Quad3D,
                    XChartSlotAccess.ReadOnly, m_SlotValues, out string _);
                m_FacesPublished++;
            }
        }

        // ── 初始化 ────────────────────────────────────────────────

        protected override void OnInitialize()
        {
            EdgeRenderer = CreateChildComponent<UILineRendererV2>("CubeEdgeRenderer");
            if (EdgeRenderer != null)
            {
                EdgeRenderer.defaultColor = FrontEdgeColor;
                EdgeRenderer.defaultThickness = EdgeThickness;
                StretchToRoot(EdgeRenderer);
            }

            SetVisible(true);
        }

        private static void StretchToRoot(UILineRendererV2 renderer)
        {
            var rt = renderer.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // ── 投影 ──────────────────────────────────────────────────

        private void UpdateProjection()
        {
            var space = Space;
            if (space == null) return;
            if (!m_Dirty && space.ProjectionVersion == m_LastSpaceVersion) return;
            m_Dirty = false;
            m_LastSpaceVersion = space.ProjectionVersion;

            m_Composed = space.Compose(Transform);
            space.BuildFaceFrames(m_FaceFrames, m_Composed);
            PublishFaceSlots();
        }

        // ── 渲染 ──────────────────────────────────────────────────

        public override void OnRender()
        {
            base.OnRender();
            if (EdgeRenderer == null) return;

            UpdateProjection();
            DrawEdges();
        }

        private void DrawEdges()
        {
            EdgeRenderer.ClearAll();
            m_FrontEdges.Clear();
            m_BackEdges.Clear();
            m_EdgesDrawn = 0;

            for (int e = 0; e < XChartCubeGeometry.EdgeCount; e++)
            {
                bool isFront = XChartCubeGeometry.IsEdgeVisible(m_Composed, e);
                var list = isFront ? m_FrontEdges : m_BackEdges;
                Color color = isFront ? FrontEdgeColor : BackEdgeColor;

                list.Add(new LineVertexData
                {
                    position = Space.Project(XChartCubeGeometry.Vertices[XChartCubeGeometry.Edges[e, 0]], m_Composed),
                    color = color,
                    thickness = EdgeThickness,
                    userData = Vector4.zero,
                });
                list.Add(new LineVertexData
                {
                    position = Space.Project(XChartCubeGeometry.Vertices[XChartCubeGeometry.Edges[e, 1]], m_Composed),
                    color = color,
                    thickness = EdgeThickness,
                    userData = Vector4.zero,
                });
            }

            // 先画后向棱（在底层），再画前向棱
            if (ShowBackEdges)
                DrawSegments(m_BackEdges);
            DrawSegments(m_FrontEdges);
        }

        private void DrawSegments(List<LineVertexData> segments)
        {
            for (int i = 0; i + 1 < segments.Count; i += 2)
            {
                EdgeRenderer.DrawLine(
                    segments[i].position,
                    segments[i + 1].position,
                    segments[i].color,
                    segments[i].thickness);
                m_EdgesDrawn++;
            }
        }

        // ── 自述读数 ──────────────────────────────────────────────

#if UNITY_EDITOR
        /// <summary>
        /// 线框这一帧做了什么，全写在报表这一行里：<c>edges=</c> 提交了几条棱、
        /// <c>faces=</c> 发布了几个面、<c>space=</c> 有没有投影权威、<c>renderer=</c> 渲染器在不在位。
        /// "线框整个不见了"这四件事在画面上长得一样，只有这一行分得清（规范 §22）。
        /// </summary>
        internal override void AppendDebugLine(System.Text.StringBuilder sb)
        {
            base.AppendDebugLine(sb);
            int valid = 0;
            for (int i = 0; i < m_FaceFrames.Length; i++)
                if (m_FaceFrames[i].IsValid) valid++;
            sb.Append(" edges=").Append(m_EdgesDrawn)
              .Append(" faces=").Append(m_FacesPublished)
              .Append(" frames=").Append(valid).Append('/').Append(m_FaceFrames.Length)
              .Append(" backEdges=").Append(ShowBackEdges ? "on" : "off")
              .Append(" space=").Append(Context != null && Context.Space != null ? "ok" : "NULL")
              .Append(" renderer=").Append(EdgeRenderer == null ? "NULL" : "ok");
        }
#endif

        // ── 清理 ──────────────────────────────────────────────────

        protected override void OnDispose()
        {
            EdgeRenderer = null;
            m_FrontEdges.Clear();
            m_BackEdges.Clear();
        }
    }
}
