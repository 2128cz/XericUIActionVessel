using UnityEngine;
using XericUI.Core;
using XericUI.XChart.Mesh;

namespace XericUI.XChart.Space
{
    /// <summary>
    /// 空间工具（<b>数据源工具</b>）：一张图里<b>唯一</b>的"立方体局部空间 → 图表局部像素"权威。
    ///
    /// <para><b>为什么要有它</b>：背景网格、线框、散点、轴线以前各算各的投影——同一份 pitch/yaw
    /// 在两个类里各存一份、各推一遍，点就落不进盒子里。本工具把基变换收在一处，
    /// 所有元素只问它要矩阵和面帧：<c>投影 = 基变换 ∘ 工具变换</c>，
    /// 于是"点落在背景里"是结构保证，不是靠两边数值凑巧一致。</para>
    ///
    /// <para><b>两档出口</b>：</para>
    /// <list type="bullet">
    /// <item><see cref="Compose"/>：基矩阵 × 工具矩阵，得到该工具用的仿射矩阵，
    /// 交给 <see cref="XChartCubeGeometry.Project(Matrix4x4,float,Vector2,Vector3)"/> /
    /// <see cref="XChartCubeGeometry.BuildFaceFrames"/> 复用同一套投影与面帧推导。</item>
    /// <item><see cref="CubeToPixel"/> / <see cref="Offset"/>：需要自己换算长度或偏移的元素（轴线的
    /// 刻度长度、标签的让位距离）用它们，不要各自再算一遍 scale。</item>
    /// </list>
    ///
    /// <para><b>版本号</b>：<see cref="ProjectionVersion"/> 在标记脏的那一刻递增，
    /// 消费者拿它比对"要不要重新投影"，不需要谁去清脏标记。</para>
    /// </summary>
    [AddComponentMenu(XericRegistrationPaths.Chart + "/Space/Space Tool")]
    public sealed class ChartToolSpace : XChartToolBase
    {
        /// <summary>图表自带的空间工具在管理器里的名字</summary>
        public const string DefaultToolName = "Space";

        /// <summary>空间是"投影权威"：投影矩阵没算出来，谁也算不了 ⇒ 归定义组，最先跑（规范 §11.4）</summary>
        public override XChartToolCategory Category => XChartToolCategory.Definition;

        // ── 基变换 ────────────────────────────────────────────────

        private Quaternion m_Rotation =
            XChartCubeView.RotationOf(XChartCubeViewPreset.Isometric);
        /// <summary>图表级姿态（四元数，唯一的旋转权威量）</summary>
        public Quaternion Rotation { get => m_Rotation; set { m_Rotation = value; MarkDirty(); } }

        private float m_HalfEdge = 120f;
        /// <summary>半边长（UI 像素）</summary>
        public float HalfEdge { get => m_HalfEdge; set { m_HalfEdge = value; MarkDirty(); } }

        private XChartCubeSizeMode m_SizeMode = XChartCubeSizeMode.HalfEdgeLength;
        /// <summary>尺寸口径（默认半边长：尺度不随旋转变化）</summary>
        public XChartCubeSizeMode SizeMode { get => m_SizeMode; set { m_SizeMode = value; MarkDirty(); } }

        private Vector2 m_CenterOffset;
        /// <summary>立方体中心在图表局部像素里的偏移</summary>
        public Vector2 CenterOffset { get => m_CenterOffset; set { m_CenterOffset = value; MarkDirty(); } }

        // ── 派生状态 ──────────────────────────────────────────────

        private Matrix4x4 m_Base;
        private float m_Scale = 1f;
        private Vector2 m_Offset;
        private bool m_ProjectionDirty = true;

        /// <summary>投影版本号（标记脏时递增）</summary>
        public int ProjectionVersion { get; private set; }

        /// <summary>基变换矩阵（纯旋转）</summary>
        public Matrix4x4 BaseMatrix { get { EnsureProjection(); return m_Base; } }

        /// <summary>立方体单位 → 像素的缩放（半边长口径下就等于 <see cref="HalfEdge"/>）</summary>
        public float CubeToPixel { get { EnsureProjection(); return m_Scale; } }

        /// <summary>投影后的平移（图表局部像素）</summary>
        public Vector2 Offset { get { EnsureProjection(); return m_Offset; } }

        /// <summary>空间的"脏"只有投影这一件事：走同一条路，不自立第二个脏位</summary>
        public override void MarkDirty() => MarkProjectionDirty();

        public void MarkProjectionDirty()
        {
            m_ProjectionDirty = true;
            ProjectionVersion++;
        }

        private void EnsureProjection()
        {
            if (!m_ProjectionDirty) return;
            m_ProjectionDirty = false;
            m_Base = XChartCubeView.ToMatrix(Rotation);
            XChartCubeView.ComputeProjection(m_Base, HalfEdge, CenterOffset, SizeMode, out m_Scale, out m_Offset);
        }

        // ── 出口 ──────────────────────────────────────────────────

        /// <summary>基变换 ∘ 工具变换（<paramref name="tool"/> 为恒等时就只剩基矩阵）</summary>
        public Matrix4x4 Compose(XChartToolTransform tool)
        {
            EnsureProjection();
            return tool.IsIdentity ? m_Base : XChartCubeView.Compose(m_Base, tool.Build());
        }

        /// <summary>立方体局部点 → 图表局部像素（带工具变换）</summary>
        public Vector2 Project(Vector3 cubePoint, Matrix4x4 composed)
        {
            EnsureProjection();
            return XChartCubeGeometry.Project(composed, m_Scale, m_Offset, cubePoint);
        }

        /// <summary>立方体局部点 → 图表局部像素（只用基变换）</summary>
        public Vector2 Project(Vector3 cubePoint)
        {
            EnsureProjection();
            return XChartCubeGeometry.Project(m_Base, m_Scale, m_Offset, cubePoint);
        }

        /// <summary>深度（越大越靠近观察者），用来做画家序</summary>
        public float DepthOf(Vector3 cubePoint, Matrix4x4 composed)
        {
            EnsureProjection();
            return composed.MultiplyPoint3x4(cubePoint).z;
        }

        /// <summary>六个面帧（<paramref name="output"/> 长度至少 6；顺序同 <see cref="XChartCubeGeometry.CubeFace"/>）</summary>
        public void BuildFaceFrames(XChartFaceFrame[] output, Matrix4x4 composed)
        {
            EnsureProjection();
            XChartCubeGeometry.BuildFaceFrames(composed, m_Scale, m_Offset, output);
        }

        /// <summary>六个面帧（只用基变换）</summary>
        public void BuildFaceFrames(XChartFaceFrame[] output)
        {
            EnsureProjection();
            XChartCubeGeometry.BuildFaceFrames(m_Base, m_Scale, m_Offset, output);
        }

        /// <summary>某个面是否朝向观察者。注意：法线是按矩阵的线性部分转的，
        /// 工具带<b>非等比缩放</b>时这只是近似（立方体场景里够用，别拿它判曲面）。</summary>
        public bool IsFaceVisible(XChartCubeGeometry.CubeFace face, Matrix4x4 composed)
            => XChartCubeGeometry.IsFaceVisible(composed, face);

        public bool IsFaceVisible(XChartCubeGeometry.CubeFace face)
        {
            EnsureProjection();
            return XChartCubeGeometry.IsFaceVisible(m_Base, face);
        }

        // ── 数据域 → 立方体域 ─────────────────────────────────────

        /// <summary>
        /// 归一化数据（每维 [0,1]）→ 立方体局部点（[-1,1]）。
        /// 数据元素统一走这一条，保证"0.5 就是立方体中间"这件事只有一处定义。
        /// </summary>
        public static Vector3 NormalizedToCube(Vector3 zeroOne)
            => new Vector3(zeroOne.x * 2f - 1f, zeroOne.y * 2f - 1f, zeroOne.z * 2f - 1f);

        protected override void OnInitialize()
        {
            // 空间本身不画东西：它只是别的元素的坐标系。节点仍然要建，
            // 因为元素的兄弟序（= 绘制序）里得有它占的那一格
            SetVisible(true);
            MarkProjectionDirty();
        }
    }
}
