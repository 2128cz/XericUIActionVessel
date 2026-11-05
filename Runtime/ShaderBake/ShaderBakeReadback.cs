using System;
using UnityEngine;

namespace XericUI.ShaderBake
{
	/// <summary>
	/// RenderTexture → 可读 Texture2D，以及透明压平。
	///
	/// <para><b>为什么用 ReadPixels 而不是 AsyncGPUReadback</b>：后者要等帧，回调里的数据这一轮就拿不到，
	/// 而"点导出→拿到文件"这条链要的是同步返回。烘焙一张图本来就是离线动作， stall 一帧无所谓。</para>
	/// </summary>
	public static class ShaderBakeReadback
	{
		/// <summary>
		/// 把目标读回成可读纹理。<paramref name="linear"/> 决定纹理按线性还是 sRGB 解释，
		/// 必须和生成它的那张 RenderTexture 一致，否则同一张图在编辑器和文件里会差一层 gamma。
		/// </summary>
		public static Texture2D ToTexture2D(RenderTexture source, TextureFormat format, bool linear, bool mipChain = false)
		{
			if (source == null)
				throw new ArgumentNullException(nameof(source));

			var texture = new Texture2D(source.width, source.height, format, mipChain, linear)
			{
				name = source.name,
				hideFlags = HideFlags.DontSave
			};
			CopyInto(source, texture, mipChain);
			return texture;
		}

		/// <summary>
		/// 读进一张已存在的纹理（尺寸要和 source 一致；不一致 Unity 只会填左上角，剩下留黑）。
		/// 走 RenderTexture.active + ReadPixels，MSAA 目标已被调用方解析过，这里读到的是解析后的像素。
		/// </summary>
		public static void CopyInto(RenderTexture source, Texture2D destination, bool recalculateMipMaps = false)
		{
			RenderTexture previous = RenderTexture.active;
			try
			{
				RenderTexture.active = source;
				destination.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0, recalculateMipMaps);
				destination.Apply(false);
			}
			finally
			{
				RenderTexture.active = previous;
			}
		}

		/// <summary>
		/// 把浮点/带 alpha 的纹理压成 RGB24：透明部分按 <paramref name="background"/> 混合，
		/// 分量截到 0~1。JPG 没有 alpha 通道，直接用原图编码会得到一圈发黑的边。
		/// </summary>
		public static Texture2D FlattenOntoBackground(Texture2D source, Color background)
		{
			int width = source.width;
			int height = source.height;
			var flattened = new Texture2D(width, height, TextureFormat.RGB24, false, false)
			{
				name = source.name,
				hideFlags = HideFlags.DontSave
			};

			Color[] pixels = source.GetPixels();
			for (int i = 0; i < pixels.Length; i++)
			{
				Color p = pixels[i];
				float a = Mathf.Clamp01(p.a);
				pixels[i] = new Color(
					Mathf.Lerp(background.r, p.r, a),
					Mathf.Lerp(background.g, p.g, a),
					Mathf.Lerp(background.b, p.b, a),
					1f);
			}
			flattened.SetPixels(pixels);
			flattened.Apply(false);
			return flattened;
		}

		/// <summary>
		/// 把一张纹理/RT 画成 Sprite。注意 Sprite 不拥有纹理：纹理销毁了 Sprite 就空了，
		/// 所以调用方要么留着纹理，要么连纹理一起作为资产存下来。
		/// </summary>
		public static Sprite ToSprite(Texture2D texture, Vector2 pivot, float pixelsPerUnit, string name = null)
		{
			if (texture == null)
				throw new ArgumentNullException(nameof(texture));
			var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), pivot, pixelsPerUnit);
			if (!string.IsNullOrEmpty(name))
				sprite.name = name;
			return sprite;
		}
	}
}
