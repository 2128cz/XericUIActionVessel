using UnityEngine;
using XericLibrary.Runtime.UIGraph;
using UIPrimitiveType = XericLibrary.Runtime.UIGraph.PrimitiveType;
using XericUI.Core;

namespace XericUI.XChart.Pseudo3D
{
    /// <summary>
    /// 散点图元渲染器。
    ///
    /// 继承 PrimitiveCacheRendererBase，支持在一个 MaskableGraphic 中批量渲染多个图元。
    /// 所有散点共享同一套形状参数，但位置和颜色可独立设置。
    ///
    /// 相比为每个点创建独立的 UIPrimitiveRenderer，本组件大幅减少 GameObject 数量和 Draw Call。
    /// </summary>
    [AddComponentMenu(XericRegistrationPaths.Chart + "/Pseudo3D/Scatter Point Renderer")]
    public class XScatterPointRenderer : PrimitiveCacheRendererBase
    {
        // ── 全局形状参数 ──────────────────────────────────────────

        /// <summary>图元类型（多边形 / 矩形）</summary>
        public UIPrimitiveType pointType = UIPrimitiveType.Polygon;

        /// <summary>尺寸模式</summary>
        public SizeMode sizeMode = SizeMode.InscribedCircle;

        /// <summary>点大小（像素）</summary>
        public float pointSize = 8f;

        /// <summary>填充颜色</summary>
        public Color fillColor = Color.white;

        /// <summary>中心颜色（渐变中心）</summary>
        public Color centerColor = Color.white;

        /// <summary>边框颜色</summary>
        public Color borderColor = Color.black;

        /// <summary>边框宽度</summary>
        public float borderThickness = 1f;

        /// <summary>多边形边数（仅 polygon 类型有效）</summary>
        [Range(3, 64)]
        public int sideCount = 8;

        /// <summary>倒角大小</summary>
        [Range(0f, 1f)]
        public float chamferSize = 0f;

        /// <summary>倒角分段数</summary>
        [Range(1, 16)]
        public int chamferSegments = 4;

        /// <summary>旋转角度</summary>
        [Range(-180f, 180f)]
        public float angle = 0f;

        // ── 公共 API ──────────────────────────────────────────────

        /// <summary>
        /// 批量设置散点位置。
        /// 清除所有现有图元，用新位置重建。
        /// </summary>
        /// <param name="positions">2D 位置数组</param>
        /// <param name="colors">可选：每个点的颜色（null 则使用 fillColor）</param>
        public void SetPoints(Vector2[] positions, Color[] colors = null)
        {
            // null 的意思是"没有点了，清空"。当成数据去读 .Length 就是一条 NRE，
            // 而这条 NRE 会把管理器渲染循环里后面的所有工具一起带走（现象：加一个元素，全图不显示）。
            if (positions == null)
            {
                ClearAll();
                return;
            }

            ClearAll();

            for (int i = 0; i < positions.Length; i++)
            {
                Color c = (colors != null && i < colors.Length) ? colors[i] : fillColor;

                var entry = new PrimitiveCacheEntry
                {
                    type = pointType,
                    sizeMode = sizeMode,
                    center = positions[i],
                    size = new Vector2(pointSize, pointSize),
                    angle = angle,
                    @params = new PrimitiveParams
                    {
                        axisScaleX = 1f,
                        axisScaleY = 1f,
                        sideCount = sideCount,
                        chamferSize = chamferSize,
                        chamferSegments = chamferSegments,
                        bgColor = c,
                        centerColor = centerColor,
                        borderColor = borderColor,
                        borderThickness = borderThickness,
                    },
                };

                AddPrimitive(entry);
            }
        }

        /// <summary>
        /// 更新单个点的位置（不重建所有点，仅标记脏）。
        /// </summary>
        public void UpdatePointPosition(int index, Vector2 position)
        {
            if (index < 0 || index >= m_Primitives.Count) return;

            var entry = m_Primitives[index];
            entry.center = position;
            m_Primitives[index] = entry;
            SetPrimitiveDirty(index);
        }

        /// <summary>
        /// 更新单个点的颜色
        /// </summary>
        public void UpdatePointColor(int index, Color color)
        {
            if (index < 0 || index >= m_Primitives.Count) return;

            var entry = m_Primitives[index];
            entry.@params.bgColor = color;
            m_Primitives[index] = entry;
            SetPrimitiveDirty(index);
        }

        /// <summary>更新全局形状参数并重建所有点</summary>
        public void UpdateShapeParams()
        {
            for (int i = 0; i < m_Primitives.Count; i++)
            {
                var entry = m_Primitives[i];
                entry.type = pointType;
                entry.sizeMode = sizeMode;
                entry.size = new Vector2(pointSize, pointSize);
                entry.angle = angle;
                entry.@params.sideCount = sideCount;
                entry.@params.chamferSize = chamferSize;
                entry.@params.chamferSegments = chamferSegments;
                entry.@params.borderThickness = borderThickness;
                entry.@params.borderColor = borderColor;
                entry.@params.centerColor = centerColor;
                m_Primitives[i] = entry;
            }
            RebuildAll();
        }

        // ── 编辑器 ────────────────────────────────────────────────

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            if (sideCount < 3) sideCount = 3;
            if (pointSize < 0.5f) pointSize = 0.5f;
        }
#endif
    }
}