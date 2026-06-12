using UnityEngine;
using UnityEditor;

namespace XericUIEditor.BubbleLayout
{
	/// <summary>
	/// BubbleLayout Editor 共享样式，提供白色圆角边框 + 深色底的 GUIStyle。
	/// </summary>
	internal static class BubbleLayoutEditorStyles
	{
		private static GUIStyle s_BoxStyle;
		private static bool s_Initialized;

		/// <summary>白色圆角边框 + 深灰黑色底的 Box 样式</summary>
		public static GUIStyle BoxStyle
		{
			get
			{
				EnsureInitialized();
				return s_BoxStyle;
			}
		}

		private static void EnsureInitialized()
		{
			if (s_Initialized)
				return;

			s_BoxStyle = new GUIStyle(GUI.skin.box)
			{
				padding = new RectOffset(12, 12, 10, 10),
				margin  = new RectOffset(4, 4, 4, 4),
			};

			const int borderThickness = 2;
			const int cornerRadius   = 4;

			s_BoxStyle.normal.background = GenerateRoundedBorderTexture(
				borderThickness, cornerRadius,
				Color.white,
				new Color(0.155f, 0.155f, 0.155f, 1f),
				out var borderInset);

			s_BoxStyle.border = borderInset;

			s_Initialized = true;
		}

		/// <summary>
		/// 生成一张用于 GUIStyle 9-slice 的圆角边框纹理。
		/// 
		/// 纹理布局（9-slice）：
		///   borderInset ─┤├─ edge ─┤├─ borderInset
		///   
		///   左上角块   │  上边缘  │  右上角块
		///   ──────────┼─────────┼──────────
		///   左边缘     │  中心    │  右边缘
		///   ──────────┼─────────┼──────────
		///   左下角块   │  下边缘  │  右下角块
		/// </summary>
		private static Texture2D GenerateRoundedBorderTexture(
			int borderThickness,
			int cornerRadius,
			Color borderColor,
			Color fillColor,
			out RectOffset borderInset)
		{
			// 角块大小必须能容纳整个圆角装饰 (圆角半径 + 边框宽度)
			int cornerBlockSize = cornerRadius + borderThickness + 1;
			// 边缘条带宽度
			int edgeWidth = 4;
			// 中心区域最小尺寸
			int centerSize = 2;

			int texSize = cornerBlockSize * 2 + edgeWidth;
			// 确保中心区域有像素
			if (edgeWidth < centerSize)
			{
				edgeWidth = centerSize;
				texSize = cornerBlockSize * 2 + edgeWidth;
			}

			borderInset = new RectOffset(cornerBlockSize, cornerBlockSize, cornerBlockSize, cornerBlockSize);

			var tex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
			tex.wrapMode = TextureWrapMode.Clamp;
			tex.filterMode = FilterMode.Bilinear;

			Color transparent = new Color(0, 0, 0, 0);
			Color[] pixels = new Color[texSize * texSize];

			int maxCoord = texSize - 1;
			float outerR = cornerRadius + borderThickness;   // 外圆角半径（白色边框的外缘）
			float innerR = cornerRadius;                      // 内圆角半径（深色填充的外缘）

			for (int y = 0; y < texSize; y++)
			{
				for (int x = 0; x < texSize; x++)
				{
					// 像素是否在「外圆角矩形」内（白色边框区域）
					bool inOuter = IsInsideRoundedRect(x, y, 0, 0, maxCoord, maxCoord, outerR);
					if (!inOuter)
					{
						pixels[y * texSize + x] = transparent;
						continue;
					}

					// 像素是否在「内圆角矩形」内（深色填充区域）
					int innerMin = borderThickness;
					int innerMax = maxCoord - borderThickness;

					bool inInner = IsInsideRoundedRect(
						x, y,
						innerMin, innerMin,
						innerMax, innerMax,
						innerR);

					pixels[y * texSize + x] = inInner ? fillColor : borderColor;
				}
			}

			tex.SetPixels(pixels);
			tex.Apply();
			return tex;
		}

		/// <summary>
		/// 判断坐标 (x,y) 是否在由 (minX,minY)-(maxX,maxY) 定义的圆角矩形内。
		/// </summary>
		private static bool IsInsideRoundedRect(
			int x, int y,
			int minX, int minY,
			int maxX, int maxY,
			float radius)
		{
			// 完全在边界外
			if (x < minX || x > maxX || y < minY || y > maxY)
				return false;

			// 主体矩形区域（远离四个角）
			if (x >= minX + radius && x <= maxX - radius) return true;
			if (y >= minY + radius && y <= maxY - radius) return true;

			// 四个圆角区域 —— 判断是否在对应的四分之一圆内
			float cx, cy;

			if (x < minX + radius && y < minY + radius)
			{
				// 左上角
				cx = minX + radius;
				cy = minY + radius;
			}
			else if (x > maxX - radius && y < minY + radius)
			{
				// 右上角
				cx = maxX - radius;
				cy = minY + radius;
			}
			else if (x < minX + radius && y > maxY - radius)
			{
				// 左下角
				cx = minX + radius;
				cy = maxY - radius;
			}
			else if (x > maxX - radius && y > maxY - radius)
			{
				// 右下角
				cx = maxX - radius;
				cy = maxY - radius;
			}
			else
			{
				// 边缘条带区域（不在任何角区），在矩形边界内
				return true;
			}

			float dx = x - cx;
			float dy = y - cy;
			return (dx * dx + dy * dy) <= (radius * radius);
		}
	}
}
