using System.Collections.Generic;
using UnityEngine;
using XericLibrary.Runtime.UIGraph;

namespace XericUI.XChart.Pseudo3D
{
    /// <summary>
    /// 伪 3D 立方体背景工具。
    ///
    /// 使用 UILineRendererV2 绘制：
    ///   1. 立方体 12 条棱的线框（前向棱使用实色，后向棱可选半透明）
    ///   2. 三个可见面上的轴标线网格
    ///
    /// 3D 坐标系统：
    ///   立方体中心在原点，半边长 1（即 [-1, 1] 范围）。
    ///   通过 pitch/yaw 旋转后进行正交投影（丢弃 Z 轴）得到 2D UI 坐标。
    ///   最终通过 scale + offset 映射到 RectTransform 的像素空间。
    ///
    /// 子节点结构：
    ///   [ChartTool_CubeBg]
    ///     └─ CubeRenderer (UILineRendererV2)
    /// </summary>
    [AddComponentMenu("Xeric/XChart/Pseudo3D/Cube Background Tool")]
    public class ChartToolCubeBg : XChartToolBase
    {
        // ── 外观配置 ──────────────────────────────────────────────

        /// <summary>立方体半边长（UI 像素）</summary>
        public float CubeHalfSize { get; set; } = 150f;

        /// <summary>俯仰角（绕 X 轴旋转，度数）</summary>
        public float Pitch { get; set; } = 25f;

        /// <summary>偏航角（绕 Y 轴旋转，度数）</summary>
        public float Yaw { get; set; } = -30f;

        /// <summary>前向棱颜色</summary>
        public Color FrontEdgeColor { get; set; } = new Color(0.7f, 0.7f, 0.7f, 1f);

        /// <summary>后向棱颜色</summary>
        public Color BackEdgeColor { get; set; } = new Color(0.4f, 0.4f, 0.4f, 0.4f);

        /// <summary>棱线宽度</summary>
        public float EdgeThickness { get; set; } = 2f;

        /// <summary>网格线颜色</summary>
        public Color GridLineColor { get; set; } = new Color(0.5f, 0.5f, 0.5f, 0.3f);

        /// <summary>网格线宽度</summary>
        public float GridLineThickness { get; set; } = 1f;

        /// <summary>每轴网格分割数</summary>
        public int GridDivisions { get; set; } = 5;

        /// <summary>是否显示后向棱</summary>
        public bool ShowBackEdges { get; set; } = true;

        /// <summary>是否显示网格标线</summary>
        public bool ShowGridLines { get; set; } = true;

        /// <summary>立方体中心偏移</summary>
        public Vector2 CenterOffset { get; set; } = Vector2.zero;

        // ── 内部组件 ──────────────────────────────────────────────

        /// <summary>棱线渲染器</summary>
        public UILineRendererV2 EdgeRenderer { get; private set; }

        /// <summary>网格线渲染器</summary>
        public UILineRendererV2 GridRenderer { get; private set; }

        // ── 3D 管线状态 ──────────────────────────────────────────

        // 8 个立方体顶点（中心在原点，半边长 1）
        private static readonly Vector3[] s_CubeVertices = new Vector3[]
        {
            new Vector3(-1, -1, -1), // 0: 后下左
            new Vector3( 1, -1, -1), // 1: 后下右
            new Vector3( 1,  1, -1), // 2: 后上右
            new Vector3(-1,  1, -1), // 3: 后上左
            new Vector3(-1, -1,  1), // 4: 前下左
            new Vector3( 1, -1,  1), // 5: 前下右
            new Vector3( 1,  1,  1), // 6: 前上右
            new Vector3(-1,  1,  1), // 7: 前上左
        };

        // 12 条棱（顶点索引对）
        private static readonly int[,] s_CubeEdges = new int[12, 2]
        {
            {0,1}, {1,2}, {2,3}, {3,0}, // 后面
            {4,5}, {5,6}, {6,7}, {7,4}, // 前面
            {0,4}, {1,5}, {2,6}, {3,7}, // 连接棱
        };

        // 6 个面的顶点索引 + 法线
        private struct FaceDef { public int[] verts; public Vector3 normal; }
        private static readonly FaceDef[] s_Faces = new FaceDef[]
        {
            new FaceDef { verts = new int[] {4,5,6,7}, normal = new Vector3(0, 0, 1) },  // 前
            new FaceDef { verts = new int[] {0,1,2,3}, normal = new Vector3(0, 0,-1) },  // 后
            new FaceDef { verts = new int[] {1,5,6,2}, normal = new Vector3(1, 0, 0) },  // 右
            new FaceDef { verts = new int[] {0,4,7,3}, normal = new Vector3(-1,0, 0) },  // 左
            new FaceDef { verts = new int[] {2,6,7,3}, normal = new Vector3(0, 1, 0) },  // 上
            new FaceDef { verts = new int[] {0,1,5,4}, normal = new Vector3(0,-1, 0) },  // 下
        };

        // 每个面由两个轴定义（用于网格线方向）
        private static readonly Vector3[] s_FaceAxisA = new Vector3[]
        {
            new Vector3(1,0,0), new Vector3(1,0,0), // 前/后: X 轴
            new Vector3(0,1,0), new Vector3(0,1,0), // 右/左: Y 轴
            new Vector3(1,0,0), new Vector3(1,0,0), // 上/下: X 轴
        };
        private static readonly Vector3[] s_FaceAxisB = new Vector3[]
        {
            new Vector3(0,1,0), new Vector3(0,1,0), // 前/后: Y 轴
            new Vector3(0,0,1), new Vector3(0,0,1), // 右/左: Z 轴
            new Vector3(0,0,1), new Vector3(0,0,1), // 上/下: Z 轴
        };

        // ── 投影状态 ──────────────────────────────────────────────

        private Vector3[] m_RotatedVerts = new Vector3[8]; // 旋转后的 3D 顶点
        private Vector2[] m_ProjectedVerts = new Vector2[8]; // 投影后的 2D 顶点
        private bool m_ProjectionDirty = true;

        /// <summary>投影版本号（每次投影矩阵变化时递增，供外部检测变化）</summary>
        public int ProjectionVersion { get; private set; } = 0;

        private Matrix4x4 m_RotationMatrix;
        private float m_Scale;
        private Vector2 m_Offset;

        // ── 初始化 ────────────────────────────────────────────────

        protected override void OnInitialize()
        {
            // 创建棱线渲染器
            EdgeRenderer = CreateChildComponent<UILineRendererV2>("CubeEdgeRenderer");
            if (EdgeRenderer != null)
            {
                EdgeRenderer.defaultColor = FrontEdgeColor;
                EdgeRenderer.defaultThickness = EdgeThickness;
                var rt = EdgeRenderer.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            // 创建网格线渲染器
            GridRenderer = CreateChildComponent<UILineRendererV2>("CubeGridRenderer");
            if (GridRenderer != null)
            {
                GridRenderer.defaultColor = GridLineColor;
                GridRenderer.defaultThickness = GridLineThickness;
                var rt = GridRenderer.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            SetVisible(true);
        }

        // ── 更新投影矩阵 ──────────────────────────────────────────

        private void UpdateProjection()
        {
            if (!m_ProjectionDirty) return;
            m_ProjectionDirty = false;

            // 1. 构建旋转矩阵：先绕 X 轴（pitch），再绕 Y 轴（yaw）
            float pitchRad = Pitch * Mathf.Deg2Rad;
            float yawRad = Yaw * Mathf.Deg2Rad;
            m_RotationMatrix = Matrix4x4.Rotate(
                Quaternion.Euler(pitchRad * Mathf.Rad2Deg, yawRad * Mathf.Rad2Deg, 0));

            // 2. 旋转所有顶点
            for (int i = 0; i < 8; i++)
                m_RotatedVerts[i] = m_RotationMatrix.MultiplyPoint3x4(s_CubeVertices[i]);

            // 3. 计算投影后的 2D 范围（丢弃 Z）
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                float px = m_RotatedVerts[i].x;
                float py = m_RotatedVerts[i].y;
                if (px < minX) minX = px;
                if (px > maxX) maxX = px;
                if (py < minY) minY = py;
                if (py > maxY) maxY = py;
            }

            // 4. 计算缩放和偏移，使立方体适配 CubeHalfSize
            float rangeX = maxX - minX;
            float rangeY = maxY - minY;
            float maxRange = Mathf.Max(rangeX, rangeY);
            m_Scale = (maxRange > 0.001f) ? (CubeHalfSize * 2f / maxRange) : 1f;

            // 中心对齐到 CenterOffset
            float centerX = (minX + maxX) * 0.5f;
            float centerY = (minY + maxY) * 0.5f;
            m_Offset = new Vector2(
                CenterOffset.x - centerX * m_Scale,
                CenterOffset.y - centerY * m_Scale);

            // 5. 计算最终投影坐标
            for (int i = 0; i < 8; i++)
            {
                m_ProjectedVerts[i] = new Vector2(
                    m_RotatedVerts[i].x * m_Scale + m_Offset.x,
                    m_RotatedVerts[i].y * m_Scale + m_Offset.y);
            }
        }

        /// <summary>标记投影需要重新计算</summary>
        public void MarkProjectionDirty()
        {
            m_ProjectionDirty = true;
            ProjectionVersion++;
        }

        // ── 3D 投影辅助 ──────────────────────────────────────────

        /// <summary>将 3D 点投影到 2D</summary>
        public Vector2 ProjectPoint3D(Vector3 point3d)
        {
            UpdateProjection();
            Vector3 rotated = m_RotationMatrix.MultiplyPoint3x4(point3d);
            return new Vector2(rotated.x * m_Scale + m_Offset.x,
                               rotated.y * m_Scale + m_Offset.y);
        }

        /// <summary>批量投影 3D 点到 2D</summary>
        public Vector2[] ProjectPoints3D(Vector3[] points3d)
        {
            UpdateProjection();
            var result = new Vector2[points3d.Length];
            for (int i = 0; i < points3d.Length; i++)
            {
                Vector3 rotated = m_RotationMatrix.MultiplyPoint3x4(points3d[i]);
                result[i] = new Vector2(rotated.x * m_Scale + m_Offset.x,
                                        rotated.y * m_Scale + m_Offset.y);
            }
            return result;
        }

        /// <summary>获取当前旋转矩阵</summary>
        public Matrix4x4 RotationMatrix => m_RotationMatrix;

        /// <summary>获取当前缩放</summary>
        public float Scale => m_Scale;

        /// <summary>获取当前偏移</summary>
        public Vector2 Offset => m_Offset;

        // ── 渲染 ──────────────────────────────────────────────────

        public override void OnRender()
        {
            base.OnRender();
            if (EdgeRenderer == null) return;

            UpdateProjection();

            // 绘制棱线
            DrawEdges();

            // 绘制网格标线
            if (GridRenderer != null)
            {
                if (ShowGridLines)
                {
                    DrawGridLines();
                    GridRenderer.RootObject.SetActive(true);
                }
                else
                {
                    GridRenderer.RootObject.SetActive(false);
                }
            }
        }

        // ── 棱线绘制 ──────────────────────────────────────────────

        private void DrawEdges()
        {
            EdgeRenderer.ClearAll();

            // 确定前向棱：棱的两个顶点中，Z 值较大的为前向
            // 更准确：棱所属的面中，法线 Z > 0 的面为可见面
            // 简单方法：棱的两个顶点的 rotated Z 平均值 > 0 为前向
            var frontVerts = new List<LineVertexData>();
            var backVerts = new List<LineVertexData>();

            for (int e = 0; e < 12; e++)
            {
                int v0 = s_CubeEdges[e, 0];
                int v1 = s_CubeEdges[e, 1];
                float avgZ = (m_RotatedVerts[v0].z + m_RotatedVerts[v1].z) * 0.5f;
                bool isFront = avgZ >= 0;

                var list = isFront ? frontVerts : backVerts;
                int startIdx = list.Count;

                list.Add(new LineVertexData
                {
                    position = m_ProjectedVerts[v0],
                    color = isFront ? FrontEdgeColor : BackEdgeColor,
                    thickness = EdgeThickness,
                    userData = Vector4.zero,
                });
                list.Add(new LineVertexData
                {
                    position = m_ProjectedVerts[v1],
                    color = isFront ? FrontEdgeColor : BackEdgeColor,
                    thickness = EdgeThickness,
                    userData = Vector4.zero,
                });
            }

            // 先画后向棱（在底层），再画前向棱（在上层）
            if (ShowBackEdges && backVerts.Count > 0)
            {
                // 每两个顶点构成一条线段
                for (int i = 0; i < backVerts.Count; i += 2)
                {
                    EdgeRenderer.DrawLine(
                        backVerts[i].position,
                        backVerts[i + 1].position,
                        backVerts[i].color,
                        backVerts[i].thickness);
                }
            }

            for (int i = 0; i < frontVerts.Count; i += 2)
            {
                EdgeRenderer.DrawLine(
                    frontVerts[i].position,
                    frontVerts[i + 1].position,
                    frontVerts[i].color,
                    frontVerts[i].thickness);
            }
        }

        // ── 网格标线绘制 ──────────────────────────────────────────

        private void DrawGridLines()
        {
            GridRenderer.ClearAll();

            // 获取 3 个可见面（法线 Z 值最大的 3 个面）
            var visibleFaces = GetVisibleFaces();
            if (visibleFaces.Count == 0) return;

            foreach (int faceIdx in visibleFaces)
            {
                DrawGridLinesOnFace(faceIdx);
            }
        }

        /// <summary>获取可见面索引列表（按可见度排序，最多 3 个）</summary>
        private List<int> GetVisibleFaces()
        {
            var result = new List<int>(3);
            var faceScores = new (int index, float score)[6];

            for (int i = 0; i < 6; i++)
            {
                // 将面法线旋转到当前视角
                Vector3 rotatedNormal = m_RotationMatrix.MultiplyVector(s_Faces[i].normal);
                // Z 分量越大，越面向用户
                faceScores[i] = (i, rotatedNormal.z);
            }

            // 按 Z 值降序排列（Z 越大越面向用户）
            System.Array.Sort(faceScores, (a, b) => b.score.CompareTo(a.score));

            // 取前 3 个且 Z > 0 的面
            for (int i = 0; i < 3 && faceScores[i].score > 0; i++)
                result.Add(faceScores[i].index);

            return result;
        }

        /// <summary>在指定面上绘制网格线</summary>
        private void DrawGridLinesOnFace(int faceIdx)
        {
            var face = s_Faces[faceIdx];
            Vector3 axisA = s_FaceAxisA[faceIdx];
            Vector3 axisB = s_FaceAxisB[faceIdx];

            // 面的四个顶点（在立方体局部坐标中）
            Vector3 v0 = s_CubeVertices[face.verts[0]];
            Vector3 v1 = s_CubeVertices[face.verts[1]];
            Vector3 v2 = s_CubeVertices[face.verts[2]];
            Vector3 v3 = s_CubeVertices[face.verts[3]];

            // 面的范围
            float minA = float.MaxValue, maxA = float.MinValue;
            float minB = float.MaxValue, maxB = float.MinValue;

            // 计算顶点在轴上的投影
            float GetAxisValue(Vector3 v, Vector3 axis) => Vector3.Dot(v, axis);

            for (int i = 0; i < 4; i++)
            {
                var v = s_CubeVertices[face.verts[i]];
                float va = GetAxisValue(v, axisA);
                float vb = GetAxisValue(v, axisB);
                if (va < minA) minA = va;
                if (va > maxA) maxA = va;
                if (vb < minB) minB = vb;
                if (vb > maxB) maxB = vb;
            }

            // 沿 A 轴画线（平行于 B 轴）
            float stepA = (maxA - minA) / GridDivisions;
            float stepB = (maxB - minB) / GridDivisions;

            // 沿 A 轴方向的网格线
            for (int i = 0; i <= GridDivisions; i++)
            {
                float aVal = minA + i * stepA;
                // 线的两个端点
                Vector3 p1 = face.normal + axisA * aVal + axisB * minB;
                Vector3 p2 = face.normal + axisA * aVal + axisB * maxB;

                Vector2 pp1 = ProjectPoint3D(p1);
                Vector2 pp2 = ProjectPoint3D(p2);

                // 只绘制落在面范围内的线
                GridRenderer.DrawLine(pp1, pp2, GridLineColor, GridLineThickness);
            }

            // 沿 B 轴方向的网格线
            for (int i = 0; i <= GridDivisions; i++)
            {
                float bVal = minB + i * stepB;
                Vector3 p1 = face.normal + axisA * minA + axisB * bVal;
                Vector3 p2 = face.normal + axisA * maxA + axisB * bVal;

                Vector2 pp1 = ProjectPoint3D(p1);
                Vector2 pp2 = ProjectPoint3D(p2);

                GridRenderer.DrawLine(pp1, pp2, GridLineColor, GridLineThickness);
            }
        }

        // ── 清理 ──────────────────────────────────────────────────

        protected override void OnDispose()
        {
            EdgeRenderer = null;
            GridRenderer = null;
        }
    }
}