using UnityEngine;

namespace XericUI.OverrideUI
{
    /// <summary>
    /// 单位正方形 [0,1]^2 到任意四边形的投影映射（单应）。
    /// 平行四边形自动退化为仿射（分母恒为 1）；自交或零面积的退化四边形判定为无效。
    /// </summary>
    /// <remarks>
    /// 顶点约定与 <see cref="UnityEngine.UI.Image"/> 的顶点顺序一致：
    /// (0,0)=左下 BL，(1,0)=右下 BR，(1,1)=右上 TR，(0,1)=左上 TL。
    /// 坐标是归一化矩形空间下的坐标，乘以矩形尺寸即得 RectTransform 局部坐标。
    /// </remarks>
    public struct XericQuadProjection
    {
        private const float Epsilon = 1e-8f;

        private float m_A, m_B, m_C;
        private float m_D, m_E, m_F;
        private float m_G, m_H;
        private bool m_Valid;

        public bool IsValid => m_Valid;

        /// <summary>
        /// 由四边形的四个角点构造映射。
        /// </summary>
        /// <param name="p00">单位坐标 (0,0) 对应的角点</param>
        /// <param name="p10">单位坐标 (1,0) 对应的角点</param>
        /// <param name="p11">单位坐标 (1,1) 对应的角点</param>
        /// <param name="p01">单位坐标 (0,1) 对应的角点</param>
        /// <param name="projection">构造结果；四点为退化四边形时 IsValid 为 false</param>
        public static bool TryCreate(Vector2 p00, Vector2 p10, Vector2 p11, Vector2 p01,
            out XericQuadProjection projection)
        {
            float x0 = p00.x, y0 = p00.y;
            float x1 = p10.x, y1 = p10.y;
            float x2 = p11.x, y2 = p11.y;
            float x3 = p01.x, y3 = p01.y;

            float g, h;
            float sx = x0 - x1 + x2 - x3;
            float sy = y0 - y1 + y2 - y3;

            if (Mathf.Abs(sx) < Epsilon && Mathf.Abs(sy) < Epsilon)
            {
                // 平行四边形：仿射，分母恒为 1
                g = 0f;
                h = 0f;
            }
            else
            {
                float dx1 = x1 - x2, dx2 = x3 - x2, dx3 = x0 - x1 + x2 - x3;
                float dy1 = y1 - y2, dy2 = y3 - y2, dy3 = y0 - y1 + y2 - y3;
                float den = dx1 * dy2 - dx2 * dy1;
                if (Mathf.Abs(den) < Epsilon)
                {
                    projection = default;
                    return false;
                }

                g = (dx3 * dy2 - dx2 * dy3) / den;
                h = (dx1 * dy3 - dx3 * dy1) / den;

                // 分母 g*u+h*v+1 是线性函数，其在单位正方形上的最小值必落在角点上，
                // 且 (0,0) 处恒为 1。若另外三个角点出现非正分母，说明该分母在四边形内部
                // 会穿过零点，映射会炸成 NaN 顶点。
                if (Mathf.Min(g + 1f, Mathf.Min(h + 1f, g + h + 1f)) <= 0f)
                {
                    projection = default;
                    return false;
                }
            }

            projection = new XericQuadProjection
            {
                m_A = x1 - x0 + g * x1,
                m_B = x3 - x0 + h * x3,
                m_C = x0,
                m_D = y1 - y0 + g * y1,
                m_E = y3 - y0 + h * y3,
                m_F = y0,
                m_G = g,
                m_H = h,
                m_Valid = true,
            };
            return true;
        }

        /// <summary>把单位正方形内的坐标映射到四边形上（需 IsValid）。</summary>
        public Vector2 Transform(float u, float v)
        {
            float w = m_G * u + m_H * v + 1f;
            return new Vector2(
                (m_A * u + m_B * v + m_C) / w,
                (m_D * u + m_E * v + m_F) / w);
        }

        /// <summary>求逆映射：四边形上的坐标映射回单位正方形。</summary>
        public bool TryInvert(out XericQuadProjection inverse)
        {
            float m00 = m_A, m01 = m_B, m02 = m_C;
            float m10 = m_D, m11 = m_E, m12 = m_F;
            float m20 = m_G, m21 = m_H, m22 = 1f;

            float det = m00 * (m11 * m22 - m12 * m21)
                - m01 * (m10 * m22 - m12 * m20)
                + m02 * (m10 * m21 - m11 * m20);
            if (Mathf.Abs(det) < Epsilon)
            {
                inverse = default;
                return false;
            }

            float i00 = (m11 * m22 - m12 * m21) / det;
            float i01 = (m02 * m21 - m01 * m22) / det;
            float i02 = (m01 * m12 - m02 * m11) / det;
            float i10 = (m12 * m20 - m10 * m22) / det;
            float i11 = (m00 * m22 - m02 * m20) / det;
            float i12 = (m02 * m10 - m00 * m12) / det;
            float i20 = (m10 * m21 - m11 * m20) / det;
            float i21 = (m01 * m20 - m00 * m21) / det;
            float i22 = (m00 * m11 - m01 * m10) / det;
            if (Mathf.Abs(i22) < Epsilon)
            {
                inverse = default;
                return false;
            }

            // 正向映射的分母常数是 1，逆矩阵必须按 i22 归一化才能同构表示
            float inv = 1f / i22;
            inverse = new XericQuadProjection
            {
                m_A = i00 * inv,
                m_B = i01 * inv,
                m_C = i02 * inv,
                m_D = i10 * inv,
                m_E = i11 * inv,
                m_F = i12 * inv,
                m_G = i20 * inv,
                m_H = i21 * inv,
                m_Valid = true,
            };
            return true;
        }
    }

    /// <summary>
    /// 变形映射 + 它所属的矩形，合起来才够把局部坐标映射到局部坐标。
    /// </summary>
    /// <remarks>
    /// 角点活在归一化矩形空间，而网格顶点活在 RectTransform 局部空间（两者差一次
    /// 矩形偏移与尺寸的换算）。这次换乘必须只有一份实现：网格生成、场景线框、离线自测
    /// 都走这里，否则很容易漏掉某一次换算 —— 漏了就会写出一堆 0..1 的顶点，
    /// 在像素空间里塌成不到 1 像素的一团。
    /// </remarks>
    public struct XericQuadMapper
    {
        private XericQuadProjection m_Projection;
        private Rect m_Rect;
        private bool m_Valid;

        public bool IsValid => m_Valid;

        /// <summary>
        /// 角点按 左下 / 左上 / 右上 / 右下 给（与 UnityEngine.UI.Image 的顶点顺序一致）。
        /// </summary>
        public static bool TryCreate(Vector2 cornerBL, Vector2 cornerTL, Vector2 cornerTR, Vector2 cornerBR,
            Rect rect, out XericQuadMapper mapper)
        {
            mapper = default;
            if (rect.width <= 0f || rect.height <= 0f)
                return false;
            // 单位正方形顺序：p00=左下 p10=右下 p11=右上 p01=左上
            if (!XericQuadProjection.TryCreate(cornerBL, cornerBR, cornerTR, cornerTL, out XericQuadProjection projection))
                return false;

            mapper.m_Projection = projection;
            mapper.m_Rect = rect;
            mapper.m_Valid = true;
            return true;
        }

        public Vector2 NormalizedToLocal(Vector2 normalized)
            => XericQuadDeformMath.NormalizedToLocal(normalized, m_Rect);

        public Vector2 LocalToNormalized(Vector2 local)
            => XericQuadDeformMath.LocalToNormalized(local, m_Rect);

        /// <summary>归一化矩形空间 -> 局部坐标，并经过变形投射。</summary>
        public Vector2 MapNormalized(Vector2 normalized)
            => NormalizedToLocal(m_Projection.Transform(normalized.x, normalized.y));

        /// <summary>局部坐标 -> 局部坐标：网格顶点就是这么算的。</summary>
        public Vector2 MapLocal(Vector2 local)
        {
            Vector2 normalized = LocalToNormalized(local);
            return NormalizedToLocal(m_Projection.Transform(normalized.x, normalized.y));
        }
    }

    /// <summary>
    /// 四边形变形的生成器：把矩阵 / 虚拟法线换算成归一化矩形空间下的四个角点。
    /// 角点数组顺序统一为 [0]=左下BL [1]=左上TL [2]=右上TR [3]=右下BR。
    /// </summary>
    public static class XericQuadDeformMath
    {
        /// <summary>归一化矩形空间 -> 局部坐标（矩形自身的换算，与变形是否有效无关）。</summary>
        public static Vector2 NormalizedToLocal(Vector2 normalized, Rect rect) => new Vector2(
            rect.xMin + rect.width * normalized.x,
            rect.yMin + rect.height * normalized.y);

        /// <summary>局部坐标 -> 归一化矩形空间（矩形自身的换算，与变形是否有效无关）。</summary>
        public static Vector2 LocalToNormalized(Vector2 local, Rect rect) => new Vector2(
            (local.x - rect.xMin) / rect.width,
            (local.y - rect.yMin) / rect.height);

        /// <summary>恒等角点（不变形）</summary>
        public static Vector2[] IdentityCorners() => new[]
        {
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 0f),
        };

        /// <summary>
        /// 2D 仿射：只取矩阵的 XY 子块（平移取第三列），结果必为平行四边形。
        /// </summary>
        public static Vector2[] AffineCorners(Matrix4x4 matrix)
        {
            var corners = new Vector2[4];
            for (int i = 0; i < 4; i++)
            {
                Vector2 unit = UnitSquareCorner(i);
                Vector3 p = matrix.MultiplyPoint3x4(new Vector3(unit.x, unit.y, 0f));
                corners[i] = new Vector2(p.x, p.y);
            }
            return corners;
        }

        /// <summary>
        /// 伪投影：用完整 4x4 变换四个角点后做 w 透视除。
        /// 与 <see cref="AffineCorners"/> 的区别在于 w 不恒为 1，
        /// 因此可以吃到"平面在 3D 内倾斜后再投影"的透视收敛。
        /// </summary>
        public static Vector2[] ProjectedCorners(Matrix4x4 matrix)
        {
            var corners = new Vector2[4];
            for (int i = 0; i < 4; i++)
            {
                Vector2 unit = UnitSquareCorner(i);
                corners[i] = Project(matrix, new Vector3(unit.x, unit.y, 0f));
            }
            return corners;
        }

        /// <summary>
        /// 虚拟平面法线：把 UI 平面（默认法线 (0,0,-1)）旋到给定法线后做正交投影，
        /// 得到一个拟合该平面的仿射（倾斜会带来 cos 的纵向压缩）。
        /// </summary>
        /// <param name="normal">虚拟法线，零向量按默认朝向处理</param>
        /// <param name="rollDegrees">绕法线自身的旋转角（度）</param>
        public static Vector2[] PlaneCorners(Vector3 normal, float rollDegrees)
        {
            if (normal.sqrMagnitude < 1e-8f)
                normal = Vector3.back;
            normal = normal.normalized;

            Quaternion rotation = Quaternion.FromToRotation(Vector3.back, normal)
                * Quaternion.Euler(0f, 0f, rollDegrees);
            Matrix4x4 m = Matrix4x4.Rotate(rotation);

            // 旋转后的本地 XY 轴在屏幕 XY 平面上的投影（正交投影）
            var ex = new Vector2(m.m00, m.m10);
            var ey = new Vector2(m.m01, m.m11);
            var center = new Vector2(0.5f, 0.5f);
            Vector2 hx = ex * 0.5f;
            Vector2 hy = ey * 0.5f;

            return new[]
            {
                center - hx - hy,
                center - hx + hy,
                center + hx + hy,
                center + hx - hy,
            };
        }

        private static Vector2 UnitSquareCorner(int index)
        {
            switch (index)
            {
                case 0: return new Vector2(0f, 0f);
                case 1: return new Vector2(0f, 1f);
                case 2: return new Vector2(1f, 1f);
                case 3: return new Vector2(1f, 0f);
                default: return Vector2.zero;
            }
        }

        private static Vector2 Project(Matrix4x4 m, Vector3 p)
        {
            float x = m.m00 * p.x + m.m01 * p.y + m.m02 * p.z + m.m03;
            float y = m.m10 * p.x + m.m11 * p.y + m.m12 * p.z + m.m13;
            float w = m.m30 * p.x + m.m31 * p.y + m.m32 * p.z + m.m33;
            if (Mathf.Abs(w) < 1e-6f)
                w = w < 0f ? -1e-6f : 1e-6f;
            return new Vector2(x / w, y / w);
        }
    }
}