using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using XericUI.Core;

namespace XericUI.XChart.Mesh
{
    /// <summary>
    /// 顶点预算分块（纯算，离线可验）。
    ///
    /// <para><b>为什么要有它</b>：UGUI 的 65535 顶点上限是<b>每个 Graphic 各自</b>的，而有些元素是
    /// 按记录数长网格的（箭头一条记录 2 个四边形、散点一个点 1 个四边形）⇒ 记录一多必然撞上
    /// （他 2026-10-23 灌 10000 条时现场抛 `Mesh can not have more than 65000 vertices`）。
    /// 所以提交前先按预算把面切成若干块，每块一个 Graphic。</para>
    /// </summary>
    public static class XChartMeshBudget
    {
        /// <summary>每块顶点上限。65535 是硬上限，这里留足余量（还要给别的顶点与实现细节留空间）。</summary>
        public const int ChunkVertexBudget = 16000;

        /// <summary>
        /// 切块：把 <paramref name="faceCount"/> 个面按"每面顶点数"切成若干块，
        /// 各块起始面下标写进 <paramref name="starts"/>，返回块数。
        /// 块 k 覆盖面 <c>[starts[k], starts[k+1])</c>，最后一块到 <paramref name="faceCount"/>。
        /// 顺序不变、不重不漏；单面就超过预算时它自成一块（无解，只能交给渲染器）。
        /// </summary>
        public static int Split(int[] faceVertexCounts, int faceCount, int budget, int[] starts)
        {
            if (faceVertexCounts == null) throw new ArgumentNullException(nameof(faceVertexCounts));
            if (starts == null) throw new ArgumentNullException(nameof(starts));
            if (starts.Length < 1) throw new ArgumentException("starts 至少要有 1 个位置");
            if (faceCount <= 0)
            {
                starts[0] = 0;
                return 0;
            }
            if (budget < 1) budget = 1;

            int chunks = 0;
            int start = 0;
            int used = 0;
            for (int i = 0; i < faceCount; i++)
            {
                int vertices = Mathf.Max(1, faceVertexCounts[i]);
                if (used > 0 && used + vertices > budget)
                {
                    starts[chunks++] = start;
                    start = i;
                    used = 0;
                }
                used += vertices;
            }
            starts[chunks++] = start;
            return chunks;
        }

        /// <summary>各块顶点数的最大值（报表用：报告"最大的那个 Graphic 有多少顶点"）</summary>
        public static int MaxChunkVertices(int[] faceVertexCounts, int faceCount, int[] starts, int chunkCount)
        {
            int max = 0;
            for (int c = 0; c < chunkCount; c++)
            {
                int end = (c + 1 < chunkCount) ? starts[c + 1] : faceCount;
                int sum = 0;
                for (int i = starts[c]; i < end; i++) sum += Mathf.Max(1, faceVertexCounts[i]);
                if (sum > max) max = sum;
            }
            return max;
        }
    }

    /// <summary>
    /// 面填充渲染器：提交若干四边形，<b>按顶点预算自动分块</b>，每块一个子 Graphic。
    ///
    /// <para>每面可以纯色填充，也可以"四角色 + 中心色"径向渐变（背景做浅淡的立体感很需要这个）。
    /// 面的<b>提交顺序就是绘制顺序</b>，所以调用方要按深度从远到近喂（画家算法）——
    /// 半透明面叠在一起时，顺序错了颜色就错了。分块不改这个顺序：块按提交顺序排，块内也按顺序。</para>
    ///
    /// <para>不参与射线检测（背景不该挡住交互）。</para>
    /// </summary>
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
        }

        /// <summary>一块：只画 [Start, End) 之间的面。顶点数受预算限制，所以不会撞 65535 上限。</summary>
        private sealed class FaceChunk : MaskableGraphic
        {
            public XChartMeshFillRenderer Owner;
            public int FirstFace;
            public int LastFace;

            protected override void OnEnable()
            {
                base.OnEnable();
                raycastTarget = false;
            }

            protected override void OnPopulateMesh(VertexHelper vh)
            {
                vh.Clear();
                if (Owner != null) Owner.PopulateSlice(vh, FirstFace, LastFace);
            }
        }

        private readonly List<FaceQuad> m_Faces = new List<FaceQuad>(8);
        private readonly List<FaceChunk> m_Chunks = new List<FaceChunk>();
        private int[] m_FaceVertexCounts = new int[0];
        private int[] m_ChunkStarts = new int[8];

        /// <summary>报表用：这一帧提交了几个面 / 切成几块 / 最大的那块有多少顶点</summary>
        public int FaceCount => m_Faces.Count;
        public int ChunkCount => m_Chunks.Count;
        public int MaxChunkVertices { get; private set; }

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
            m_Faces.Add(new FaceQuad
            {
                Corner00 = corner00,
                Corner01 = corner01,
                Corner11 = corner11,
                Corner10 = corner10,
                EdgeColor = edgeColor,
                CenterColor = centerColor,
            });
        }

        /// <summary>提交：按预算分块，然后各块重画</summary>
        public void Apply()
        {
            RebuildChunks();
            SetVerticesDirty();   // 本体不画东西，但仍刷一次，保证子块的顺序紧跟其后
            for (int i = 0; i < m_Chunks.Count; i++) m_Chunks[i].SetVerticesDirty();
        }

        private void RebuildChunks()
        {
            if (m_FaceVertexCounts.Length < m_Faces.Count) m_FaceVertexCounts = new int[m_Faces.Count];
            for (int i = 0; i < m_Faces.Count; i++)
                m_FaceVertexCounts[i] = IsGradient(m_Faces[i]) ? 5 : 4;

            if (m_ChunkStarts.Length < m_Faces.Count + 1) m_ChunkStarts = new int[m_Faces.Count + 1];
            int chunks = XChartMeshBudget.Split(m_FaceVertexCounts, m_Faces.Count,
                XChartMeshBudget.ChunkVertexBudget, m_ChunkStarts);
            MaxChunkVertices = XChartMeshBudget.MaxChunkVertices(m_FaceVertexCounts, m_Faces.Count,
                m_ChunkStarts, chunks);

            EnsureChunkCount(chunks);
            for (int c = 0; c < chunks; c++)
            {
                m_Chunks[c].FirstFace = m_ChunkStarts[c];
                m_Chunks[c].LastFace = (c + 1 < chunks) ? m_ChunkStarts[c + 1] : m_Faces.Count;
                m_Chunks[c].enabled = true;
            }
            for (int c = chunks; c < m_Chunks.Count; c++) m_Chunks[c].enabled = false;   // 多余的块停掉，不画空网格
        }

        private void EnsureChunkCount(int count)
        {
            while (m_Chunks.Count < count)
            {
                var chunk = new GameObject("FillChunk" + m_Chunks.Count, typeof(RectTransform)).AddComponent<FaceChunk>();
                chunk.Owner = this;
                var rect = (RectTransform)chunk.transform;
                rect.SetParent(transform, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                chunk.raycastTarget = false;
                m_Chunks.Add(chunk);
            }
        }

        /// <summary>本体不画东西：所有面都交给受预算约束的子块</summary>
        protected override void OnPopulateMesh(VertexHelper vh) => vh.Clear();

        private static bool IsGradient(FaceQuad face)
        {
            // Color32 是逐字节比较："两色不同"就说明这次要做渐变
            return face.CenterColor.r != face.EdgeColor.r
                || face.CenterColor.g != face.EdgeColor.g
                || face.CenterColor.b != face.EdgeColor.b
                || face.CenterColor.a != face.EdgeColor.a;
        }

        internal void PopulateSlice(VertexHelper vh, int start, int end)
        {
            if (start < 0) start = 0;
            if (end > m_Faces.Count) end = m_Faces.Count;

            for (int i = start; i < end; i++)
            {
                FaceQuad face = m_Faces[i];
                int first = vh.currentVertCount;

                vh.AddVert(face.Corner00, face.EdgeColor, new Vector2(0f, 0f));
                vh.AddVert(face.Corner01, face.EdgeColor, new Vector2(0f, 1f));
                vh.AddVert(face.Corner11, face.EdgeColor, new Vector2(1f, 1f));
                vh.AddVert(face.Corner10, face.EdgeColor, new Vector2(1f, 0f));

                if (IsGradient(face))
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
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            // 图形停用时子块也得停，否则会留下"看不见但还在"的空网格
            for (int i = 0; i < m_Chunks.Count; i++)
            {
                if (m_Chunks[i] != null) m_Chunks[i].enabled = false;
            }
        }
    }
}