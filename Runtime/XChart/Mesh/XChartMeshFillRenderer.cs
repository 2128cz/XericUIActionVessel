using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using XericUI.Core;

namespace XericUI.XChart.Mesh
{
    /// <summary>
    /// 一个填充四边形怎么上色（<b>纯枚举</b>，住在预算算式旁边是为了让"几顶点"这条判据离线可验）。
    ///
    /// <list type="bullet">
    /// <item><see cref="Flat"/>：四角同色（4 顶点 2 三角）。</item>
    /// <item><see cref="Radial"/>：四角一色 + 中心一色（5 顶点 4 三角，做浅淡的立体感）。</item>
    /// <item><see cref="Linear"/>：沿 00/10 ↔ 01/11 这一对边线性渐变（4 顶点 2 三角）——
    /// 箭头杆"起点色 → 终点色"走的是这一档。</item>
    /// </list>
    /// </summary>
    public enum XChartFaceFillKind
    {
        Flat = 0,
        Radial = 1,
        Linear = 2,
    }

    /// <summary>
    /// 顶点预算（纯算，离线可验）。
    ///
    /// <para><b>为什么要有它</b>：UGUI 的顶点上限是<b>每个 Graphic 各自</b>的，而且门槛是
    /// <b>65000</b> 不是 65535（`VertexHelper.FillMesh`：`if (m_Positions.Count >= 65000) throw`，
    /// 他 2026-10-23 与 2026-11-11 各现场中过一次），而有些元素是按记录数长网格的
    /// （散点一个点 1 个四边形、圆柱填充一片轮廓）⇒ 数据一多就会撞。</para>
    ///
    /// <para>填充这一侧<b>不分块</b>（2026-11-08 他要求：自己写简单填充，不要那套 chunk 子物体），
    /// 所以判据从"切成几块"换成<b>"装不下的那部分必须整段放弃、并且必须报出来"</b>——
    /// 悄悄少画和崩溃是同一种坏。</para>
    /// </summary>
    public static class XChartMeshBudget
    {
        /// <summary>
        /// UGUI 每个 Graphic 的硬上限：`VertexHelper.FillMesh` 的判据是
        /// `if (m_Positions.Count >= 65000) throw`（ugui 1.0.0，VertexHelper.cs:214），
        /// 所以能装的是 <b>64999</b>，不是 65535——写 65535 会让 65000..65535 这一段必然抛。
        /// </summary>
        public const int GraphicVertexCap = 64999;

        /// <summary>
        /// 按提交顺序能装进 <paramref name="cap"/> 的<b>前缀</b>面数。
        ///
        /// <para>只取前缀是硬要求：提交顺序＝绘制顺序（画家算法），所以"装不下的"不能重排、
        /// 也不能插到前面去——只能整段放弃。被放弃的面由渲染器报出来（报表里的 `fillRefused=`），
        /// 不能悄悄少画。</para>
        /// </summary>
        public static int FacesWithinCap(int[] faceVertexCounts, int faceCount, int cap)
        {
            if (faceVertexCounts == null) throw new ArgumentNullException(nameof(faceVertexCounts));
            if (faceCount <= 0) return 0;
            if (cap < 1) return 0;

            int accepted = 0;
            int used = 0;
            for (int i = 0; i < faceCount; i++)
            {
                int vertices = Mathf.Max(1, faceVertexCounts[i]);
                // 装不下的只能整段放弃（顺序不许重排），被放弃的面由报表 fillRefused= 报出来
                if (used + vertices > cap) break;
                used += vertices;
                accepted++;
            }
            return accepted;
        }

        /// <summary>
        /// 一个面要几个顶点：只有<b>径向渐变</b>要多一个中心顶点（5 个、4 三角），
        /// 平坦与线性渐变都是 4 顶点 2 三角。
        ///
        /// <para>预算和渲染两侧必须吃这<b>同一个</b>定义：两边各写一套就会出现"预算以为 4 个、
        /// 实际发 5 个"，正好卡线时撞上限。</para>
        /// </summary>
        public static int VerticesPerFace(XChartFaceFillKind kind)
            => kind == XChartFaceFillKind.Radial ? 5 : 4;
    }

    /// <summary>
    /// 面填充渲染器：把四边形<b>写进自己这一个 Graphic 的网格</b>——没有子物体，也没有第二套渲染脚本。
    ///
    /// <para>每面可以纯色填充，也可以"四角色 + 中心色"径向渐变（背景做浅淡的立体感很需要这个），
    /// 或"一对边各一色"的线性渐变。</para>
    ///
    /// <para>面的<b>提交顺序就是绘制顺序</b>，所以调用方要按深度从远到近喂（画家算法）——
    /// 半透明面叠在一起时，顺序错了颜色就错了。这条决定了上限超了以后只能<b>按顺序整段放弃</b>：
    /// 不能把装不下的面挪到前面去（<see cref="XChartMeshBudget.FacesWithinCap"/>），也不能悄悄少画。</para>
    ///
    /// <para>不参与射线检测（背景不该挡住交互）。</para>
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu(XericRegistrationPaths.Chart + "/Mesh/Cube Face Fill Renderer")]
    public sealed class XChartMeshFillRenderer : MaskableGraphic
    {
        private struct FaceQuad
        {
            public Vector2 Corner00;
            public Vector2 Corner01;
            public Vector2 Corner11;
            public Vector2 Corner10;
            public Color32 EdgeColor;
            public Color32 CenterColor;
            /// <summary>怎么上色（平坦 / 径向 / 线性）。<see cref="XChartMeshBudget.VerticesPerFace"/> 吃它</summary>
            public XChartFaceFillKind Kind;
        }

        private readonly List<FaceQuad> m_Faces = new List<FaceQuad>(8);
        private int[] m_FaceVertexCounts = new int[0];

        // 三个数分开看，因为 P23 那个坏点就是"提交侧的数好看、画面是空的"：
        // PlannedVertexCount 是提交那一刻算出来的，ShippedVertexCount 是 canvas 更新循环真写进
        // VertexHelper 的，HasCanvasRenderer 是"这一跳的入口在不在"。三个数凑不齐就是还没画。
        private int m_PlannedVertexCount;
        private int m_ShippedVertexCount;
        private int m_RefusedFaces;
        private int m_AcceptedFaces;

        /// <summary>报表用：提交了几个面 / 算出来要发几个顶点 / 真写进网格的顶点数 / 被上限挤掉的面</summary>
        public int FaceCount => m_Faces.Count;
        public int PlannedVertexCount => m_PlannedVertexCount;
        public int ShippedVertexCount => m_ShippedVertexCount;
        public int RefusedFaces => m_RefusedFaces;

        /// <summary>
        /// 报表用：这个 Graphic 到底有没有 CanvasRenderer。
        ///
        /// <para>必须走 <c>GetComponent</c>：`Graphic.canvasRenderer` 那个 getter 在缺的时候会被访问
        /// （它自己会 AddComponent），读它就把"有没有"变成"我补了一个"——判据不许写被检对象的状态。
        /// 缺的后果是 <c>Graphic.Rebuild</c> 第一句 `if (canvasRenderer == null …) return`
        /// （ugui@1.0.0 Graphic.cs:640）：网格填了、不画、也不报错。</para>
        /// </summary>
        public bool HasCanvasRenderer => GetComponent<CanvasRenderer>() != null;

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
        }

        /// <summary>清空已提交的面（每次重建网格前调用）</summary>
        public void ClearFaces() => m_Faces.Clear();

        /// <summary>
        /// 追加一个面。<paramref name="centerColor"/> 与 <paramref name="edgeColor"/> 不同时
        /// 走径向渐变（多一个中心顶点 + 4 个三角），相同则平坦填充（4 顶点 2 三角）。
        /// </summary>
        public void AddFace(Vector2 corner00, Vector2 corner01, Vector2 corner11, Vector2 corner10,
            Color edgeColor, Color centerColor)
        {
            bool gradient = edgeColor != centerColor;
            m_Faces.Add(new FaceQuad
            {
                Corner00 = corner00,
                Corner01 = corner01,
                Corner11 = corner11,
                Corner10 = corner10,
                EdgeColor = edgeColor,
                CenterColor = centerColor,
                Kind = gradient ? XChartFaceFillKind.Radial : XChartFaceFillKind.Flat,
            });
        }

        /// <summary>
        /// 追加一个<b>线性渐变</b>的四边形：<c>corner00</c> 与 <c>corner10</c> 取
        /// <paramref name="fromColor"/>，<c>corner01</c> 与 <c>corner11</c> 取 <paramref name="toColor"/>
        /// ——也就是"一对边一色"，渐变方向垂直于那两条边。
        ///
        /// <para>这一档是"一对边一色"，渐变方向垂直于那两条边，顶点数与平坦一样（4 个、2 三角）。
        /// <b>矢量场的箭头已经不用它了</b>（§29：杆改走曲线渲染器的共线贝塞尔，头用库的标记），
        /// 这里留着是因为它是这个渲染器本身的一种上色模式，别的元素要用渐变填充时不必再写第二套。</para>
        /// </summary>
        public void AddFaceLinear(Vector2 corner00, Vector2 corner01, Vector2 corner11, Vector2 corner10,
            Color fromColor, Color toColor)
        {
            m_Faces.Add(new FaceQuad
            {
                Corner00 = corner00,
                Corner01 = corner01,
                Corner11 = corner11,
                Corner10 = corner10,
                EdgeColor = fromColor,
                CenterColor = toColor,
                Kind = XChartFaceFillKind.Linear,
            });
        }

        /// <summary>
        /// 提交：按"每面顶点数"算这个 Graphic 装得下前几面（装不下的整段放弃、报出来），然后置脏
        /// 让 canvas 更新循环去发顶点。
        /// </summary>
        public void Apply()
        {
            if (m_FaceVertexCounts.Length < m_Faces.Count) m_FaceVertexCounts = new int[m_Faces.Count];
            for (int i = 0; i < m_Faces.Count; i++)
                m_FaceVertexCounts[i] = XChartMeshBudget.VerticesPerFace(m_Faces[i].Kind);

            m_AcceptedFaces = XChartMeshBudget.FacesWithinCap(m_FaceVertexCounts, m_Faces.Count,
                XChartMeshBudget.GraphicVertexCap);

            int used = 0;
            for (int i = 0; i < m_AcceptedFaces; i++) used += m_FaceVertexCounts[i];
            m_PlannedVertexCount = used;
            m_RefusedFaces = m_Faces.Count - m_AcceptedFaces;

            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            // `Graphic` 自己不清：s_VertexHelper 是静态共用的那一个，Clear 是 OnPopulateMesh 的责任
            // （ugui@1.0.0 Graphic.cs:694-700 —— 只有"矩形无效"那一支才替它清）。不清就会把上一批
            // 顶点留在网格里，读数也会跟着撒谎。
            vh.Clear();

            // 只写提交侧认下来的那前 m_AcceptedFaces 个面：提交顺序就是画家序，不能重排。
            // 读数用 vh.currentVertCount（真写进去的），不用"应该是几个"——两者相等才是这条链真的跑到终点。
            for (int i = 0; i < m_AcceptedFaces && i < m_Faces.Count; i++)
            {
                FaceQuad face = m_Faces[i];
                int first = vh.currentVertCount;

                // 线性渐变：00/10 这一对取 from（EdgeColor），01/11 取 to（CenterColor）；
                // 平坦与径向都是四角同色（径向另加一个中心顶点）
                bool linear = face.Kind == XChartFaceFillKind.Linear;
                Color32 corner01Color = linear ? face.CenterColor : face.EdgeColor;
                Color32 corner11Color = linear ? face.CenterColor : face.EdgeColor;

                vh.AddVert(face.Corner00, face.EdgeColor, new Vector2(0f, 0f));
                vh.AddVert(face.Corner01, corner01Color, new Vector2(0f, 1f));
                vh.AddVert(face.Corner11, corner11Color, new Vector2(1f, 1f));
                vh.AddVert(face.Corner10, face.EdgeColor, new Vector2(1f, 0f));

                if (face.Kind == XChartFaceFillKind.Radial)
                {
                    Vector2 center = (face.Corner00 + face.Corner01 + face.Corner11 + face.Corner10) * 0.25f;
                    vh.AddVert(center, face.CenterColor, new Vector2(0.5f, 0.5f));

                    vh.AddTriangle(first + 0, first + 1, first + 4);
                    vh.AddTriangle(first + 1, first + 2, first + 4);
                    vh.AddTriangle(first + 2, first + 3, first + 4);
                    vh.AddTriangle(first + 3, first + 0, first + 4);
                }
                else
                {
                    vh.AddTriangle(first + 0, first + 1, first + 2);
                    vh.AddTriangle(first + 2, first + 3, first + 0);
                }
            }

            m_ShippedVertexCount = vh.currentVertCount;
        }
    }
}