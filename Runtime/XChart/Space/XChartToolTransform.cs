using UnityEngine;
using XericUI.XChart.Mesh;

namespace XericUI.XChart.Space
{
    /// <summary>
    /// 工具级变换：<b>作用在立方体局部空间里</b>的一个仿射（缩放 → 旋转 → 平移）。
    ///
    /// <para><b>单位</b>：立方体局部空间是"半边长 1"的归一化域（顶点在 ±1），所以
    /// <see cref="Position"/> 的 1 就是立方体边界、<see cref="Scale"/> 的 1 就是原尺寸。
    /// 这样工具自己不用关心像素，也不用关心图表当前多大。</para>
    ///
    /// <para><b>和基变换的关系</b>：投影时算的是 <c>Base ∘ Tool</c>（先工具、后基），
    /// 由 <see cref="XericUI.XChart.Mesh.XChartCubeView.Compose"/> 复合，
    /// 结果仍然是一个仿射矩阵，所以面帧依旧落在平行四边形那一档（协议见
    /// <see cref="XChartFaceFrame"/>）。</para>
    /// </summary>
    [System.Serializable]
    public struct XChartToolTransform
    {
        [SerializeField] private Vector3 m_Position;
        [SerializeField] private Quaternion m_Rotation;
        [SerializeField] private Vector3 m_Scale;

        /// <summary>
        /// 结构体不能有字段初始化器，所以<b>单位变换只能由 <see cref="Identity"/> 或这个构造给</b>。
        /// 直接 <c>new XChartToolTransform()</c> 会得到 scale=0（整个塌成一个点），
        /// 现象很扎眼但不是语义——所有配置字段都写成 <c>= XChartToolTransform.Identity</c>。
        /// </summary>
        public XChartToolTransform(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            m_Position = position;
            m_Rotation = rotation;
            m_Scale = scale;
        }

        /// <summary>立方体局部空间里的平移（1 = 到立方体边界）</summary>
        public Vector3 Position { get => m_Position; set => m_Position = value; }

        /// <summary>工具自身的旋转（四元数，和基变换同样不拆角度）</summary>
        public Quaternion Rotation { get => m_Rotation; set => m_Rotation = value; }

        /// <summary>各轴缩放（1 = 原尺寸）</summary>
        public Vector3 Scale { get => m_Scale; set => m_Scale = value; }

        public static XChartToolTransform Identity
            => new XChartToolTransform { Scale = Vector3.one };

        /// <summary>是否等于不做任何事（用来走"省掉复合"的快速路径）</summary>
        public bool IsIdentity => m_Position == Vector3.zero
            && m_Rotation == new Quaternion(0f, 0f, 0f, 1f)
            && m_Scale == Vector3.one;

        /// <summary><c>T * R * S</c>（线性 + 平移，最后一行恒 0,0,0,1）</summary>
        public Matrix4x4 Build()
        {
            Matrix4x4 r = XChartCubeView.ToMatrix(m_Rotation);
            var m = new Matrix4x4
            {
                m00 = r.m00 * m_Scale.x, m01 = r.m01 * m_Scale.y, m02 = r.m02 * m_Scale.z, m03 = m_Position.x,
                m10 = r.m10 * m_Scale.x, m11 = r.m11 * m_Scale.y, m12 = r.m12 * m_Scale.z, m13 = m_Position.y,
                m20 = r.m20 * m_Scale.x, m21 = r.m21 * m_Scale.y, m22 = r.m22 * m_Scale.z, m23 = m_Position.z,
                m30 = 0f, m31 = 0f, m32 = 0f, m33 = 1f,
            };
            return m;
        }
    }
}
