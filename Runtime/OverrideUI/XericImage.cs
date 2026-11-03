using System;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using XericUI.Helper;
using XericUI.Core;

namespace XericUI.OverrideUI
{
    /// <summary>
    /// 图像组件
    /// </summary>
    /// <remarks>
    /// 这个组件主要为了提供graphic层的事件，用于应对部分无法重写的ui组件事件。
    /// 另外支持四顶点变形（错切 / 倾斜 / 平行四边形 / 伪投影）：
    /// 顶点偏移存在归一化矩形空间里，RectTransform 尺寸变化时变形等比跟随。
    /// </remarks>
    [AddComponentMenu(XericRegistrationPaths.UI + "/Image", 50)]
    public class XericImage : Image
    {
        /// <summary>
        /// 针对toggle的回报(ison)
        /// </summary>
        internal Action<bool> OnToggleCrossFadeCall;
        public override void CrossFadeColor(Color targetColor, float duration, bool ignoreTimeScale, bool useAlpha, bool useRGB)
        {
            base.CrossFadeColor(targetColor, duration, ignoreTimeScale, useAlpha, useRGB);
            OnToggleCrossFadeCall?.Invoke(targetColor.a > 0);
        }

        #region 顶点变形

        // 角点索引：0=左下 1=左上 2=右上 3=右下（与 Image 生成的顶点顺序一致）

        [SerializeField, Tooltip("左下角顶点偏移（归一化矩形空间，零=不变形）")]
        private Vector2 m_CornerOffsetBL;
        [SerializeField, Tooltip("左上角顶点偏移（归一化矩形空间，零=不变形）")]
        private Vector2 m_CornerOffsetTL;
        [SerializeField, Tooltip("右上角顶点偏移（归一化矩形空间，零=不变形）")]
        private Vector2 m_CornerOffsetTR;
        [SerializeField, Tooltip("右下角顶点偏移（归一化矩形空间，零=不变形）")]
        private Vector2 m_CornerOffsetBR;

        /// <summary>是否处于变形状态（四角偏移全为零即未变形）</summary>
        public bool HasDeform =>
            m_CornerOffsetBL != Vector2.zero ||
            m_CornerOffsetTL != Vector2.zero ||
            m_CornerOffsetTR != Vector2.zero ||
            m_CornerOffsetBR != Vector2.zero;

        /// <summary>归一化矩形空间下的恒等角点</summary>
        public static Vector2 IdentityCorner(int index)
        {
            switch (index)
            {
                case 0: return new Vector2(0f, 0f);
                case 1: return new Vector2(0f, 1f);
                case 2: return new Vector2(1f, 1f);
                case 3: return new Vector2(1f, 0f);
                default: throw new ArgumentOutOfRangeException(nameof(index), index, "角点索引必须在 0..3");
            }
        }

        /// <summary>取某个角点在归一化矩形空间下的坐标（恒等 + 偏移）</summary>
        public Vector2 GetCorner(int index) => IdentityCorner(index) + GetCornerOffset(index);

        /// <summary>取某个角点在归一化矩形空间下的偏移量</summary>
        public Vector2 GetCornerOffset(int index)
        {
            switch (index)
            {
                case 0: return m_CornerOffsetBL;
                case 1: return m_CornerOffsetTL;
                case 2: return m_CornerOffsetTR;
                case 3: return m_CornerOffsetBR;
                default: throw new ArgumentOutOfRangeException(nameof(index), index, "角点索引必须在 0..3");
            }
        }

        /// <summary>按归一化矩形空间下的坐标设置单个角点</summary>
        public void SetCorner(int index, Vector2 normalizedCorner)
        {
            Vector2[] corners = GetCorners();
            corners[index] = normalizedCorner;
            ApplyCorners(corners[0], corners[1], corners[2], corners[3]);
        }

        /// <summary>一次性设置四个角点（归一化矩形空间）</summary>
        public void SetCorners(Vector2 cornerBL, Vector2 cornerTL, Vector2 cornerTR, Vector2 cornerBR)
            => ApplyCorners(cornerBL, cornerTL, cornerTR, cornerBR);

        /// <summary>取四个角点（归一化矩形空间），顺序 左下 / 左上 / 右上 / 右下</summary>
        public Vector2[] GetCorners() => new[]
        {
            GetCorner(0),
            GetCorner(1),
            GetCorner(2),
            GetCorner(3),
        };

        /// <summary>
        /// 用 2D 仿射矩阵变形（只取矩阵的 XY 子块），结果一定是平行四边形。
        /// </summary>
        public void ApplyAffine(Matrix4x4 matrix)
        {
            Vector2[] c = XericQuadDeformMath.AffineCorners(matrix);
            ApplyCorners(c[0], c[1], c[2], c[3]);
        }

        /// <summary>
        /// 用完整 4x4 矩阵变形并做透视除（伪投影）：可得到任意四边形。
        /// </summary>
        public void ApplyProjected(Matrix4x4 matrix)
        {
            Vector2[] c = XericQuadDeformMath.ProjectedCorners(matrix);
            ApplyCorners(c[0], c[1], c[2], c[3]);
        }

        /// <summary>
        /// 用虚拟平面法线定义这个面，内部换算成拟合该平面的仿射（倾斜 = 纵向 cos 压缩）。
        /// </summary>
        /// <param name="normal">虚拟法线，UI 平面默认朝向为 (0,0,-1)</param>
        /// <param name="rollDegrees">绕法线自身的旋转角（度）</param>
        public void ApplyPlane(Vector3 normal, float rollDegrees = 0f)
        {
            Vector2[] c = XericQuadDeformMath.PlaneCorners(normal, rollDegrees);
            ApplyCorners(c[0], c[1], c[2], c[3]);
        }

        /// <summary>清除变形，恢复未变形的矩形</summary>
        public void ResetDeform()
        {
            m_CornerOffsetBL = Vector2.zero;
            m_CornerOffsetTL = Vector2.zero;
            m_CornerOffsetTR = Vector2.zero;
            m_CornerOffsetBR = Vector2.zero;
            SetVerticesDirty();
        }

        /// <summary>
        /// 取当前变形的映射器。基准矩形与 Image 生成网格时用的那个同源
        /// （<see cref="Graphic.GetPixelAdjustedRect"/>）；变形数据退化时返回 false。
        /// </summary>
        public bool TryGetDeformMapper(out XericQuadMapper mapper)
            => XericQuadMapper.TryCreate(GetCorner(0), GetCorner(1), GetCorner(2), GetCorner(3),
                GetPixelAdjustedRect(), out mapper);

        /// <summary>归一化矩形空间 -> RectTransform 局部坐标</summary>
        public Vector2 NormalizedToLocal(Vector2 normalized)
            => XericQuadDeformMath.NormalizedToLocal(normalized, GetPixelAdjustedRect());

        /// <summary>RectTransform 局部坐标 -> 归一化矩形空间</summary>
        public Vector2 LocalToNormalized(Vector2 local)
        {
            Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f)
                return Vector2.zero;
            return XericQuadDeformMath.LocalToNormalized(local, rect);
        }

        /// <summary>取某个角点的局部坐标（场景编辑手柄与网格生成共用同一套换算）</summary>
        public Vector2 GetCornerLocal(int index) => NormalizedToLocal(GetCorner(index));

        private void ApplyCorners(Vector2 cornerBL, Vector2 cornerTL, Vector2 cornerTR, Vector2 cornerBR)
        {
            m_CornerOffsetBL = cornerBL - IdentityCorner(0);
            m_CornerOffsetTL = cornerTL - IdentityCorner(1);
            m_CornerOffsetTR = cornerTR - IdentityCorner(2);
            m_CornerOffsetBR = cornerBR - IdentityCorner(3);
            SetVerticesDirty();
        }

        #endregion

        #region 网格生成

        protected override void OnPopulateMesh(VertexHelper toFill)
        {
            base.OnPopulateMesh(toFill);

            if (!HasDeform || toFill.currentVertCount == 0)
                return;

            // 退化四边形（自交/零面积）时退回未变形网格，避免把 NaN 顶点写进 CanvasRenderer。
            // 顶点坐标活在局部空间、变形数据活在归一化矩形空间，那次换乘只在
            // XericQuadMapper 里实现一次（曾经在这里手写换算，漏掉回程就整块塌成 1 像素）。
            if (!TryGetDeformMapper(out XericQuadMapper mapper))
                return;

            var vertex = UIVertex.simpleVert;
            for (int i = 0; i < toFill.currentVertCount; i++)
            {
                toFill.PopulateUIVertex(ref vertex, i);

                Vector2 mapped = mapper.MapLocal(vertex.position);
                vertex.position = new Vector3(mapped.x, mapped.y, vertex.position.z);
                toFill.SetUIVertex(vertex, i);
            }
        }

        #endregion

        #region 组件替换
#if UNITY_EDITOR
        [UnityEditor.MenuItem(XericRegistrationPaths.ContextPrefix + "Image/Replace To/Xeric Image", true)]
        private static bool ValidateReplaceToXeric(UnityEditor.MenuCommand command)
            => command.context is Image and not XericImage;

        [UnityEditor.MenuItem(XericRegistrationPaths.ContextPrefix + "Image/Replace To/Xeric Image", false, 10)]
        private static void ReplaceToXeric(UnityEditor.MenuCommand command) 
            => command.ReplaceCommandContext<Image, XericImage, Sprite>(
                o =>
                    o.sprite,
                (t, d) =>
                {
                    t.sprite = d;
                });

        [UnityEditor.MenuItem(XericRegistrationPaths.ContextPrefix + "Image/Replace To/Unity Image (Native)", true)]
        private static bool ValidateReplaceToUnity(UnityEditor.MenuCommand command)
            => command.context is XericImage;

        [UnityEditor.MenuItem(XericRegistrationPaths.ContextPrefix + "Image/Replace To/Unity Image (Native)", false, 100)]
        private static void ReplaceToUnity(UnityEditor.MenuCommand command) 
            => command.ReplaceCommandContext<XericImage, Image, Sprite>(
                o =>
                    o.sprite,
                (t, d) =>
                {
                    t.sprite = d;
                });
#endif
        #endregion
    }
}