using UnityEngine;
using XericUI.XChart.Space;

namespace XericUI.XChart.Mesh
{
    /// <summary>
    /// 伪立方体的几何来源：8 顶点 / 12 棱 / 6 面，以及"把面算成屏幕四边形"的推导方法。
    ///
    /// <para><b>为什么它决定协议</b></para>
    /// 我们要的不是"立方体"，而是"任意一个面在屏幕上是哪四个点"。把它写出来就能看清协议该长什么样：
    /// <code>
    ///   面局部 3D  :  p3(s,t) = O3 + s * S3 + t * T3        (s,t ∈ [0,1])
    ///   投影       :  screen  = (R * p3).xy * scale + offset  ← 正交投影，丢 Z
    ///   合成       :  screen  = O2 + s * S2 + t * T2          ← 关于 (s,t) 是<b>仿射</b>的
    /// </code>
    /// 也就是说：正交投影下，每个面的屏幕映射必定是仿射（平行四边形），只需要"原点 + 两个基向量"；
    /// 一旦换成透视投影（除以 w），它升级成一般四边形/单应。协议因此只需四个角点，
    /// 不需要知道立方体、pitch/yaw、透视——详见 <see cref="XChartFaceFrame"/>。</para>
    ///
    /// <para><b>坐标与单位</b>：立方体中心在原点、半边长 1（顶点在 <c>[-1,1]³</c>）；S3/T3 是<b>整条边</b>
    /// 向量（长度 2），所以 s,t 直接就是 [0,1] 的面参数。因为顶点就在 ±1，投影缩放
    /// <c>scale</c> 在"半边长口径"下<b>就等于半边长（像素）</b>——见
    /// <see cref="XChartCubeView"/>，旋转与尺寸都收在那里，本类只负责顶点和可见性判定。</para>
    ///
    /// <para><b>与 ChartToolCubeBg / ChartToolMeshBg 的关系</b>：面顺序与本类的枚举一致
    /// （Front/Back/Right/Left/Top/Bottom），两个工具都消费本类的顶点/面/棱表，
    /// 不再各自带一份私有副本。</para>
    /// </summary>
    public static class XChartCubeGeometry
    {
        /// <summary>面的枚举，顺序与 <see cref="FaceCount"/> 个面帧的输出顺序一致</summary>
        public enum CubeFace
        {
            /// <summary>法线 (0,0,1)，面参数 (s,t) = (X,Y)</summary>
            Front = 0,
            /// <summary>法线 (0,0,-1)，面参数 (s,t) = (-X,Y)</summary>
            Back = 1,
            /// <summary>法线 (1,0,0)，面参数 (s,t) = (-Z,Y)</summary>
            Right = 2,
            /// <summary>法线 (-1,0,0)，面参数 (s,t) = (Z,Y)</summary>
            Left = 3,
            /// <summary>法线 (0,1,0)，面参数 (s,t) = (X,Z)（当作地板用）</summary>
            Top = 4,
            /// <summary>法线 (0,-1,0)，面参数 (s,t) = (X,Z)</summary>
            Bottom = 5,
        }

        public const int FaceCount = 6;
        public const int EdgeCount = 12;

        // ── 顶点与棱 ──────────────────────────────────────────────

        /// <summary>8 个顶点（中心原点、半边长 1）</summary>
        public static readonly Vector3[] Vertices =
        {
            new Vector3(-1, -1, -1), // 0 后下左
            new Vector3( 1, -1, -1), // 1 后下右
            new Vector3( 1,  1, -1), // 2 后上右
            new Vector3(-1,  1, -1), // 3 后上左
            new Vector3(-1, -1,  1), // 4 前下左
            new Vector3( 1, -1,  1), // 5 前下右
            new Vector3( 1,  1,  1), // 6 前上右
            new Vector3(-1,  1,  1), // 7 前上左
        };

        /// <summary>12 条棱（顶点索引对）：0-3 后面，4-7 前面，8-11 连接棱</summary>
        public static readonly int[,] Edges =
        {
            {0,1}, {1,2}, {2,3}, {3,0},
            {4,5}, {5,6}, {6,7}, {7,4},
            {0,4}, {1,5}, {2,6}, {3,7},
        };

        // ── 面帧（3D） ────────────────────────────────────────────

        private static readonly Vector3[] s_FaceNormal = new Vector3[FaceCount];
        private static readonly Vector3[] s_FaceOrigin = new Vector3[FaceCount]; // (s,t)=(0,0)
        private static readonly Vector3[] s_FaceAxisS = new Vector3[FaceCount];  // 整条边，长度 2
        private static readonly Vector3[] s_FaceAxisT = new Vector3[FaceCount];  // 整条边，长度 2

        // 每个面的四个角点对应的顶点索引，顺序与 (s,t) 角点一致： (0,0) (0,1) (1,1) (1,0)
        // 不手写这张表：由 3D 角点与 Vertices 比对得出（探针里有"每面四角恰落在顶点上"的断言兜底）
        private static readonly int[,] s_FaceVertexIndices = new int[FaceCount, 4];

        /// <summary>棱 -> 相邻面（每面两条）. 由面顶点索引反查得出，供"棱是否朝向观察者"判定</summary>
        private static readonly int[,] s_EdgeAdjacentFaces = new int[EdgeCount, 2];

        static XChartCubeGeometry()
        {
            for (int i = 0; i < FaceCount; i++)
                BuildFace3D((CubeFace)i, out s_FaceNormal[i], out s_FaceOrigin[i], out s_FaceAxisS[i], out s_FaceAxisT[i]);

            for (int f = 0; f < FaceCount; f++)
            {
                for (int k = 0; k < 4; k++)
                {
                    float s = (k == 2 || k == 3) ? 1f : 0f;
                    float t = (k == 1 || k == 2) ? 1f : 0f;
                    s_FaceVertexIndices[f, k] = FindVertexIndex(GetFacePoint3D((CubeFace)f, s, t));
                }
            }

            for (int e = 0; e < EdgeCount; e++)
            {
                s_EdgeAdjacentFaces[e, 0] = -1;
                s_EdgeAdjacentFaces[e, 1] = -1;
                int slot = 0;
                for (int f = 0; f < FaceCount && slot < 2; f++)
                {
                    if (!FaceContainsEdge(f, Edges[e, 0], Edges[e, 1])) continue;
                    s_EdgeAdjacentFaces[e, slot++] = f;
                }
            }
        }

        private static int FindVertexIndex(Vector3 point)
        {
            int best = -1;
            float bestDistance = 1e-6f;
            for (int i = 0; i < Vertices.Length; i++)
            {
                float d = (point - Vertices[i]).sqrMagnitude;
                if (d <= bestDistance) { bestDistance = d; best = i; }
            }
            return best;
        }

        private static bool FaceContainsEdge(int face, int v0, int v1)
        {
            return FaceContainsVertex(face, v0) && FaceContainsVertex(face, v1);
        }

        private static bool FaceContainsVertex(int face, int vertex)
        {
            for (int k = 0; k < 4; k++)
                if (s_FaceVertexIndices[face, k] == vertex) return true;
            return false;
        }

        /// <summary>
        /// 由"外法线 + 参考上方向"导出面的 3D 帧。
        /// 一致的右手规则：right = up x n，t = n x right，原点取该面的 (0,0) 角点。
        /// 侧面用世界 Y 当参考上方向，顶/底面用 Z（这样顶面天然是 (X,Z) 地板参数化）。
        /// </summary>
        private static void BuildFace3D(CubeFace face, out Vector3 normal, out Vector3 origin,
            out Vector3 axisS, out Vector3 axisT)
        {
            normal = GetFaceNormalVector(face);
            Vector3 upReference = (face == CubeFace.Top || face == CubeFace.Bottom)
                ? new Vector3(0f, 0f, 1f)
                : new Vector3(0f, 1f, 0f);

            Vector3 right = Vector3.Cross(upReference, normal).normalized;
            Vector3 up = Vector3.Cross(normal, right).normalized;

            // 面在 ±1 的立方体上，所以 (0,0) 角点 = 法线方向的面 + 反向的两个轴
            origin = normal - right - up;
            axisS = right * 2f;
            axisT = up * 2f;
        }

        private static Vector3 GetFaceNormalVector(CubeFace face)
        {
            switch (face)
            {
                case CubeFace.Front: return new Vector3(0f, 0f, 1f);
                case CubeFace.Back: return new Vector3(0f, 0f, -1f);
                case CubeFace.Right: return new Vector3(1f, 0f, 0f);
                case CubeFace.Left: return new Vector3(-1f, 0f, 0f);
                case CubeFace.Top: return new Vector3(0f, 1f, 0f);
                default: return new Vector3(0f, -1f, 0f);
            }
        }

        /// <summary>面的外法线（立方体局部空间）</summary>
        public static Vector3 GetFaceNormal(CubeFace face) => s_FaceNormal[(int)face];

        /// <summary>面参数 (0,0) 对应的 3D 点</summary>
        public static Vector3 GetFaceOrigin(CubeFace face) => s_FaceOrigin[(int)face];

        /// <summary>面参数 s 方向的 3D 边向量（长度 2）</summary>
        public static Vector3 GetFaceAxisS(CubeFace face) => s_FaceAxisS[(int)face];

        /// <summary>面参数 t 方向的 3D 边向量（长度 2）</summary>
        public static Vector3 GetFaceAxisT(CubeFace face) => s_FaceAxisT[(int)face];

        /// <summary>
        /// 面片四个角（<b>单位立方体</b>，±1 就是边界），角序与面帧完全一致
        /// （ST00→(s0,t0)、ST01→(s0,t1)、ST11→(s1,t1)、ST10→(s1,t0)）。
        /// 定义网格的工具把这四个点发布成 `Quad3D` 插槽，消费者用自己的矩阵投影它们即可——
        /// 这条与 <see cref="BuildFaceFrames"/> 的角序必须一直一致（探针把两者对拍）。
        /// </summary>
        public static void GetFaceCorners(CubeFace face, out Vector3 cornerST00, out Vector3 cornerST01,
            out Vector3 cornerST11, out Vector3 cornerST10)
        {
            Vector3 origin = s_FaceOrigin[(int)face];
            Vector3 axisS = s_FaceAxisS[(int)face];
            Vector3 axisT = s_FaceAxisT[(int)face];
            cornerST00 = origin;
            cornerST01 = origin + axisT;
            cornerST11 = origin + axisT + axisS;
            cornerST10 = origin + axisS;
        }

        /// <summary>插槽 id 约定：定义网格的工具把每个面发布成 <c>mesh.face.&lt;面号&gt;</c></summary>
        public const string FaceSlotPrefix = "mesh.face.";

        /// <summary>某个面的插槽 id</summary>
        public static string FaceSlotId(CubeFace face) => FaceSlotPrefix + (int)face;

        /// <summary>
        /// 格线插槽 id：<c>mesh.face.&lt;面号&gt;.grid.S</c>（沿 S 的等 t 线）/
        /// <c>.grid.T</c>（沿 T 的等 s 线）。格线由<b>定义网格的那个工具</b>发布，轴线按 id 取用——
        /// 于是"刻度落在格线上"这件事有一个共同的来源，而不是两边各自算一遍。
        /// </summary>
        public static string GridSlotId(CubeFace face, bool alongS)
            => FaceSlotPrefix + (int)face + (alongS ? ".grid.S" : ".grid.T");

        /// <summary>
        /// 一条棱插槽 id：<c>mesh.face.&lt;面号&gt;.side.&lt;边号&gt;</c>，一个面 4 条 ⇒ 全立方体 6×4＝24 个。
        ///
        /// <para><b>为什么是 24 而不是 12</b>（他 2026-10-26 数的）：一根立方体棱被两个面共享，而"轴贴着这条棱画"
        /// 需要的是<b>面内</b>的两件事——沿棱的方向、以及从棱往面内走的横方向。同一根棱从不同面看，
        /// 后者完全不同，所以 12 条棱表达不出"哪一面的那条棱"。命名按 <c>面 + 边</c> 来，边号见
        /// <see cref="GetSideQuad"/>（0＝底、1＝左、2＝顶、3＝右，绕序逆时针）。</para>
        ///
        /// <para>插槽的<b>值</b>还是同一个面的四个角、只是把选中的那条边旋到面帧的底边（S 轴、t=0）——
        /// 于是它仍是一个 <c>Quad3D</c>，消费者（轴线）的 AlongS／CrossRatio 一行都不用改。</para>
        /// </summary>
        public const int SideCount = 4;

        public static string SideSlotId(CubeFace face, int side)
            => FaceSlotPrefix + (int)face + ".side." + side;

        /// <summary>边的名字（面板与报表用；绕序逆时针，0 是面帧 S 轴那条底边）</summary>
        public static string SideName(int side)
        {
            switch (side)
            {
                case 0: return "底边";
                case 1: return "左边";
                case 2: return "顶边";
                case 3: return "右边";
                default: return "边" + side;
            }
        }

        /// <summary>
        /// 第 <paramref name="side"/> 条边旋到面帧底边后的四角（仍是 <c>ST00/ST01/ST11/ST10</c> 顺序，
        /// 因此新面帧的 <b>S 轴就是那条棱</b>、<b>T 轴就是"从棱往面内走"</b>；绕序不变 ⇒ 不会镜像）。
        /// 做法是纯旋转：<see cref="GetFaceCorners"/> 的四角按 00→01→11→10 成环，环上转 side 格。
        /// </summary>
        public static void GetSideQuad(CubeFace face, int side, out Vector3 cornerST00, out Vector3 cornerST01,
            out Vector3 cornerST11, out Vector3 cornerST10)
        {
            Vector3 c00, c01, c11, c10;
            GetFaceCorners(face, out c00, out c01, out c11, out c10);
            var ring = new[] { c00, c01, c11, c10 };
            int step = ((side % SideCount) + SideCount) % SideCount;
            cornerST00 = ring[step];
            cornerST01 = ring[(step + 1) % SideCount];
            cornerST11 = ring[(step + 2) % SideCount];
            cornerST10 = ring[(step + 3) % SideCount];
        }

        /// <summary>
        /// 一组格线要占多少个 float（<paramref name="lineCount"/> 组 × 每组两个端点 × 3 个分量）：
        /// 两个背景都按它开缓冲，"值布局"只有这一个来源。
        /// </summary>
        public static int GridSlotValueCount(int lineCount)
        {
            if (lineCount <= 0) return 0;
            return lineCount * XChartSlotModes.PointsPerGroup(XChartSlotMode.Line3D) * 3;
        }

        /// <summary>
        /// 按给定的一组面参数比例把格线写进 <paramref name="values"/>：
        /// <b>每组 6 个 float（两个单位立方体端点）</b>，共 <paramref name="lineCount"/> 组。
        ///
        /// <para>比例来自 <see cref="XChartGridLines"/>（等分，或按数据范围算出来的刻度位置）——
        /// 这里只管"比例 → 端点"，于是"格线在哪儿"只有一个实现、两个背景共用，离线探针钉得住
        /// （轴线对齐格线的判据也靠它同源）。</para>
        ///
        /// <para><b>沿 S 的线 = 等 t 线</b>（从 (0,t) 到 (1,t)），沿 T 的线 = 等 s 线——
        /// 与面帧 <c>GetLineAlongS/GetLineAlongT</c> 同一条约定（写反了就是整片网格转 90°）。</para>
        /// </summary>
        public static int FillGridLineValues(CubeFace face, bool alongS, float[] ratios, int lineCount, float[] values)
        {
            if (values == null || ratios == null || lineCount <= 0) return 0;
            if (values.Length < GridSlotValueCount(lineCount)) return 0;

            int written = 0;
            for (int i = 0; i < lineCount; i++)
            {
                float ratio = ratios[i];
                Vector3 from = alongS ? GetFacePoint3D(face, 0f, ratio) : GetFacePoint3D(face, ratio, 0f);
                Vector3 to = alongS ? GetFacePoint3D(face, 1f, ratio) : GetFacePoint3D(face, ratio, 1f);

                int at = i * 6;
                values[at + 0] = from.x; values[at + 1] = from.y; values[at + 2] = from.z;
                values[at + 3] = to.x; values[at + 4] = to.y; values[at + 5] = to.z;
                written++;
            }
            return written;
        }

        /// <summary>
        /// 面参数 (s,t) -> 立方体局部 3D 点
        /// </summary>
        public static Vector3 GetFacePoint3D(CubeFace face, float s, float t)
            => s_FaceOrigin[(int)face] + s_FaceAxisS[(int)face] * s + s_FaceAxisT[(int)face] * t;

        /// <summary>
        /// 立方体局部 3D 点 → 面参数 (s,t)：<b>与 <see cref="GetFacePoint3D"/> 互为逆</b>。
        /// 面基是单位正交的（角的定义就是 origin / +axisT / +axisT+axisS / +axisS），所以直接点积即可。
        ///
        /// <para>谁需要它：轴线要按网格<b>发布的线</b>（单位立方体端点）反解出"这条线在面参数的哪儿"，
        /// 才能把刻度吸附上去——于是"网格定义、轴线跟随"落地，而两边不各算一遍。</para>
        /// </summary>
        public static void GetFaceParameter(CubeFace face, Vector3 point, out float s, out float t)
        {
            Vector3 axisS = s_FaceAxisS[(int)face];
            Vector3 axisT = s_FaceAxisT[(int)face];
            Vector3 relative = point - s_FaceOrigin[(int)face];

            // 面轴是"整条边"，长度 2（±1 的立方体）⇒ 点积后要**除以 |轴|²** 才是参数。
            // 少了这一步，参数会整整齐齐地放大 4 倍（看着像对的，其实全偏了）。
            float lengthS = axisS.sqrMagnitude;
            float lengthT = axisT.sqrMagnitude;
            s = lengthS > 1e-8f ? Vector3.Dot(relative, axisS) / lengthS : 0f;
            t = lengthT > 1e-8f ? Vector3.Dot(relative, axisT) / lengthT : 0f;
        }

        /// <summary>面的第 corner 个角点（0=(0,0) 1=(0,1) 2=(1,1) 3=(1,0)）对应的顶点索引</summary>
        public static int GetFaceVertexIndex(CubeFace face, int corner)
            => s_FaceVertexIndices[(int)face, Mathf.Clamp(corner, 0, 3)];

        // ── 投影 ──────────────────────────────────────────────────

        /// <summary>立方体局部 3D 点 -> 图表局部像素（旋转 -> 正交投影丢 Z -> 缩放平移）</summary>
        public static Vector2 Project(Matrix4x4 rotation, float scale, Vector2 offset, Vector3 point)
        {
            Vector3 rotated = rotation.MultiplyPoint3x4(point);
            return new Vector2(rotated.x * scale + offset.x, rotated.y * scale + offset.y);
        }

        /// <summary>投影后的包围盒（旋转 + 正交投影丢 Z 后的 XY 范围）。半边长为 1 时范围在 ±√2 内</summary>
        public static void ComputeProjectedBounds(Matrix4x4 rotation, out Vector2 min, out Vector2 max)
        {
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < Vertices.Length; i++)
            {
                Vector3 rotated = rotation.MultiplyPoint3x4(Vertices[i]);
                if (rotated.x < min.x) min.x = rotated.x;
                if (rotated.x > max.x) max.x = rotated.x;
                if (rotated.y < min.y) min.y = rotated.y;
                if (rotated.y > max.y) max.y = rotated.y;
            }
        }

        /// <summary>
        /// 用四元数投影：先换成旋转矩阵，再走 <see cref="Project"/>。
        /// 缩放/平移的算法（半边长口径 vs 视口适配口径）不在这里，见
        /// <see cref="XChartCubeView.ComputeProjection(Quaternion,float,Vector2,XChartCubeSizeMode,out float,out Vector2)"/>。
        /// </summary>
        public static Vector2 Project(Quaternion rotation, float scale, Vector2 offset, Vector3 point)
            => Project(XChartCubeView.ToMatrix(rotation), scale, offset, point);

        // ── 面帧（屏幕） ──────────────────────────────────────────

        /// <summary>
        /// 算出六个面在图表局部像素空间的四边形——这就是"协议"的来源。
        /// 输出顺序与 <see cref="CubeFace"/> 一致。
        /// </summary>
        public static void BuildFaceFrames(Matrix4x4 rotation, float scale, Vector2 offset,
            XChartFaceFrame[] output)
        {
            if (output == null || output.Length < FaceCount)
                return;

            for (int i = 0; i < FaceCount; i++)
            {
                var face = (CubeFace)i;
                Vector3 o3 = s_FaceOrigin[i];
                Vector3 s3 = s_FaceAxisS[i];
                Vector3 t3 = s_FaceAxisT[i];

                Vector2 c00 = Project(rotation, scale, offset, o3);
                Vector2 c01 = Project(rotation, scale, offset, o3 + t3);
                Vector2 c11 = Project(rotation, scale, offset, o3 + t3 + s3);
                Vector2 c10 = Project(rotation, scale, offset, o3 + s3);

                XChartFaceFrame frame = XChartFaceFrame.FromCorners(c00, c01, c11, c10);
                frame.FaceId = i;
                output[i] = frame;
            }
        }

        /// <summary>
        /// 一次把"四元数 + 半边长 + 口径"算成完整的投影状态与六个面帧。
        /// scale/offset 一并吐出来，数据类元素（散点等）要和背景共用同一份，不能各算一次。
        /// </summary>
        public static XChartFaceFrame[] BuildFaceFrames(Quaternion rotation, float halfEdge,
            Vector2 centerOffset, XChartCubeSizeMode mode, out float scale, out Vector2 offset)
        {
            Matrix4x4 matrix = XChartCubeView.ToMatrix(rotation);
            XChartCubeView.ComputeProjection(matrix, halfEdge, centerOffset, mode, out scale, out offset);
            var frames = new XChartFaceFrame[FaceCount];
            BuildFaceFrames(matrix, scale, offset, frames);
            return frames;
        }

        /// <summary>面的朝向是否对着观察者（旋转后法线 Z > 0）——填充面必须用它判可见性</summary>
        public static bool IsFaceVisible(Matrix4x4 rotation, CubeFace face)
            => rotation.MultiplyVector(s_FaceNormal[(int)face]).z > 0f;

        /// <summary>
        /// 棱是否朝向观察者：它相邻的两个面里只要有一个可见就算。
        /// 这样棱和面用同一套口径（旧实现里棱按平均 Z 判、网格线按法线判，两套口径）。
        /// </summary>
        public static bool IsEdgeVisible(Matrix4x4 rotation, int edgeIndex)
        {
            for (int slot = 0; slot < 2; slot++)
            {
                int face = s_EdgeAdjacentFaces[edgeIndex, slot];
                if (face < 0) continue;
                if (IsFaceVisible(rotation, (CubeFace)face)) return true;
            }
            return false;
        }

        /// <summary>面的旋转后 Z（面心深度），供"先画远的再画近的"排序</summary>
        public static float GetFaceDepth(Matrix4x4 rotation, CubeFace face)
        {
            Vector3 center = s_FaceOrigin[(int)face]
                + s_FaceAxisS[(int)face] * 0.5f
                + s_FaceAxisT[(int)face] * 0.5f;
            return rotation.MultiplyPoint3x4(center).z;
        }
    }
}