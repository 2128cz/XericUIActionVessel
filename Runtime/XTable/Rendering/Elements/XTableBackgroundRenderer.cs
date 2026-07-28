using System.Collections.Generic;
using UnityEngine;
using XericLibrary.Runtime.UIGraph;
using PrimitiveType = XericLibrary.Runtime.UIGraph.PrimitiveType;

namespace XericUI.XTable.Rendering.Elements
{
    /// <summary>用于批量绘制 XTable 背景区域的图元渲染器。</summary>
    public sealed class XTableBackgroundRenderer : PrimitiveCacheRendererBase
    {
        /// <summary>以左上角为原点的背景区域及其填充颜色。</summary>
        public struct RectData
        {
            public Rect Rect;
            public Color Color;

            public RectData(Rect rect, Color color)
            {
                Rect = rect;
                Color = color;
            }
        }

        /// <summary>以左上角坐标系替换全部背景矩形。</summary>
        public void ReplaceRects(IReadOnlyList<RectData> rects)
        {
            ClearAll();
            if (rects == null) return;

            for (int i = 0; i < rects.Count; i++)
            {
                RectData rectData = rects[i];
                Rect rect = rectData.Rect;
                AddPrimitive(new PrimitiveCacheEntry
                {
                    type = PrimitiveType.Rectangle,
                    sizeMode = SizeMode.InscribedEllipse,
                    center = new Vector2(rect.x + rect.width * 0.5f, -(rect.y + rect.height * 0.5f)),
                    size = rect.size,
                    @params = new PrimitiveParams
                    {
                        axisScaleX = 1f,
                        axisScaleY = 1f,
                        chamferSegments = 1,
                        bgColor = rectData.Color,
                        centerColor = rectData.Color,
                        borderColor = Color.clear,
                        borderThickness = 0f,
                    },
                });
            }
        }
    }
}
