using UnityEngine;

namespace XericUI.XChart.Mesh
{
    /// <summary>
    /// 立方体尺寸的两种口径。两者的差别正是"旋转时立方体会不会变小"。
    /// </summary>
    public enum XChartCubeSizeMode
    {
        /// <summary>
        /// <b>半边长口径（默认）</b>：给定值是立方体<b>真实边长的一半</b>（也等于面心到中心的距离，
        /// 即内切球半径）。投影缩放与旋转<b>无关</b>：怎么转，边长都是 <c>2 * halfEdge</c> 像素。
        /// </summary>
        HalfEdgeLength,

        /// <summary>
        /// <b>视口适配口径</b>：把<b>投影包围盒</b>的最长边缩放到 <c>2 * halfEdge</c> 像素，
        /// 于是立方体永远刚好铺满那块矩形。代价是尺寸随旋转变化：包围盒在某屏幕轴上的半长是
        /// <c>h * (|ux| + |uy| + |uz|)</c>（<c>u</c> 是该屏幕轴在立方体局部空间的像，
        /// 即 <c>R^T</c> 的那一行），正对时为 <c>h</c>，某条体对角线对着该轴时最大、为 <c>√3 h</c>。
        /// 同一份配置下的实际尺度因此只有设定值的 <c>1/√3 ≈ 0.577</c>（等距预设下垂直方向半长
        /// <c>√(8/3) h</c>，即 <c>≈ 0.612</c> 倍）——这就是"越转越小"。
        /// 需要"填满指定的矩形"时才用它。
        /// </summary>
        FitToViewport,
    }

    /// <summary>
    /// 视角预设：每个值都对应<b>一个四元数</b>（见 <see cref="XChartCubeView.RotationOf"/>），不是角度分量。
    /// <see cref="Manual"/> 表示不做预设覆盖，直接用配置里序列化好的那个四元数。
    /// </summary>
    public enum XChartCubeViewPreset
    {
        Manual = 0,
        /// <summary>45° 正交（体对角线对着观察者），轮廓是<b>正六边形</b>，Front/Right/Top 三面可见</summary>
        Isometric = 1,
        /// <summary>等距视角的镜像（Front/Left/Top 可见）</summary>
        IsometricMirrored = 2,
        Front = 3,
        Back = 4,
        Right = 5,
        Left = 6,
        Top = 7,
        Bottom = 8,
    }

    /// <summary>
    /// 立方体视图：旋转<b>只有一个权威量——四元数</b>，尺寸<b>只有一个权威量——半边长</b>。
    ///
    /// <para><b>为什么不再拆 pitch/yaw</b>：两轴拆分本身不算错，但它把"视角"表达成了
    /// "绕世界 X 转一下、再绕世界 Y 转一下"，于是任何第三个轴的姿态都拿不到（roll 被钉死为 0），
    /// 而且默认值 (25, -30) 只是随手取的，既不是正交等距也不是正视，所以画面里的"矩形"总是歪的。
    /// 本类把姿态收成一个四元数：预设、拖拽、代码 API 都直接给四元数，需要由角度合成时用
    /// <see cref="Euler"/>（它按 <c>Rx * Ry * Rz</c> 合成，与 <c>Quaternion.Euler</c> 同一约定）。</para>
    ///
    /// <para><b>为什么尺寸要收成半边长</b>：见 <see cref="XChartCubeSizeMode"/>。
    /// 旧实现按"投影包围盒最长边"来 fit，而包围盒半长随旋转从 <c>h</c> 一路长大到 <c>√3 h</c>，
    /// 立方体就必须缩小，调一次俯仰小一档、再调一次偏航又小一档，最后远小于设定的尺寸。
    /// 默认口径改成与旋转无关的半边长后，尺寸这件事和视角这件事彻底解耦。</para>
    ///
    /// <para><b>几种半径的关系</b>（半边长记作 <c>h</c>，立方体顶点在 <c>±h</c>）：
    /// <code>
    ///   面心到中心（内切球半径）      = h
    ///   顶点到中心（外接球半径）      = √3 h ≈ 1.7321 h
    ///   等距轮廓正六边形的外接圆半径  = √(8/3) h ≈ 1.6330 h
    ///   等距轮廓正六边形的内切圆半径  = √2 h   ≈ 1.4142 h
    ///   沿某屏幕轴的包围盒半长        = h * (|ux| + |uy| + |uz|)，取值 h … √3 h
    /// </code>
    /// 所以"要画进一个 R 半径的圆里"时，半边长取 <c>R / √(8/3)</c>；
    /// 换算函数在下面这几个 <c>*FromHalfEdge</c> / <c>HalfEdgeFrom*</c> 里。</para>
    ///
    /// <para><b>离线可跑</b>：这里不做任何引擎内部调用（<c>Quaternion.Euler</c>、
    /// <c>Quaternion.FromToRotation</c>、<c>Matrix4x4.TRS</c> 出不了 Unity），
    /// 四元数由 axis-angle + Hamilton 积纯托管合成，矩阵由字段逐个拼出来。
    /// 编译门里的 mono 探针会把 <see cref="ToMatrix"/> 与 <c>Matrix4x4.Rotate</c> 对拍，
    /// 把 <see cref="Euler"/> 与三次 <see cref="AxisAngle"/> 的连乘对拍，
    /// 并把"等距视角轮廓是正六边形""半边长口径下尺度不随旋转变化"当断言钉住。</para>
    /// </summary>
    public static class XChartCubeView
    {
        /// <summary>atan(1/√2) 的度数：体对角线对着观察者时，对角线与水平面的夹角</summary>
        public const float IsometricPitchDegrees = 35.2643897f;

        /// <summary>等距视角的偏航量（绝对值），符号由 <see cref="XChartCubeViewPreset"/> 决定</summary>
        public const float IsometricYawDegrees = 45f;

        private const float k_Sqrt3 = 1.7320508f;
        private const float k_Sqrt2 = 1.41421356f;
        private const float k_Sqrt8Over3 = 1.6329932f;
        private const float k_MinLength = 1e-8f;

        // ── 四元数合成（纯托管） ──────────────────────────────────

        /// <summary>归一化后的四元数。长度过短返回单位四元数（不做除法放大噪声）</summary>
        public static Quaternion Normalized(float x, float y, float z, float w)
        {
            float length = Mathf.Sqrt(x * x + y * y + z * z + w * w);
            if (length < k_MinLength) return new Quaternion(0f, 0f, 0f, 1f);
            float inv = 1f / length;
            return new Quaternion(x * inv, y * inv, z * inv, w * inv);
        }

        /// <summary>
        /// 绕任意轴的旋转（右手定则，角度制）。轴不必预先归一化。
        /// <c>q = (axis * sin(θ/2), cos(θ/2))</c>
        /// </summary>
        public static Quaternion AxisAngle(Vector3 axis, float angleDegrees)
        {
            float length = Mathf.Sqrt(axis.x * axis.x + axis.y * axis.y + axis.z * axis.z);
            if (length < k_MinLength) return new Quaternion(0f, 0f, 0f, 1f);

            float inv = 1f / length;
            float half = angleDegrees * 0.5f * Mathf.Deg2Rad;
            float s = Mathf.Sin(half);
            return new Quaternion(
                axis.x * inv * s,
                axis.y * inv * s,
                axis.z * inv * s,
                Mathf.Cos(half));
        }

        /// <summary>
        /// Hamilton 积 <c>a ⊗ b</c>，按 Unity 的约定即"先施加 <paramref name="b"/>、再施加 <paramref name="a"/>"
        /// （矩阵视角就是 <c>Ra * Rb</c>）。
        /// 手写展开而不用引擎的 <c>operator*</c>，好让整条合成链在离线探针里也走得通。
        /// </summary>
        public static Quaternion Multiply(Quaternion a, Quaternion b)
        {
            return new Quaternion(
                a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y,
                a.w * b.y - a.x * b.z + a.y * b.w + a.z * b.x,
                a.w * b.z + a.x * b.y - a.y * b.x + a.z * b.w,
                a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z);
        }

        /// <summary>
        /// 按 <c>Rx(xDeg) * Ry(yDeg) * Rz(zDeg)</c> 合成——与 <c>Quaternion.Euler</c> 的约定一致
        /// （先绕 Z、再绕 Y、最后绕 X，都是绕<b>世界</b>轴）。
        /// </summary>
        public static Quaternion Euler(float xDegrees, float yDegrees, float zDegrees)
        {
            Quaternion rx = AxisAngle(Vector3.right, xDegrees);
            Quaternion ry = AxisAngle(Vector3.up, yDegrees);
            Quaternion rz = AxisAngle(Vector3.forward, zDegrees);
            return Multiply(Multiply(rx, ry), rz);
        }

        /// <summary>把配置里的四元数收正：归一化 + 长度失效时退回单位旋转</summary>
        public static Quaternion Sanitize(Quaternion q) => Normalized(q.x, q.y, q.z, q.w);

        // ── 预设 ──────────────────────────────────────────────────

        /// <summary>
        /// 预设对应的旋转。"某面正对观察者"= 把该面的外法线转到 +Z（投影保留 X/Y、丢弃 Z，
        /// 所以 <c>(R n).z == 1</c> 时该面正对观察者）。
        /// </summary>
        public static Quaternion RotationOf(XChartCubeViewPreset preset)
        {
            switch (preset)
            {
                case XChartCubeViewPreset.Isometric:
                    return Euler(IsometricPitchDegrees, -IsometricYawDegrees, 0f);
                case XChartCubeViewPreset.IsometricMirrored:
                    return Euler(IsometricPitchDegrees, IsometricYawDegrees, 0f);
                case XChartCubeViewPreset.Front:
                    return new Quaternion(0f, 0f, 0f, 1f);
                case XChartCubeViewPreset.Back:
                    return AxisAngle(Vector3.up, 180f);
                case XChartCubeViewPreset.Right:
                    return AxisAngle(Vector3.up, -90f);
                case XChartCubeViewPreset.Left:
                    return AxisAngle(Vector3.up, 90f);
                case XChartCubeViewPreset.Top:
                    return AxisAngle(Vector3.right, 90f);
                case XChartCubeViewPreset.Bottom:
                    return AxisAngle(Vector3.right, -90f);
                default:
                    return new Quaternion(0f, 0f, 0f, 1f);
            }
        }

        // ── 旋转 -> 矩阵 ──────────────────────────────────────────

        /// <summary>
        /// 四元数 -> 3D 旋转矩阵（齐次形式，<c>m33 = 1</c>）。
        /// 这里逐字段拼装，和 <c>Matrix4x4.Rotate</c> 是两条独立实现，探针要求它们一致。
        /// 输入必须是单位四元数，所以先过一遍 <see cref="Sanitize"/>。
        /// </summary>
        public static Matrix4x4 ToMatrix(Quaternion q)
        {
            Quaternion u = Sanitize(q);

            float xx = u.x * u.x, yy = u.y * u.y, zz = u.z * u.z;
            float xy = u.x * u.y, xz = u.x * u.z, yz = u.y * u.z;
            float wx = u.w * u.x, wy = u.w * u.y, wz = u.w * u.z;

            var m = new Matrix4x4
            {
                m00 = 1f - 2f * (yy + zz),
                m01 = 2f * (xy - wz),
                m02 = 2f * (xz + wy),
                m03 = 0f,

                m10 = 2f * (xy + wz),
                m11 = 1f - 2f * (xx + zz),
                m12 = 2f * (yz - wx),
                m13 = 0f,

                m20 = 2f * (xz - wy),
                m21 = 2f * (yz + wx),
                m22 = 1f - 2f * (xx + yy),
                m23 = 0f,

                m30 = 0f,
                m31 = 0f,
                m32 = 0f,
                m33 = 1f,
            };
            return m;
        }

        /// <summary>
        /// 仿射复合 <c>a ∘ b</c>（矩阵意义下的 <c>a * b</c>：先施加 <paramref name="b"/>，再施加
        /// <paramref name="a"/>）。只支持"线性 + 平移"这一族（最后一行恒为 0,0,0,1），
        /// 所以 12 个元素就够，不用写全 4x4 乘。
        /// <para>为什么不用 <c>Matrix4x4.operator*</c>：它底层是引擎内部调用，出不了 Unity，
        /// 离线探针就跑不了这条链；空间层的"基变换 × 工具变换"必须能离线验。</para>
        /// </summary>
        public static Matrix4x4 Compose(Matrix4x4 a, Matrix4x4 b)
        {
            var m = new Matrix4x4
            {
                m00 = a.m00 * b.m00 + a.m01 * b.m10 + a.m02 * b.m20,
                m01 = a.m00 * b.m01 + a.m01 * b.m11 + a.m02 * b.m21,
                m02 = a.m00 * b.m02 + a.m01 * b.m12 + a.m02 * b.m22,
                m03 = a.m00 * b.m03 + a.m01 * b.m13 + a.m02 * b.m23 + a.m03,

                m10 = a.m10 * b.m00 + a.m11 * b.m10 + a.m12 * b.m20,
                m11 = a.m10 * b.m01 + a.m11 * b.m11 + a.m12 * b.m21,
                m12 = a.m10 * b.m02 + a.m11 * b.m12 + a.m12 * b.m22,
                m13 = a.m10 * b.m03 + a.m11 * b.m13 + a.m12 * b.m23 + a.m13,

                m20 = a.m20 * b.m00 + a.m21 * b.m10 + a.m22 * b.m20,
                m21 = a.m20 * b.m01 + a.m21 * b.m11 + a.m22 * b.m21,
                m22 = a.m20 * b.m02 + a.m21 * b.m12 + a.m22 * b.m22,
                m23 = a.m20 * b.m03 + a.m21 * b.m13 + a.m22 * b.m23 + a.m23,

                m30 = 0f, m31 = 0f, m32 = 0f, m33 = 1f,
            };
            return m;
        }

        // ── 尺寸口径 ──────────────────────────────────────────────

        /// <summary>半边长 -> 外接球半径（顶点到中心）= √3 h</summary>
        public static float CircumradiusFromHalfEdge(float halfEdge) => halfEdge * k_Sqrt3;

        /// <summary>外接球半径 -> 半边长</summary>
        public static float HalfEdgeFromCircumradius(float circumradius) => circumradius / k_Sqrt3;

        /// <summary>半边长 -> 等距轮廓正六边形的外接圆半径 = √(8/3) h</summary>
        public static float IsometricHexagonCircumradius(float halfEdge) => halfEdge * k_Sqrt8Over3;

        /// <summary>想让轮廓正六边形内接于半径 r 的圆，半边长该给多少</summary>
        public static float HalfEdgeFromHexagonCircumradius(float radius) => radius / k_Sqrt8Over3;

        /// <summary>半边长 -> 等距轮廓正六边形的内切圆半径 = √2 h</summary>
        public static float IsometricHexagonInradius(float halfEdge) => halfEdge * k_Sqrt2;

        /// <summary>
        /// 按口径算投影缩放与平移。
        /// <para><see cref="XChartCubeSizeMode.HalfEdgeLength"/>：<c>scale = halfEdge</c>——
        /// 立方体顶点在 ±1，所以 scale 就是半边长，与旋转无关；包围盒中心仍然对齐到
        /// <paramref name="centerOffset"/>，于是转起来只是转、不会漂。</para>
        /// <para><see cref="XChartCubeSizeMode.FitToViewport"/>：<c>scale</c> 让包围盒最长边等于
        /// <c>2 * halfEdge</c>，铺满固定矩形但尺寸随旋转变化。</para>
        /// </summary>
        public static void ComputeProjection(Matrix4x4 rotation, float halfEdge, Vector2 centerOffset,
            XChartCubeSizeMode mode, out float scale, out Vector2 offset)
        {
            XChartCubeGeometry.ComputeProjectedBounds(rotation, out Vector2 min, out Vector2 max);
            Vector2 center = new Vector2((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f);

            if (mode == XChartCubeSizeMode.HalfEdgeLength)
            {
                scale = Mathf.Max(0.001f, halfEdge);
            }
            else
            {
                float maxRange = Mathf.Max(max.x - min.x, max.y - min.y);
                scale = maxRange > 0.001f ? halfEdge * 2f / maxRange : 1f;
            }

            offset = new Vector2(centerOffset.x - center.x * scale, centerOffset.y - center.y * scale);
        }

        /// <summary>四元数版入口：内部先转矩阵，调用方不必自己拿 <see cref="ToMatrix"/></summary>
        public static void ComputeProjection(Quaternion rotation, float halfEdge, Vector2 centerOffset,
            XChartCubeSizeMode mode, out float scale, out Vector2 offset)
        {
            ComputeProjection(ToMatrix(rotation), halfEdge, centerOffset, mode, out scale, out offset);
        }
    }
}
