using UnityEngine;
using XericUI.OverrideUI;

namespace XericUI.XChart.Mesh
{
    /// <summary>
    /// 面帧：把一个平面面片自己的参数域 (s,t) 映射到图表局部像素空间的<b>变换协议</b>。
    ///
    /// <para><b>它从哪来的（推导链）</b></para>
    /// 以伪立方体为例，要画它任意一个面，我们必须回答"这个面在屏幕上是哪四个点"：
    /// <code>
    ///   1) 面在立方体局部空间里是一个四边形
    ///        面原点 O3 + s * S3 + t * T3   （s,t ∈ [0,1]，S3/T3 是面的两个边向量）
    ///   2) 把 3D 点投到屏幕局部像素
    ///        正交投影（旋转后丢 Z）: P(p) = (R*p).xy * scale + offset   → 线性
    ///        透视投影（除以 w）    : 一般四边形（梯形）
    ///   3) 于是 "面参数 -> 屏幕" 只剩两种可能：
    ///        仿射（S/T 的投影像仍是平面上的两个固定向量）      —— 正交投影下恒成立
    ///        一般四边形 / 单应（投影后不再保持平行）           —— 透视投影
    /// </code>
    /// 所以协议不需要知道立方体、pitch/yaw、透视——它只需要四个角点，剩下的都由此推出。</para>
    ///
    /// <para><b>两档表示</b></para>
    /// 同一个类型承担两档，不做两个类型：
    /// <list type="bullet">
    /// <item>仿射档：四角构成平行四边形，(1,0)-(0,0) 与 (0,1)-(0,0) 就是两个基向量，映射可解析求逆，零开销。</item>
    /// <item>一般四边形档：任意四角，用单应（透视）映射，退化时会失败。</item>
    /// </list>
    /// 映射实现直接复用 <see cref="XericQuadProjection"/>（同一个"单位正方形 -> 四边形"的投影，已被
    /// 数值验证），本类型只负责角点约定与语义，<b>不再另写一份映射</b>。</para>
    ///
    /// <para><b>角点约定</b>（与 Image 顶点顺序同源）：</para>
    /// <code>
    ///   CornerST00 = Map(0,0)  左下      CornerST01 = Map(0,1)  左上
    ///   CornerST11 = Map(1,1)  右上      CornerST10 = Map(1,0)  右下
    /// </code>
    ///
    /// <para><b>谁用它</b>：网格绘制器（填充面）、面内网格线、轴线组件（在面上取
    /// 值 -> 比例 -> 面参数 -> 像素）、数据图元（把点投到面上）。</para>
    /// </summary>
    public struct XChartFaceFrame
    {
        /// <summary>(s,t)=(0,0) 角点（左下），局部像素</summary>
        public Vector2 CornerST00;
        /// <summary>(s,t)=(0,1) 角点（左上）</summary>
        public Vector2 CornerST01;
        /// <summary>(s,t)=(1,1) 角点（右上）</summary>
        public Vector2 CornerST11;
        /// <summary>(s,t)=(1,0) 角点（右下）</summary>
        public Vector2 CornerST10;

        /// <summary>面语义（哪个面 / 哪条轴用它做宿主），仅作标识，不参与映射</summary>
        public int FaceId;

        private XericQuadProjection m_Projection;
        private bool m_Valid;

        /// <summary>面参数到像素的映射是否可用（四角退化时不可用）</summary>
        public bool IsValid => m_Valid;

        /// <summary>
        /// 是否平行四边形。成立时映射退化为仿射，可解析求逆；正交投影下恒成立，
        /// 透视投影下一般不成立。
        /// </summary>
        public bool IsAffine
        {
            get
            {
                Vector2 d = CornerST00 + CornerST11 - CornerST01 - CornerST10;
                return d.sqrMagnitude <= 1e-6f;
            }
        }

        // ── 构造 ──────────────────────────────────────────────────

        /// <summary>由四个角点构造（左下 / 左上 / 右上 / 右下）</summary>
        public static XChartFaceFrame FromCorners(Vector2 cornerST00, Vector2 cornerST01,
            Vector2 cornerST11, Vector2 cornerST10)
        {
            var frame = new XChartFaceFrame
            {
                CornerST00 = cornerST00,
                CornerST01 = cornerST01,
                CornerST11 = cornerST11,
                CornerST10 = cornerST10,
            };
            // 单位正方形顺序：p00 -> p10 -> p11 -> p01
            frame.m_Valid = XericQuadProjection.TryCreate(
                cornerST00, cornerST10, cornerST11, cornerST01, out frame.m_Projection);
            return frame;
        }

        /// <summary>由仿射基构造：原点 + s 方向 + t 方向（正交投影下最省的一条路）</summary>
        public static XChartFaceFrame FromAffine(Vector2 origin, Vector2 axisS, Vector2 axisT)
        {
            return FromCorners(
                origin,
                origin + axisT,
                origin + axisT + axisS,
                origin + axisS);
        }

        /// <summary>把面参数域里的一个矩形子区间映射成新的面帧（留白 / 内缩用）</summary>
        public XChartFaceFrame Slice(float s0, float t0, float s1, float t1)
        {
            return FromCorners(
                Map(s0, t0),
                Map(s0, t1),
                Map(s1, t1),
                Map(s1, t0));
        }

        // ── 映射 ──────────────────────────────────────────────────

        /// <summary>面参数 (s,t) -> 图表局部像素</summary>
        public Vector2 Map(float s, float t)
        {
            if (!m_Valid)
                return CornerST00;
            return m_Projection.Transform(s, t);
        }

        /// <summary>
        /// 子区间内插：把 (u,v) ∈ [0,1]^2 映射到面参数域里 [s0,s1] x [t0,t1] 的位置。
        /// 轴线的留白、图例区、数据区都靠它表达，避免各处自己乘偏移。
        /// </summary>
        public Vector2 MapRange(float s0, float t0, float s1, float t1, float u, float v)
        {
            return Map(s0 + (s1 - s0) * u, t0 + (t1 - t0) * v);
        }

        /// <summary>局部像素 -> 面参数（反查，命中测试/拖拽用）</summary>
        public bool TryInverse(Vector2 local, out Vector2 st)
        {
            st = Vector2.zero;
            if (!m_Valid)
                return false;
            if (!m_Projection.TryInvert(out var inverse))
                return false;
            st = inverse.Transform(local.x, local.y);
            return true;
        }

        /// <summary>取一条等 t 线（沿 s 方向）的两端</summary>
        public void GetLineAlongS(float t, out Vector2 from, out Vector2 to)
        {
            from = Map(0f, t);
            to = Map(1f, t);
        }

        /// <summary>取一条等 s 线（沿 t 方向）的两端</summary>
        public void GetLineAlongT(float s, out Vector2 from, out Vector2 to)
        {
            from = Map(s, 0f);
            to = Map(s, 1f);
        }

        /// <summary>四个角点（顺序：左下 / 左上 / 右上 / 右下）</summary>
        public Vector2[] GetCorners() => new[]
        {
            CornerST00, CornerST01, CornerST11, CornerST10,
        };

        /// <summary>包围盒（局部像素）</summary>
        public Rect GetBounds()
        {
            float minX = Mathf.Min(Mathf.Min(CornerST00.x, CornerST01.x), Mathf.Min(CornerST11.x, CornerST10.x));
            float maxX = Mathf.Max(Mathf.Max(CornerST00.x, CornerST01.x), Mathf.Max(CornerST11.x, CornerST10.x));
            float minY = Mathf.Min(Mathf.Min(CornerST00.y, CornerST01.y), Mathf.Min(CornerST11.y, CornerST10.y));
            float maxY = Mathf.Max(Mathf.Max(CornerST00.y, CornerST01.y), Mathf.Max(CornerST11.y, CornerST10.y));
            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }
    }
}